public class weapon_glock : BaseCombatWeapon
{
	protected override bool ReloadsSingly => WeaponData.ReloadsSingly;
	private float _SoonestPrimaryAttack { get; set; }
	private float _SoonestSecondaryAttack { get; set; }
	private float _refirePrimaryTime => 0.1f;
	private float _refireSecondaryTime => 0.1f;

	protected override void OnValidate()
	{
		_SoonestPrimaryAttack = Time.Now;
		_SoonestSecondaryAttack = Time.Now;
	}
	protected override void HandleWeaponInput()
	{
		base.HandleWeaponInput();

		if ( !Input.Down( "attack1" ) && (_SoonestPrimaryAttack < Time.Now) )
		{
			_nextPrimaryAttack = Time.Now - 0.1f;
			_nextSecondaryAttack = Time.Now - 0.1f;
		}
		/*
					if ( !Input.Down( "attack2" ) && (_SoonestSecondaryAttack < Time.Now) )
					{
						_nextPrimaryAttack = Time.Now - 0.1f;
						_nextSecondaryAttack = Time.Now - 0.1f;
					}
		*/
	}

	protected override void PrimaryAttack()
	{
		base.PrimaryAttack();
		_SoonestPrimaryAttack = Time.Now + _refirePrimaryTime;
	}
}
