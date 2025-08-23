namespace Core;
// this is all of the GetThing() methods
public partial class BaseCombatWeapon
{
	/// <summary>
	/// Clip/Magazine size of primary ammo for this weapon (not currently loaded, but the actual mag size)
	/// </summary>
	/// <returns>The amount</returns>
	public int GetPrimaryCapacity()
	{
		return WeaponData.PrimaryAmmoCapacity;
	}
	/// <summary>
	/// Clip/Magazine size of secondary ammo for this weapon (not currently loaded, but the actual mag size)
	/// </summary>
	/// <returns></returns>
	public int GetSecondaryCapacity()
	{
		return WeaponData.SecondaryAmmoCapacity;
	}
	/// <summary>
	/// Default primary quantity (when given for the first time)
	/// </summary>
	/// <returns>The amount</returns>
	public int GetPrimaryDefault()
	{
		return WeaponData.DefaultPrimaryAmmo;
	}
	/// <summary>
	/// Default secondary quantity (when given for the first time)
	/// </summary>
	/// <returns>The amount</returns>
	public int GetSecondaryDefault()
	{
		return WeaponData.DefaultSecondaryAmmo;
	}
	/// <summary>
	/// The primary ammo type of this weapon as a string
	/// </summary>
	/// <returns>The name of the ammo resource</returns>
	public string GetPrimaryAmmoType()
	{
		return WeaponData.PrimaryAmmoType.ResourceName;
	}
	/// <summary>
	/// The secondary ammo type of this weapon as a string
	/// </summary>
	/// <returns>The name of the ammo resource</returns>
	public string GetSecondaryAmmoType()
	{
		return WeaponData.SecondaryAmmoType.ResourceName;
	}
	/// <summary>
	/// Return true if this weapon has some ammo
	/// </summary>
	public bool HasLoadedAmmo()
	{
		// Weapons with no ammo types can always be selected
		if ( GetPrimaryCapacity() <= 0 && GetSecondaryCapacity() <= 0 )
			return true;

		return (PrimaryAmmoLoaded > 0) || (SecondaryAmmoLoaded > 0);
	}
	/// <summary>
	/// Does this weapon use primary attack?
	/// </summary>
	/// <returns>Yes/No</returns>
	public bool UsesPrimary()
	{
		return AmmoResourceHasAnythingPrimary() || (GetPrimaryCapacity() > 0);
	}
	/// <summary>
	/// Does this weapon use secondary attack?
	/// </summary>
	/// <returns>Yes/No</returns>
	public bool UsesSecondary()
	{
		return AmmoResourceHasAnythingSecondary() || (GetSecondaryCapacity() > 0);
	}
	/// <summary>
	/// Is this weapon a melee?
	/// </summary>
	/// <returns>Yes/No</returns>
	public bool IsMeleeWeapon()
	{
		if ( WeaponData.WeaponType == WeaponType.WEAPON_MELEE )
			return true;

		return false;
	}
	/// <summary>
	/// Get the fire rate for primary attack
	/// </summary>
	/// <returns>The it</returns>
	public float GetPrimaryFireRate()
	{
		return 60f / WeaponData.PrimaryFireRateRPM;
	}
	/// <summary>
	/// Get the fire rate for secondary attack
	/// </summary>
	/// <returns>The it</returns>
	public float GetSecondaryFireRate()
	{
		return 60f / WeaponData.SecondaryFireRateRPM;
	}
	/// <summary>
	/// Get weapon type as string
	/// </summary>
	/// <returns>Weapon type</returns>
	public string GetWeaponType()
	{
		return WeaponData.WeaponType.ToString();
	}
	/// <summary>
	/// Get weapon type as string
	/// </summary>
	/// <returns>Weapon type</returns>
	public string GetWeaponCrosshairType()
	{
		return WeaponData.WeaponType.ToString();
	}
	/// <summary>
	/// Does Primary Ammo property contain anything?
	/// </summary>
	/// <returns>Yes/No</returns>
	public bool AmmoResourceHasAnythingPrimary()
	{
		return WeaponData.PrimaryAmmoType.IsValid();
	}
	/// <summary>
	/// Does Secondary Ammo property contain anything?
	/// </summary>
	/// <returns>Yes/No</returns>
	public bool AmmoResourceHasAnythingSecondary()
	{
		return WeaponData.SecondaryAmmoType.IsValid();
	}
}
