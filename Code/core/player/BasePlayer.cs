using Sandbox.Internal;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using XMovement;
using static Sandbox.Volumes.VolumeSystem;


#if FMOD
using FMODSbox;
#endif

namespace Core;

public abstract partial class BasePlayer : BaseEntity, Component.IDamageable, ISaveEvents, ISaveRoot
{
	public static BasePlayer Local { get; private set; }
	public virtual string PlayerName { get; protected set; } = "Player";
	public override string ToString() => $"{PlayerName}";
	[Property, Feature( "Defines" )] public PlayerMovement Movement { get; set; }
	[Property, Feature( "Defines" )] public PlayerWalkControllerComplex Controller { get; set; }
	[Property, Feature( "Defines" )] public HullCollider PickupTrigger { get; set; }

	[Property, Feature( "Defines" )] public PlayerConfig PlayerCfg { get; set; }

	protected override string GetEditorVis() { return null; }

	protected virtual Model GetViewmodelHands() { return PlayerCfg.ViewmodelHands; }

	[ConCmd( "noclip" )]
	private static void ToggleNoclip()
	{
		if ( Local.LifeState == LifeState.Alive )
			Local.Controller.IsNoclipping ^= true;
		else
			Local.Controller.IsNoclipping = false;
	}

	protected override void OnStart()
	{
		if ( !IsProxy )
			Local = this;

		// In some cases if we spawn very quickly we can die immediately from fall damage, this prevents it.
		Local?.Controller?.Controller?.Velocity = Vector3.Zero;

		base.OnStart();

		GameObject.Name = PlayerName;

		PickUpOwner ??= Local;

		// Becauase they are static set them off on spawn, this might be fucked for save loading (saved with god mid gunfight) or we just dont care
		Buddha = false;
		God = false;
		ShowHud = true;
		ShowCrosshair = true;

		Controller.BodyModelRenderer.OnFootstepEvent += OnFootstepEvent;

		ViewmodelHands.Model = GetViewmodelHands();

		if ( !ViewmodelWeapon.IsValid() )
			ViewmodelVisible = false;

		if ( HUDGameObject.IsValid() )
		{
			HUDGameObject.Components.GetOrCreate<ScreenPanel>().Enabled = true;

			if ( PlayerCfg?.HudEntries.Count > 0 )
			{
				foreach ( var entry in PlayerCfg.HudEntries )
				{
					if ( string.IsNullOrWhiteSpace( entry.RazorPath ) )
						continue;

					string className = GetClassName( entry.RazorPath );

					TypeDescription type = GlobalGameNamespace.TypeLibrary.GetType( className );

					if ( type is null )
					{
						Log.Warning( $"Ignoring bad razor path: {entry.RazorPath}" );
						continue;
					}

					if ( !HUDGameObject.Components.Get( type.TargetType ).IsValid() )
						HUDGameObject.Components.Create( type );
				}
			}
		}

		// setup the gun
		ViewmodelWeapon.RenderType = ModelRenderer.ShadowRenderType.Off;
		ViewmodelHands.RenderType = ModelRenderer.ShadowRenderType.Off;

		ViewmodelWeapon?.OnAnimTagEvent += HandleAnimTag;
		PickupTrigger?.OnTriggerEnter += OnPickupTriggerEnter;
		PickupTrigger?.OnTriggerExit += OnPickupTriggerExit;
		Movement?.OnLanded += OnLandedEvent;
#if IGNIS || STANDALONE
		bool hasPendingSaveState = SaveSystem.HasPendingSavedRoots;
#else
		bool hasPendingSaveState = false;
#endif

		if ( !hasPendingSaveState )
		{
			foreach ( var player in Scene.GetAllComponents<IPlayerEvents>() )
			{
				player.OnSpawn( this );
			}
		}

#if FMOD
		var listener = Controller.Camera.Components.GetOrCreate<StudioListener>();
		listener.NonRigidbodyVelocity = false;
		listener.SetRigidbody( Controller.Controller.PhysicsBodyRigidbody );
#endif

		CheckPrefabSetup();
	}

	private string GetClassName( string path ) => System.IO.Path.GetFileNameWithoutExtension( path );

	// technically you shouldnt disable the player (like ever), but still
	protected override void OnDisabled()
	{
		base.OnDisabled();

		// clear both renderers
		ViewmodelWeapon = null;
		ViewmodelHands = null;
	}

	/// <summary>
	/// Draw debug overlay on footsteps
	/// </summary>
	[ConVar( "debug_footsteps" )] public static bool DebugFootsteps { get; set; } = false;

	TimeSince _timeSinceStep;
	private void OnFootstepEvent( SceneModel.FootstepEvent e )
	{
		if ( !Controller.Controller.IsOnGround ) return;
		if ( _timeSinceStep < 0.2f ) return;

		_timeSinceStep = 0;

		PlayFootstepSound( e.Transform.Position, e.Volume, e.FootId );
	}

	public void PlayFootstepSound( Vector3 worldPosition, float volume, int foot )
	{
		var tr = Scene.Trace
			.Ray( worldPosition + Vector3.Up * 10, worldPosition + Vector3.Down * 20 )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !tr.Hit || !tr.Surface.IsValid() )
		{
			if ( DebugFootsteps )
			{
				DebugOverlay.Sphere( new Sphere( worldPosition, volume ), duration: 10, color: Color.Red, overlay: true );
			}

			return;
		}
#if FMOD
		SolveNullStringsInSurface( tr.Surface, out var surfstring );
		var soundEvent = foot == 0 ? "event:/Physics/StepLeft" : "event:/Physics/StepRight";
		FootSound( soundEvent, GameObject, surfstring );
#else
		var left = (tr.Surface.SoundCollection.FootLeft ?? tr.Surface.GetBaseSurface().SoundCollection.FootLeft ?? tr.Surface.GetBaseSurface().GetBaseSurface().SoundCollection.FootLeft ?? tr.Surface.GetBaseSurface().GetBaseSurface().GetBaseSurface().SoundCollection.FootLeft);
		var right = (tr.Surface.SoundCollection.FootRight ?? tr.Surface.GetBaseSurface().SoundCollection.FootRight ?? tr.Surface.GetBaseSurface().GetBaseSurface().SoundCollection.FootRight ?? tr.Surface.GetBaseSurface().GetBaseSurface().GetBaseSurface().SoundCollection.FootRight);
		var soundEvent = foot == 0 ? left : right;
#endif

		FootSound( soundEvent, GameObject );

		if ( DebugFootsteps )
		{
			DebugOverlay.Sphere( new Sphere( worldPosition, volume ), duration: 10, overlay: true );
#if FMOD
			DebugOverlay.Text( worldPosition, $"{soundEvent}", size: 14, flags: TextFlag.LeftTop, duration: 10, overlay: true );
#else
			DebugOverlay.Text( worldPosition, $"{soundEvent.ResourceName}", size: 14, flags: TextFlag.LeftTop, duration: 10, overlay: true );
#endif
		}

	}
#if FMOD
	/// <summary>
	/// A helper function to play a footstep sound the proper way that hopefully wont shit the bed
	/// </summary>
	/// <param name="path">Event string</param>
	/// <param name="obj">GameObject to play this on</param>
	/// <param name="surfstring">Surface string (fmod only)</param>
	static public void FootSound( string path, GameObject obj, string surfstring = null )
	{

		var handle = FMODSound.Create( path );
		var newsnd = SolveFootstepSoundOverrides( surfstring );

		if ( !string.IsNullOrWhiteSpace( newsnd ) )
		{
			FMODSound.SetParameter( handle, "parameter:/Physics/MaterialType", newsnd );
		}
		handle.setVolume( FootstepVolume );

		FMODSound.Play( handle, obj );

	}
#else
	/// <summary>
	/// A helper function to play a footstep sound the proper way that hopefully wont shit the bed
	/// </summary>
	/// <param name="path">Event</param>
	/// <param name="obj">GameObject to play this on</param>
	static public void FootSound( SoundEvent path, GameObject obj )
	{
		var handle = obj.PlaySound( path, 0 );
		handle.Volume *= FootstepVolume;
		handle.SpacialBlend = 0; // 2D footsteps

	}
#endif

#if FMOD
	static public void SolveNullStringsInSurface( Surface surface, out string surfstring )
	{
		surfstring = string.IsNullOrWhiteSpace( surface?.SoundCollection.SurfaceParameter ) ?
		(string.IsNullOrWhiteSpace( surface?.GetBaseSurface()?.SoundCollection.SurfaceParameter ) ?
		(string.IsNullOrWhiteSpace( surface?.GetBaseSurface()?.GetBaseSurface()?.SoundCollection.SurfaceParameter ) ?
		(string.IsNullOrWhiteSpace( surface?.GetBaseSurface()?.GetBaseSurface()?.GetBaseSurface()?.SoundCollection.SurfaceParameter ) ? null :
			surface?.GetBaseSurface()?.GetBaseSurface()?.GetBaseSurface()?.SoundCollection.SurfaceParameter) :
			surface?.GetBaseSurface()?.GetBaseSurface()?.SoundCollection.SurfaceParameter) :
			surface?.GetBaseSurface()?.SoundCollection.SurfaceParameter) :
			surface?.SoundCollection.SurfaceParameter;
	}

	/// <summary>
	/// Certain conditions force specific sounds, primarily being in water
	/// </summary>
	/// <param name="basesurface">The "proper" surface if this isnt water</param>
	/// <returns>Result surface to sound</returns>
	protected static string SolveFootstepSoundOverrides( string basesurface )
	{
		return Local.WaterLevel switch
		{
			WaterLvl.Feet => "Slosh",
			WaterLvl.Waist or WaterLvl.Full => "Wade",
			_ => basesurface,
		};
	}

	static public bool IsNullOrEmptyOrNotEvent( string input )
	{
		if ( string.IsNullOrWhiteSpace( input ) )
			return true;

		if ( !input.StartsWith( "event:/" ) )
			return true;

		return false;
	}
#endif
	static public float FootstepVolume { get; set; } = 0.33333f; // this is like -9.5db which is eh

	override protected void OnUpdate()
	{
		if ( IsProxy )
			return;

		CalculateFOV();

		if ( _allowSway ) ViewmodelUpdate();
	}

	override protected void OnFixedUpdate()
	{
		if ( IsProxy )
			return;

		CheckWaterLevel();
		HandleWeaponSelection();
		HandleWeaponInventory();
		WantsSprint();

		if ( _allowSway ) ViewmodelFixedUpdate();
		UpdateFallDamage();
		UpdatePickupPhysics();
	}

	/// <summary>
	/// A crazy way to know if we want to play sprint sound
	/// </summary>
	public void WantsSprint()
	{
		if ( Controller.WantSound )
			if ( Controller.IsRunning && Controller.Controller.Velocity.WithZ( 0 ).Length > 0 && Controller.Controller.IsOnGround && Input.AnalogMove.Length != 0 )
			{
				SprintSound();
				Controller.WantSound = false;
			}
	}


	protected virtual void SprintSound() => Sound.Play( "pl_sprint" );

	protected override void OnDestroy()
	{
		if ( Local == this ) Local = null;
	}

	public virtual string SaveRootKey => "LocalPlayer";
	public virtual GameObject SaveRootObject => GameObject;

	public override JsonObject CustomSerialize()
	{
		var state = base.CustomSerialize();

		if ( Controller.IsValid() )
		{
			state[nameof( Controller.EyeAngles )] = Json.ToNode( Controller.EyeAngles );
			state[nameof( Controller.LocalEyeAngles )] = Json.ToNode( Controller.LocalEyeAngles );
		}

		return state;
	}

	public override void CustomDeserialize( JsonObject node )
	{
		base.CustomDeserialize( node );

		if ( !Controller.IsValid() )
			return;

		if ( node[nameof( Controller.EyeAngles )] is JsonNode eyeAnglesNode )
			Controller.EyeAngles = Json.FromNode<Angles>( eyeAnglesNode );
		else if ( node[nameof( Controller.LocalEyeAngles )] is JsonNode localEyeAnglesNode )
			Controller.LocalEyeAngles = Json.FromNode<Angles>( localEyeAnglesNode );

		if ( Controller.Head.IsValid() )
			Controller.Head.WorldRotation = Controller.EyeAngles.ToRotation();
	}

	void ISaveRoot.AfterLoadRoot()
	{
		if ( !IsProxy )
			Local = this;

		RestoreWeaponOwnership();

		ForceWeaponChange();

		if ( CurrentWeapon.IsValid() )
			CurrentWeapon.Draw();
	}

	public float FallingSpeed = 0;
	protected void UpdateFallDamage()
	{
		if ( Controller.Controller.IsOnGround && FallingSpeed != 0f )
		{
			//if ( !PlayerAnimatedInteraction.StaticRef.Active )
			ApplyFallDamage( -FallingSpeed );
			//BasePlayerOld.StaticRef.IgnoreNextFallDamage = false;
			FallingSpeed = 0f;
		}
		else
		{
			FallingSpeed = Controller.Controller.Velocity.z;
		}
	}

	protected void ApplyFallDamage( float speed )
	{
		if ( LifeState != LifeState.Alive )
			return;

		if ( speed < 303 )
			return;

		//		TODO: Scale it down if we landed on something that's floating... (speed -= 173)
		//		TODO: Subtract the velocity of whatever we landed on, dont allow negative speed
		//		TODO: get sound volume and play sound

		if ( speed > 526.5f )
		{
			//			do damage
			speed -= 526.5f;
			speed *= 100.0f / (922.5f - 526.5f);
			speed = (float)Math.Floor( speed ); //	round down
												//			apply to player

			var damage = new DamageInfo()
			{
				Attacker = GameObject,
				Damage = speed,
				Tags = { "fall" }
			};
			this.OnDamage( damage );
		}
	}

	protected virtual void CheckPrefabSetup()
	{
		if ( !Movement.IsValid() )
			Log.Error( "Player is missing Movement!" );

		if ( !Controller.IsValid() )
			Log.Error( "Player is missing PlayerController!" );

		if ( !PickupTrigger.IsValid() )
			Log.Error( "Player is missing the Pickup Trigger!" );

		if ( !HUDGameObject.IsValid() )
			Log.Error( "HUD GameObject is missing!" );
	}
}
