namespace Core;

public partial class BaseCombatWeapon
{
	[ConVar( "debug_animevents", Help = "Print when all of the animgraph events happen" )] public static bool DebugAnimEvents { get; set; }

	/// <summary>Cannot shoot</summary>
	/// <param name="disallow">True = not allow; False = allow</param>
	public virtual void EventDisallowFiring( bool disallow )
	{
		if ( Owner.Player.CurrentWeapon != this ) return;

		if ( DebugAnimEvents ) Log.Info( "AnimEvent: DisallowFiring (" + disallow + ")" );

		_disallowAttack = disallow;
	}
	/// <summary>Putting the gun back on the screen has finished</summary>
	public virtual void EventDrawFinished()
	{
		if ( Owner.Player.CurrentWeapon != this ) return;

		if ( DebugAnimEvents ) Log.Info( "AnimEvent: DrawFinished" );

		IsHolstered = false;
		ReadyToFire = true;

		if ( FirstEquip ) CurrentReloadStage = ReloadStage.FULL;

		CurrentAction = ActionState.ACTION_IDLE;
		FirstEquip = false;
	}
	/// <summary>Putting the gun away was finished</summary>
	public virtual void EventHolsterFinished()
	{
		if ( Owner.Player.CurrentWeapon != this ) return;

		IsHolstered = true;
		ReadyToFire = false;

		if ( DebugAnimEvents ) Log.Info( "AnimEvent: HolsterFinished" );
	}
	/// <summary>Gun has been fired</summary>
	public virtual void EventPrimaryFire()
	{
		if ( Owner.Player.CurrentWeapon != this ) return;

		if ( DebugAnimEvents ) Log.Info( "AnimEvent: PrimaryFire" );
	}

	/// <summary>The first reload event, starting from here the</summary>
	public virtual void EventMagOut()
	{
		if ( Owner.Player.CurrentWeapon != this )
			return;

		if ( DebugAnimEvents ) Log.Info( "AnimEvent: MagOut" );

		CurrentReloadStage = ReloadStage.STAGE_1; // next would be magin
		MagOut = true;
	}

	/// <summary>Usually the second in order event, magazine is back in</summary>
	public virtual void EventMagIn()
	{
		if ( Owner.Player.CurrentWeapon != this ) return;

		if ( DebugAnimEvents ) Log.Info( "AnimEvent: MagIn" );

		CurrentReloadStage = ReloadStage.STAGE_2; // next would be boltrelease
		MagOut = false;
	}
	/// <summary>Usually the third and last (for full/empty reloads) in order event, bolt is released and you now can shoot</summary>
	public virtual void EventBoltRelease()
	{
		if ( Owner.Player.CurrentWeapon != this ) return;

		if ( DebugAnimEvents ) Log.Info( "AnimEvent: BoltRelease" );

		CurrentReloadStage = ReloadStage.STAGE_3; // this is the last
	}
	/// <summary>Reload has been finished, hooray!</summary>
	public virtual void EventReloadFinished()
	{
		if ( Owner.Player.CurrentWeapon != this ) return;

		if ( DebugAnimEvents ) Log.Info( "AnimEvent: ReloadFinished" );

		if ( _disallowAttack ) EventDisallowFiring( false ); // a slight hack to help with weapons that have fucked event tags in animgraph
	}

	public enum ReloadStage
	{
		FULL,
		STAGE_1,
		STAGE_2,
		STAGE_3,
		STAGE_4,
		STAGE_5
	}

	public enum ActionState
	{
		ACTION_IDLE,
		ACTION_DRAW,
		ACTION_ATTACK,
		ACTION_RELOAD,
		ACTION_HOLSTER,
		ACTION_LOWER
	}
}
