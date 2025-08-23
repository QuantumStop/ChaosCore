public class CoreDamageInfo : DamageInfo
{
	/// <summary>
	/// Physical force of the damage, usually applied as a push
	/// </summary>
	public Vector3 Force { get; set; }
	public Vector3 ReportedPosition { get; set; }
	/// <summary>
	/// The damage amount before difficulty level adjustments are made, used to get uniform damage forces
	/// </summary>
	public float BaseDamage { get; set; }
	/// <summary>
	/// The weapon or rocket (or player) that is dealing the damage.
	/// For hitscan weapons, the Weapon will be the same as the Inflictor.
	/// For projectile weapons, the projectile is the Inflictior, and this contains the weapon that created the projectile
	/// </summary>
	public GameObject Inflictor { get; set; }
	/// <summary>
	/// What ammo was used to damage
	/// </summary>
	public AmmoInfo Ammo { get; set; }

	/// <summary>
	/// The actual weapon class
	/// </summary>
	public BaseCombatWeapon BaseCombatWeapon { get; set; }

	// a bunch of overloads to pass shit to the class

	public CoreDamageInfo()
	{
		Force = Vector3.Zero;
		Position = Vector3.Zero;
		ReportedPosition = Vector3.Zero;
		Damage = 0f;
		BaseDamage = 0f;
	}

	public CoreDamageInfo( GameObject inflictor, GameObject attacker, float damage )
	{
		Force = Vector3.Zero;
		Position = Vector3.Zero;
		ReportedPosition = Vector3.Zero;
		Inflictor = inflictor;
		Attacker = attacker;
		Damage = damage;
	}
	public CoreDamageInfo( GameObject inflictor, GameObject attacker, GameObject weapon, float damage )
	{
		Force = Vector3.Zero;
		Position = Vector3.Zero;
		ReportedPosition = Vector3.Zero;
		Inflictor = inflictor;
		Attacker = attacker;
		Weapon = weapon;
		Damage = damage;
	}
	public CoreDamageInfo( GameObject inflictor, GameObject attacker, Vector3 damageForce, Vector3 damagePosition, float damage, Vector3 reportedPosition = new Vector3() )
	{
		Force = damageForce;
		Position = damagePosition;
		ReportedPosition = reportedPosition;
		Inflictor = inflictor;
		Attacker = attacker;
		Damage = damage;
	}

	public CoreDamageInfo( GameObject inflictor, GameObject attacker, Vector3 damageForce, Vector3 damagePosition, float damage, AmmoInfo ammo, Vector3 reportedPosition = new Vector3() )
	{
		Force = damageForce;
		Position = damagePosition;
		ReportedPosition = reportedPosition;
		Inflictor = inflictor;
		Attacker = attacker;
		Damage = damage;
		Ammo = ammo;
	}
}
