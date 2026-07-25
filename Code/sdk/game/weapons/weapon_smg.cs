// dont put these into a namespace it will fuck the weapon giving
using Core;

public class weapon_smg : Core.BaseCombatWeapon
{
	// an example weapon

	protected override void OnEnabled()
	{
		base.OnEnabled();
		ReadyToFire = true;
	}
}
