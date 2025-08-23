namespace Core;
using System;

public partial class BasePlayer
{
	/// <summary>
	/// Player health
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] public float Health = 100;
	/// <summary>
	/// Player armor
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] public float Armour = 0;
	/// <summary>
	/// Does the player have suit on
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] public virtual bool HasSuit {get;set;} = false;
	/// <summary>
	/// give the suit straight up
	/// </summary>
	[ConCmd( "givesuit" )] private static void GiveSuit() { Local.HasSuit = true; }
	/// <summary>
	/// Remove the suit straight up
	/// </summary>
	[ConCmd( "removesuit" )] private static void RemoveSuit() { Local.HasSuit = false; }
	/// <summary>
	/// Are we alive or are we dead, or a secret third thing
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] public LifeState LifeState = LifeState.Alive;
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
				flArmour *= (1 / bonus);
				flNew = dmginfo.Damage - flArmour;
				Armour = 0;
			}
			else
			{
				Armour -= flArmour;
			}

			dmginfo.Damage = flNew;
		}

		//		TODO: haptic feedback
		//		TODO: OnTakeDamage_Alive
		//		TODO: Reset damage time countdown for each type of time based damage player just sustained

		//		Display any effect associate with this damage type
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
	public void OnDeath( DamageInfo dmginfo )
	{
		Log.Info( "Player State: The player is currently dead" );

		//-- Penultimate enum that says, that our player is in fact dead --//
		this.LifeState = LifeState.Dead;

		// Below we are oding a bunch of sets to make sure we aren't doing 
		// anything in the world, all of it should be self explanatory
		LockPlayer();

		Controller.HeadHeight = 24;

		Controller.Body.Tags.Add( "movement" );
		var bodyRagdoll = Controller.Body.AddComponent<ModelPhysics>();
		bodyRagdoll.Model = Controller.BodyModelRenderer.Model;
		bodyRagdoll.Renderer = Controller.BodyModelRenderer;
		Core.ScreenFlash.Set( Color.Red, 50 );
	}

	/// <summary>
	/// Show damage screen effects
	/// </summary>
	/// <param name="dmginfo">Damage information</param>
	void DamageEffect( DamageInfo dmginfo )
	{
		if ( dmginfo.Tags.Has( "crush" ) )
		{
			Core.ScreenFlash.Set( Color.Red, 1.0f );
		}
		else if ( dmginfo.Tags.Has( "drown" ) )
		{
			//			TODO: we can probably do something more interesting here
			Core.ScreenFlash.Set( Color.Blue, 1.0f );
		}
		else if ( dmginfo.Tags.Has( "slash" ) )
		{
			//			TODO: SpawnBlood(EyePosition(), g_vecAttackDir, BloodColor(), flDamage);
		}
		else if ( dmginfo.Tags.Has( "plasma" ) )
		{
			Core.ScreenFlash.Set( Color.Cyan, 1.0f );
			Sound.Play( "pl_burnpain" );
			//			TODO: Burn sound 
		}
		else if ( dmginfo.Tags.Has( "sonic" ) )
		{
			//			TODO: Sonic damage sound 
		}
		else if ( dmginfo.Tags.Has( "bullet" ) )
		{
			//			TODO: bullet impact sound
			Sound.Play( "pl_pain" );
		}
		else if ( dmginfo.Tags.Has( "shock" ) )
		{
			//			TODO: do zap particle at damage position
		}
		else if ( dmginfo.Tags.Has( "club" ) )
		{
			if ( dmginfo is CoreDamageInfo coreDamageInfo )
			{
				Log.Info( $"Player State: Player got club damage: {dmginfo.Damage}" );
			}
		}
		else if ( dmginfo.Tags.Has( "blast" ) )
		{
			if ( dmginfo is CoreDamageInfo coreDamageInfo )
			{
				Log.Info( $"Player State: Player got blast damage: {dmginfo.Damage}" );
			}
		}
		else if ( dmginfo.Tags.Has( "fall" ) )
		{
			Log.Info( $"Player State: Player got fall damage: {dmginfo.Damage}" );
			Sound.Play( "pl_fallpain" );
		}
	}

	/// <summary>
	/// Fuck the player off
	/// </summary>
	public void LockPlayer()
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

	/// <summary>
	/// Unfuck the player in
	/// </summary>
	public void UnlockPlayer()
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
