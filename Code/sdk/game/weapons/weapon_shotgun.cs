// dont put these into a namespace it will fuck the weapon giving and youll have to do give sdk.weapon_smg and thats ass
using Core;
public class weapon_shotgun : Core.BaseCombatWeapon
{
	// an example weapon

	// if calling base. first then its doing a weird thing so we have to copy the whole function inline and then add our check at the end
	protected override bool AttackConditions( bool primary = true )
	{
		// is not local player
		if ( (Owner.Player.IsValid() && !Owner.Player.IsControlledLocally) || IsProxy ) return false;

		if ( Owner.Player?.LifeState == LifeState.Dead ) return false;

		if ( Owner.Player.SelectionOpen ) return false;

		if ( _disallowAttack ) return false;

		if ( Owner.Player.Controller.IsRunning ) return false; // cant shoot when sprinting (aka in this example case, regular non-Shift movement)

		return true;
	}

	protected override void HandleWeaponInput()
	{
		if ( Input.Released( "attack1" ) ) Owner.Player?.SetAllAnimgraphParams( "b_attack", false ); // FP guns have a different idea of how a shotgun should work
	}

	protected override void PrimaryAttack()
	{
		base.PrimaryAttack();

		Owner.Player?.SetAllAnimgraphParams( "b_attack", true ); // FP guns have a different naming scheme than us
	}

	/// <summary>
	/// Draw the weapon.
	/// Needs to be public because other stuff can force us to draw
	/// </summary>
	public override void Draw( BasePlayer.HolsterType type = BasePlayer.HolsterType.Weapon, bool force = false )
	{
		base.Draw();
		Owner.Player?.SetAllAnimgraphParams( "b_holster", false );
	}
	/// <summary>
	/// Holster the weapon.
	/// Needs to be public because other stuff can force us to put down the weapon
	/// </summary>
	public override void Holster( BasePlayer.HolsterType type = BasePlayer.HolsterType.Weapon, bool force = false )
	{
		base.Holster();
		Owner.Player?.SetAllAnimgraphParams( "b_holster", true );
	}
}
