using System;
using XMovement;
using Sandbox.Internal;

namespace Core;

[Hide]
public partial class BasePlayer : BaseEntity, Component.IDamageable
{
	public static BasePlayer Local;
	public virtual string PlayerName { get; protected set; } = "Player";
	public override string ToString() => $"{PlayerName}";
	[Property, Feature( "Defines" )] public PlayerMovement Movement { get; set; }
	[Property, Feature( "Defines" )] public PlayerWalkControllerComplex Controller { get; set; }
	[Property, Feature( "Defines" )] public ModelCollider PickupTrigger { get; set; }

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

		Log.Info( "Noclip: " + Local.Controller.IsNoclipping );
	}

	// default playercfg that needs to exist before the beginning of time
	//	public BasePlayer() { PlayerCfg = ResourceLibrary.Get<PlayerConfig>( "scripts/player_config.plr" ); }

	protected override void OnStart()
	{
		if ( !IsProxy )
			Local = this;

		// In some cases if we spawn very quickly we can die immediately from fall damage, this prevents it.
		if ( Controller?.Controller != null )
			Controller.Controller.Velocity = Vector3.Zero;
		
		base.OnStart();

		GameObject.Name = PlayerName;

		// It's annoying to get anything consistently or to debug without doing this
	//	GameObject.BreakFromPrefab();

		// becauase they are static set them off on spawn, this might be fucked for save loading (saved with god mid gunfight) or we just dont care
		Buddha = false;
		God = false;
		ShowHud = true;
		ShowCrosshair = true;

		Controller.BodyModelRenderer.OnFootstepEvent += OnFootstepEvent;

		ViewmodelHands.Model = GetViewmodelHands();

		if ( ViewmodelWeapon.Model == null )
			Local.WeaponGameObject.Enabled = false;

		if ( HUDGameObject.IsValid() )
		{
			HUDGameObject.Components.GetOrCreate<ScreenPanel>().Enabled = true;

			if ( PlayerCfg.HudEntries.Count > 0 )
			{
				foreach ( var entry in PlayerCfg.HudEntries )
				{
					if ( string.IsNullOrWhiteSpace( entry.RazorPath ) )
						continue;

					string className = GetClassName( entry.RazorPath );

					TypeDescription type = GlobalGameNamespace.TypeLibrary.GetType( className );

					if ( type != null )
						HUDGameObject.Components.Create( type );
					else
						Log.Warning( $"HUD class not found for {entry.RazorPath} (expected {className})" );
				}
			}
		}

		// setup the gun
		ViewmodelWeapon.RenderType = ModelRenderer.ShadowRenderType.Off;
		ViewmodelHands.RenderType = ModelRenderer.ShadowRenderType.Off;

		ViewmodelWeapon.OnAnimTagEvent = HandleAnimTag;

		PickupTrigger.OnTriggerEnter = OnPickupTriggerTouched;

		CheckPrefabSetup();
	}

	private string GetClassName( string path )
	{
		return System.IO.Path.GetFileNameWithoutExtension( path );
	}

	public void OnPickupTriggerTouched( Collider collider )
	{
		if ( collider.GameObject.Components.TryGet<BaseItem>( out var item ) ) { if ( item.AllowTouchPickup() ) item.OnPickup( this ); }
	}

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



		if ( !tr.Hit || tr.Surface is null )
		{
			if ( DebugFootsteps )
			{
				DebugOverlay.Sphere( new Sphere( worldPosition, volume ), duration: 10, color: Color.Red, overlay: true );
			}

			return;
		}

		var soundEvent = foot == 0 ? (tr.Surface.SoundCollection.FootLeft ?? tr.Surface.GetBaseSurface().SoundCollection.FootLeft) : (tr.Surface.SoundCollection.FootRight ?? tr.Surface.GetBaseSurface().SoundCollection.FootRight);
		if ( soundEvent is null )
		{
			if ( DebugFootsteps )
			{
				DebugOverlay.Sphere( new Sphere( worldPosition, volume ), duration: 10, color: Color.Orange, overlay: true );
			}

			return;
		}



		var handle = GameObject.PlaySound( soundEvent, 0 );
		handle.Volume *= volume * FootstepVolume;
		handle.SpacialBlend = 0; // 2D footsteps

		if ( DebugFootsteps )
		{
			DebugOverlay.Sphere( new Sphere( worldPosition, volume ), duration: 10, overlay: true );
			DebugOverlay.Text( worldPosition, $"{soundEvent.ResourceName}", size: 14, flags: TextFlag.LeftTop, duration: 10, overlay: true );
		}
	}

	public float FootstepVolume { get; set; } = 0.33333f;

	public static void ToggleViewmodel()
	{
		// Temp solution to not render hands for now when we don't need it.
		// TODO: Delete this when we'll do first person body
		Local.WeaponGameObject.Enabled ^= true;
	}

	override protected void OnUpdate()
	{
		base.OnUpdate();

		if ( IsProxy )
			return;

		ViewmodelUpdate();
	}

	override protected void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( IsProxy )
			return;

		ViewmodelFixedUpdate();
		UpdateFallDamage();
		HandleWeaponInventory();
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

		//		TODO: check if landed in water
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
