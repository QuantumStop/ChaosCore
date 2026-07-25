namespace Core;

#if FMOD
using FMODSbox;
using FMOD.Studio;
#endif
using System;

public partial class BasePlayer
{
	/// <summary> Player health </summary>
	[Property, ReadOnly, Feature( "Debug" )] public float Health = 100;
	/// <summary> Player armor </summary>
	[Property, ReadOnly, Feature( "Debug" )] public float Armour = 0;
	/// <summary>
	/// Does the player have suit on
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] public virtual bool HasSuit { get; protected set; } = false;
	/// <summary>
	/// Are we alive or are we dead, or a secret third thing
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] public LifeState LifeState = LifeState.Alive;

	/// <summary> Returns true if player is underwater. </summary>
	public bool IsUnderwater => WaterLevel == WaterLvl.Full;
	/// <summary>
	/// Where is the eye level compared to the player height (0-1)
	/// </summary>
	public float HeadRatio => Controller.HeadHeight / Controller.Height;

	protected virtual void CheckWaterLevel()
	{
		WaterLevel = Controller.WaterLevel switch
		{
			float level when level >= HeadRatio => WaterLvl.Full,
			>= 0.5f => WaterLvl.Waist, // i mean regardless of how high the player is, 0.5 will probably be the waist
			> 0f => WaterLvl.Feet,
			_ => WaterLvl.None
		};
	}

#if FMOD
	private EventInstance _underwaterInstance { get; set; }
	private EventInstance _underwaterSnapshot { get; set; }
#endif

	/// <summary>
	/// What is the amount of water the player is in
	/// </summary>
	public enum WaterLvl
	{
		/// <summary>
		/// Not in water
		/// </summary>
		None,
		/// <summary>
		/// Feet are in water
		/// </summary>
		Feet,
		/// <summary>
		/// Up to waist
		/// </summary>
		Waist,
		/// <summary>
		/// Underwater
		/// </summary>
		Full
	}

	[Property, ReadOnly, Feature( "Debug" )]
	public WaterLvl WaterLevel
	{
		get;
		set
		{
			if ( field == value ) return;

			WaterLevelChanged( field, value );
			field = value;
		}
	}

	/// <summary>
	/// Water level was changed
	/// </summary>
	/// <param name="oldvalue">Previous water level</param>
	/// <param name="newvalue">New "current" water level</param>
	protected virtual void WaterLevelChanged( WaterLvl oldvalue, WaterLvl newvalue )
	{
#if FMOD
		if ( newvalue == WaterLvl.Full )
		{
			_underwaterInstance = FMODSound.Play( "event:/Player/UnderwaterLoop" );
			_underwaterSnapshot = FMODSound.Play( "snapshot:/Underwater" ); // separately because ADHSR on snapshots hates stopping
		}
		else
		{
			if ( _underwaterInstance.isValid() ) FMODSound.Stop( _underwaterInstance );
			if ( _underwaterSnapshot.isValid() ) FMODSound.Stop( _underwaterSnapshot );
		}
#else
		// nothing yet :)
#endif
	}

	/// <summary>
	/// No damage whatsoever
	/// </summary>
	public static bool God { get; set; } = false;

	[ConCmd( "god" )]
	private static void ToggleGod()
	{
		God = !God;
		Buddha = false; // cant have both on

		Log.Info( "God: " + God );
	}

	public static bool Buddha { get; set; } = false;
	[ConCmd( "buddha" )]
	private static void ToggleBuddha()
	{
		Buddha = !Buddha;
		God = false;    // cant have both on

		Log.Info( "Buddha: " + Buddha );
	}

	// port from facepunch CharacterController
	public void Punch( in Vector3 amount )
	{
		Controller.Controller.ClearGround();
		Controller.Controller.Velocity += amount;
	}

	/// <summary>
	/// Deal damage to player
	/// </summary>
	/// <param name="dmginfo">Damage Information</param>
	public void OnDamage( in DamageInfo dmginfo )
	{
		if ( dmginfo.Damage == 0f || God || LifeState == LifeState.Dead )    //	early out if no damage or in god mode or dead
			return;

		if ( dmginfo.Tags.Has( "fall" ) && Local.WaterLevel > 0 ) // cant die from fall if fall in water (although really the velocity slowdown should already do it for us)
			return;

		if ( dmginfo.Damage < 0 )
		{
			Health += Math.Abs( dmginfo.Damage );
			return; //	if the damage is negative just apply it
		}

		float ratio = 0.2f; // Armor Takes 80% of the damage
		float bonus = 1f;   // Each Point of Armor is work 1/x points of health (HL1 uses 0.5, HL2 uses 1)

		if ( HasSuit && Armour > 0 && !dmginfo.Tags.Has( "fall" ) && !dmginfo.Tags.Has( "drown" ) && !dmginfo.Tags.Has( "poison" ) && !dmginfo.Tags.Has( "radiation" ) )
		{
			float flNew = dmginfo.Damage * ratio;
			float flArmour = Math.Max( (dmginfo.Damage - flNew) * bonus, 1f );

			// Does this use more armor than we have?
			if ( flArmour > Armour )
			{
				flArmour = Armour;
				flArmour *= 1 / bonus;
				flNew = dmginfo.Damage - flArmour;
				Armour = 0;
			}
			else
			{
				Armour -= flArmour;
			}

			dmginfo.Damage = flNew;
		}

		DamageInfo dmginfoTemp = dmginfo; // doesnt allow me to pass an "in" variable
		Scene.RunEvent<IPlayerEvents>( x => x.OnTookDamage( this, dmginfoTemp ) );

		//		TODO: haptic feedback
		//		TODO: Reset damage time countdown for each type of time based damage player just sustained

		//	Display any effect associate with this damage type
		DamageEffect( dmginfo );

		//		apply velocity to the player
		if ( dmginfo is CoreDamageInfo coreDamageInfo )
			Punch( coreDamageInfo.Force.WithZ( Math.Max( 0f, coreDamageInfo.Force.z ) ) );

		Health -= dmginfo.Damage;
		Health = Math.Clamp( Health, Buddha ? 1 : 0, 100 );

		if ( Health <= 0f )
		{
			Health = 0f;
			if ( LifeState == LifeState.Alive )
			{
				LifeState = LifeState.Dead;
				OnDeath( dmginfo );
			}
		}
	}

	/// <summary>
	/// What happens on player death
	/// </summary>
	/// <param name="dmginfo">Damage Information</param>
	public virtual void OnDeath( DamageInfo dmginfo )
	{
		//	Log.Info( "Player State: The player is currently dead" );

		//-- Penultimate enum that says, that our player is in fact dead --//
		LifeState = LifeState.Dead;

		Scene.RunEvent<IPlayerEvents>( x => x.OnDeath( this ) );

		// Below we are oding a bunch of sets to make sure we aren't doing 
		// anything in the world, all of it should be self explanatory
		LockPlayer();

		Local.SetFOV( this, 0, 0.2f, 0, true );

		Controller.HeadHeight = 24;

		Controller.Body.Tags.Add( "movement" );
		var bodyRagdoll = Controller.Body.AddComponent<ModelPhysics>();
		bodyRagdoll.Model = Controller.BodyModelRenderer.Model;
		bodyRagdoll.Renderer = Controller.BodyModelRenderer;

		ScreenFlash.Set( Color.Red, 50 );
	}

	/// <summary>
	/// Show damage screen effects
	/// </summary>
	/// <param name="dmginfo">Damage information</param>
	protected virtual void DamageEffect( DamageInfo dmginfo )
	{
		if ( dmginfo.Tags.IsEmpty ) return;

		float t = Math.Clamp( dmginfo.Damage / 100f, 0f, 1f );
		float strength = MathX.Lerp( 0.5f, 4f, t );
		float duration = MathX.Lerp( 0.1f, 0.6f, t );

		foreach ( var effect in dmginfo.Tags )
		{
			switch ( effect )
			{
				case DamageTypes.DMG_CRUSH:
					ScreenFlash.Set( Color.Red, 1.0f );
					CameraEffects.AddShake( strength, duration, frequency: 6f, shakePitch: true, shakeYaw: true, shakeRoll: true );
					break;
				case DamageTypes.DMG_DROWN:
					ScreenFlash.Set( Color.Blue, 1.0f );
					CameraEffects.AddShake( strength * 0.5f, duration * 2f, frequency: 2f, shakePitch: true, shakeYaw: false, shakeRoll: true );
#if FMOD
					FMODSound.Play( "event:/Player/PainDrown" );
#else
					Sound.Play( "pl_drown" );
#endif
					break;
				case DamageTypes.DMG_PLASMA:
					ScreenFlash.Set( Color.Cyan, 1.0f );
					CameraEffects.AddShake( strength, duration, frequency: 15f, shakePitch: true, shakeYaw: false, shakeRoll: false );
#if FMOD
					FMODSound.Play( "event:/Player/PainBurn" );
#else
					Sound.Play( "pl_fallpain" );
#endif
					break;
				case DamageTypes.DMG_BULLET:
					CameraEffects.AddTrauma( t * 0.4f );
					// Sharp directional punch toward hit origin
					CameraEffects.AddShake( strength * 1.2f, duration * 0.2f, frequency: 16f, shakePitch: true, shakeYaw: true, shakeRoll: false, sourcePosition: dmginfo.Position );
					// Brief roll snap
					CameraEffects.AddShake( strength * 0.5f, duration * 0.4f, frequency: 8f, shakePitch: false, shakeYaw: false, shakeRoll: true, delay: duration * 0.05f, sourcePosition: dmginfo.Position );
#if FMOD
					FMODSound.Play( "event:/Player/PainGeneric" );
#else
					Sound.Play( "pl_pain" );
#endif
					break;
				case DamageTypes.DMG_FALL:
					CameraEffects.AddShake( strength, duration * 1.5f, frequency: 3f, shakePitch: true, shakeYaw: false, shakeRoll: true );
#if FMOD
					FMODSound.Play( "event:/Player/PainFall" );
#else
					Sound.Play( "pl_fallpain" );
#endif
					break;
				case DamageTypes.DMG_BLAST:
					CameraEffects.AddShake( strength * 1.5f, duration * 2f, frequency: 4f, shakePitch: true, shakeYaw: true, shakeRoll: true, sourcePosition: dmginfo.Position );
					break;
				case DamageTypes.DMG_SLASH:
					CameraEffects.AddShake( strength, duration, frequency: 14f, shakePitch: false, shakeYaw: true, shakeRoll: true );
					break;
				case DamageTypes.DMG_SHOCK:
					CameraEffects.AddShake( strength, duration * 0.5f, frequency: 20f, shakePitch: true, shakeYaw: true, shakeRoll: true );
					break;
				case DamageTypes.DMG_SONIC:
					CameraEffects.AddTrauma( t * 0.6f );
					CameraEffects.AddShake( strength * 1.5f, duration * 0.3f, frequency: 18f, shakePitch: true, shakeYaw: true, shakeRoll: false );
					// Roll aftermath
					CameraEffects.AddShake( strength * 0.8f, duration * 3f, frequency: 1.2f, shakePitch: false, shakeYaw: false, shakeRoll: true, delay: duration * 0.15f );
#if FMOD
					FMODSound.Play( "event:/Player/PainGeneric" );
#else
					Sound.Play( "pl_fallpain" );
#endif
					break;
				case DamageTypes.DMG_CLUB:
					break;
			}
		}

		//	Log.Info( $"Player State: Player got {string.Join( ", ", dmginfo.Tags )} damage: {dmginfo.Damage}" );
	}

	/// <summary>
	/// Fuck the player off
	/// </summary>
	public void LockPlayer( bool controller = false )
	{
		if ( controller ) Controller.AllowMovement = false;
		else
		{
			Controller.Controller.Acceleration = 0;
			Controller.WalkSpeed = 0;
			Controller.RunSpeed = 0;
			Controller.DefaultSpeed = 0;
			Controller.JumpPower = 0;
			Controller.EnableCrouching = false;

			Controller.CrouchAction = "";
			Controller.EnableJumping = false;
			Controller.JumpAction = "";

			Controller.IsNoclipping = false;
			Controller.EnableSwimming = false;
			Controller.EnableUse = false;
			Controller.EnableLadders = false;
		}
	}

	/// <summary>
	/// Unfuck the player in
	/// </summary>
	public void UnlockPlayer( bool controller = false )
	{
		if ( controller ) Controller.AllowMovement = true;
		else
		{
			Controller.Controller.Acceleration = 10;
			Controller.WalkSpeed = 150;
			Controller.RunSpeed = 320;
			Controller.DefaultSpeed = 190;
			Controller.JumpPower = 160;
			Controller.EnableCrouching = true;

			Controller.CrouchAction = "duck";
			Controller.EnableJumping = true;
			Controller.JumpAction = "jump";

			Controller.IsNoclipping = false;
			Controller.EnableSwimming = true;
			Controller.EnableUse = true;
			Controller.EnableLadders = true;
		}
	}

#if !FMOD
	/// <summary>
	/// Because the Item gets deleted, we cant do the voice stealing trick to cut off the other sounds
	/// So we do it here this way
	/// </summary>
	/// <param name="name">Name of the event</param>
	/// <param name="channel">Which "channel" do we play at, or medkit sounds will overlap with ammo</param>
	/// <param name="fade">Fade out time</param>
	/// <param name="pos">Position</param>

	public void PlayPickupSteal( string name, int channel = 0, Vector3 pos = default, float fade = 0.1f )
	{
		switch ( channel )
		{
			default:
				_pickupHandleA?.Stop( fade ); // cut off previous sound first, as the engine doesnt have voice stealing
				_pickupHandleA = Sound.Play( name );
				if ( IsPossessedLocally ) _pickupHandleA.SpacialBlend = 0;
				else _pickupHandleA.Position = pos; // if someone else picked up an item, play it at the position
				break;
			case 1:
				_pickupHandleB?.Stop( fade ); // cut off previous sound first, as the engine doesnt have voice stealing
				_pickupHandleB = Sound.Play( name );
				if ( IsPossessedLocally ) _pickupHandleB.SpacialBlend = 0;
				else _pickupHandleB.Position = pos; // if someone else picked up an item, play it at the position
				break;
			case 2:
				_pickupHandleC?.Stop( fade ); // cut off previous sound first, as the engine doesnt have voice stealing
				_pickupHandleC = Sound.Play( name );
				if ( IsPossessedLocally ) _pickupHandleC.SpacialBlend = 0;
				else _pickupHandleC.Position = pos; // if someone else picked up an item, play it at the position
				break;
		}
	}

	private SoundHandle _pickupHandleA;
	private SoundHandle _pickupHandleB;
	private SoundHandle _pickupHandleC;
#endif
}
