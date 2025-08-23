using System;
namespace Core;
public partial class BaseCombatWeapon
{
	[ConVar( "debug_animevents" )] static bool DebugAnimEvents { get; set; }

	/// <summary>
	/// Fill the ammo in hand up to mag size using reserve ammo. Perhaps this is better at the end of FinishReload, otherwise a bit of overlap/confusion between the anim clips that have this event.
	/// </summary>
	[Obsolete]
	public virtual void EventSwapMag()
	{
		if ( BasePlayer.Local.CurrentWeapon != this )
			return;

		if ( DebugAnimEvents )
			Log.Info( "AnimEvent: SwapMag" );
	}

	/// <summary>
	/// Cannot shoot
	/// </summary>
	/// <param name="should">Enabled/Disabled</param>
	public virtual void EventDisallowFiring( bool should )
	{
		if ( BasePlayer.Local.CurrentWeapon != this )
			return;

		if ( DebugAnimEvents )
			Log.Info( "AnimEvent: DisallowFiring (" + should + ")" );

		_disallowAttack = should;
	}
	/// <summary>
	/// Putting the gun back on the screen has finished
	/// </summary>
	public virtual void EventDrawFinished()
	{
		if ( BasePlayer.Local.CurrentWeapon != this )
			return;

		if ( DebugAnimEvents )
			Log.Info( "AnimEvent: DrawFinished" );

		if ( FirstEquip )
			_currentReloadStage = 0;

		BasePlayer.Local.SetAllAnimgraphParams( "b_first_equip", false );
		FirstEquip = false;
	}
	/// <summary>
	/// Putting the gun away was finished
	/// </summary>
	public virtual void EventHolsterFinished()
	{
		if ( Equipped )
			return;

		if ( BasePlayer.Local.CurrentWeapon != this )
			return;

		if ( DebugAnimEvents )
			Log.Info( "AnimEvent: HolsterFinished" );

	}
	/// <summary>
	/// Gun has been fired
	/// </summary>
	public virtual void EventPrimaryFire()
	{
		if ( BasePlayer.Local.CurrentWeapon != this )
			return;

		if ( DebugAnimEvents )
			Log.Info( "AnimEvent: PrimaryFire" );
	}

	/// <summary>
	/// The first reload event, starting from here the
	/// </summary>
	public virtual void EventMagOut()
	{
		if ( BasePlayer.Local.CurrentWeapon != this )
			return;

		if ( DebugAnimEvents )
			Log.Info( "AnimEvent: MagOut" );

		_currentReloadStage = 1; // next would be magin
		MagOut = true;
	}

	/// <summary>
	/// Usually the second in order event, magazine is back in
	/// </summary>
	public virtual void EventMagIn()
	{
		if ( BasePlayer.Local.CurrentWeapon != this )
			return;

		if ( DebugAnimEvents )
			Log.Info( "AnimEvent: MagIn" );

		_currentReloadStage = 2; // next would be boltrelease
		MagOut = false;
	}
	/// <summary>
	/// Usually the third and last (for full/empty reloads) in order event, bolt is released and you now can shoot
	/// </summary>
	public virtual void EventBoltRelease()
	{
		if ( BasePlayer.Local.CurrentWeapon != this )
			return;

		if ( DebugAnimEvents )
			Log.Info( "AnimEvent: BoltRelease" );

		_currentReloadStage = 3; // this is the last
	}
	/// <summary>
	/// Reload has been finished, hooray!
	/// </summary>
	public virtual void EventReloadFinished()
	{
		if ( BasePlayer.Local.CurrentWeapon != this )
			return;

		if ( DebugAnimEvents )
			Log.Info( "AnimEvent: ReloadFinished" );
	}

}
