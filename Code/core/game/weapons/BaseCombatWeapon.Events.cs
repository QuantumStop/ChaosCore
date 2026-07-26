using System;
namespace Core;

public partial class BaseCombatWeapon
{
	[ConVar( "debug_animevents" )] private static bool _debugAnimEvents { get; set; }

	/// <summary>
	/// Fill the ammo in hand up to mag size using reserve ammo. Perhaps this is better at the end of FinishReload, otherwise a bit of overlap/confusion between the anim clips that have this event.
	/// </summary>
	[Obsolete]
	public virtual void EventSwapMag()
	{
		if ( Owner.Player.CurrentWeapon != this )
			return;

		if ( _debugAnimEvents )
			Log.Info( "AnimEvent: SwapMag" );
	}

	/// <summary>
	/// Cannot shoot
	/// </summary>
	/// <param name="disallow">True = not allow; False = allow</param>
	public virtual void EventDisallowFiring( bool disallow )
	{
		if ( Owner.Player.CurrentWeapon != this )
			return;

		if ( _debugAnimEvents )
			Log.Info( "AnimEvent: DisallowFiring (" + disallow + ")" );

		_disallowAttack = disallow;
	}
	/// <summary>
	/// Putting the gun back on the screen has finished
	/// </summary>
	public virtual void EventDrawFinished()
	{
		if ( Owner.Player.CurrentWeapon != this )
			return;

		if ( _debugAnimEvents )
			Log.Info( "AnimEvent: DrawFinished" );

		IsHolstered = false;
		ReadyToFire = true;

		if ( FirstEquip )
			_currentReloadStage = 0;

		Owner.Player.SetAllAnimgraphParams( "b_first_equip", false );
		FirstEquip = false;
	}
	/// <summary>
	/// Putting the gun away was finished
	/// </summary>
	public virtual void EventHolsterFinished()
	{
		if ( Owner.Player.CurrentWeapon != this )
			return;

		IsHolstered = true;
		ReadyToFire = false;

		if ( _debugAnimEvents )
			Log.Info( "AnimEvent: HolsterFinished" );

	}
	/// <summary>
	/// Gun has been fired
	/// </summary>
	public virtual void EventPrimaryFire()
	{
		if ( Owner.Player.CurrentWeapon != this )
			return;

		if ( _debugAnimEvents )
			Log.Info( "AnimEvent: PrimaryFire" );
	}

	/// <summary>
	/// The first reload event, starting from here the
	/// </summary>
	public virtual void EventMagOut()
	{
		if ( Owner.Player.CurrentWeapon != this )
			return;

		if ( _debugAnimEvents )
			Log.Info( "AnimEvent: MagOut" );

		_currentReloadStage = 1; // next would be magin
		MagOut = true;
	}

	/// <summary>
	/// Usually the second in order event, magazine is back in
	/// </summary>
	public virtual void EventMagIn()
	{
		if ( Owner.Player.CurrentWeapon != this )
			return;

		if ( _debugAnimEvents )
			Log.Info( "AnimEvent: MagIn" );

		_currentReloadStage = 2; // next would be boltrelease
		MagOut = false;
	}
	/// <summary>
	/// Usually the third and last (for full/empty reloads) in order event, bolt is released and you now can shoot
	/// </summary>
	public virtual void EventBoltRelease()
	{
		if ( Owner.Player.CurrentWeapon != this )
			return;

		if ( _debugAnimEvents )
			Log.Info( "AnimEvent: BoltRelease" );

		_currentReloadStage = 3; // this is the last
	}
	/// <summary>
	/// Reload has been finished, hooray!
	/// </summary>
	public virtual void EventReloadFinished()
	{
		if ( Owner.Player.CurrentWeapon != this )
			return;

		if ( _debugAnimEvents )
			Log.Info( "AnimEvent: ReloadFinished" );

		if ( _disallowAttack )
			EventDisallowFiring( false ); // a slight hack to help with weapons that have fucked event tags in animgraph
	}

}
