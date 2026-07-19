using System;

namespace Core;
// this is all of the GetThing() methods
public partial class BaseCombatWeapon
{
	/// <summary>
	/// Clip/Magazine size of primary ammo for this weapon (not currently loaded, but the actual mag size)
	/// </summary>
	/// <returns>The amount</returns>
	public int GetPrimaryCapacity() => WeaponData.PrimaryAmmoCapacity;
	/// <summary>
	/// Clip/Magazine size of secondary ammo for this weapon (not currently loaded, but the actual mag size)
	/// </summary>
	/// <returns></returns>
	public int GetSecondaryCapacity() => WeaponData.SecondaryAmmoCapacity;
	/// <summary>
	/// Default primary quantity (when given for the first time)
	/// </summary>
	/// <returns>The amount</returns>
	public int GetPrimaryDefault() => WeaponData.DefaultPrimaryAmmo;
	/// <summary>
	/// Default secondary quantity (when given for the first time)
	/// </summary>
	/// <returns>The amount</returns>
	public int GetSecondaryDefault() => WeaponData.DefaultSecondaryAmmo;
	/// <summary>
	/// The primary ammo type of this weapon as a string
	/// </summary>
	/// <returns>The name of the ammo resource</returns>
	public string GetPrimaryAmmoType() => WeaponData.PrimaryAmmoType.ResourceName;
	/// <summary>
	/// The secondary ammo type of this weapon as a string
	/// </summary>
	/// <returns>The name of the ammo resource</returns>
	public string GetSecondaryAmmoType() => WeaponData.SecondaryAmmoType.ResourceName;
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
	/// Any ammo at all loaded or not
	/// </summary>
	/// <returns></returns>
	public bool HasUsableAmmo()
	{
		if ( WeaponData.IgnoreAmmo ) return true;

		return HasLoadedAmmo() || (BasePlayer.Local.GetReserveAmmo( WeaponData.PrimaryAmmoType?.ResourceName ) > 0) || (BasePlayer.Local.GetReserveAmmo( WeaponData.SecondaryAmmoType?.ResourceName ) > 0);
	}
	/// <summary>
	/// Does this weapon use primary attack?
	/// </summary>
	/// <returns>Yes/No</returns>
	public bool UsesPrimary() => AmmoResourceHasAnythingPrimary() || (GetPrimaryCapacity() > 0);
	/// <summary>
	/// Does this weapon use secondary attack?
	/// </summary>
	/// <returns>Yes/No</returns>
	public bool UsesSecondary() => AmmoResourceHasAnythingSecondary() || (GetSecondaryCapacity() > 0);
	/// <summary>
	/// Is this weapon a melee?
	/// </summary>
	/// <returns>Yes/No</returns>
	public bool IsMeleeWeapon() => WeaponData.WeaponType == WeaponType.WEAPON_MELEE;
	/// <summary>
	/// Get the fire rate for primary attack
	/// </summary>
	/// <returns>The it</returns>
	public float GetPrimaryFireRate() => 60f / WeaponData.PrimaryFireRateRPM;
	/// <summary>
	/// Get the fire rate for secondary attack
	/// </summary>
	/// <returns>The it</returns>
	public float GetSecondaryFireRate() => 60f / WeaponData.SecondaryFireRateRPM;
	/// <summary>
	/// Get weapon type as string
	/// </summary>
	/// <returns>Weapon type</returns>
	public string GetWeaponType() => WeaponData.WeaponType.ToString();
	/// <summary>
	/// Get weapon type as string
	/// </summary>
	/// <returns>Weapon type</returns>
	public string GetWeaponCrosshairType() => WeaponData.WeaponType.ToString();
	/// <summary>
	/// Does Primary Ammo property contain anything?
	/// </summary>
	/// <returns>Yes/No</returns>
	public bool AmmoResourceHasAnythingPrimary() => WeaponData.PrimaryAmmoType.IsValid();
	/// <summary>
	/// Does Secondary Ammo property contain anything?
	/// </summary>
	/// <returns>Yes/No</returns>
	public bool AmmoResourceHasAnythingSecondary() => WeaponData.SecondaryAmmoType.IsValid();

	/// <summary>
	/// A quick way to get a value out of custom data
	/// </summary>
	/// <param name="key">Parameter to find</param>
	/// <returns>Associated float (or int)</returns>
	protected float GetCustomDataFloat( string key )
	{
		if ( WeaponData.CustomDataFloat is null ) return 0;

		if ( !WeaponData.CustomDataFloat.TryGetValue( key, out float output ) )
		{
			Log.Warning( key + " key could not be found, output will be 0" );
			output = 0;
		}

		return output;
	}
	protected string GetCustomDataString( string key )
	{
		if ( WeaponData.CustomDataString is null ) return string.Empty;

		if ( !WeaponData.CustomDataString.TryGetValue( key, out string output ) )
		{
			Log.Warning( key + " key could not be found, output will be string.Empty" );
			output = string.Empty;
		}

		return output;
	}

	/// <summary>
	/// Process the correct DamageInfo for the type of attack we are having
	/// </summary>
	/// <param name="isPrimary">Is this PrimaryAttack() or SecondaryAttack()</param>
	/// <param name="isPlayer">Is this attack by a Player or an NPC</param>
	/// <returns>DamageInfo with correct values applied</returns>
	public CoreDamageInfo GetDamageInfo( bool isPrimary, bool isPlayer )
	{
		return new CoreDamageInfo()
		{
			Attacker = GameObject.Root,
			Weapon = GameObject,
			Damage = isPlayer ? (isPrimary ? AmmoInfo.GetAmmoData( GetPrimaryAmmoType() ) : AmmoInfo.GetAmmoData( GetSecondaryAmmoType() )).DamagePlayer
			: (isPrimary ? AmmoInfo.GetAmmoData( GetPrimaryAmmoType() ) : AmmoInfo.GetAmmoData( GetSecondaryAmmoType() )).DamageNPC,
			Tags = { "bullet" },
			Ammo = isPrimary ? AmmoInfo.GetAmmoData( GetPrimaryAmmoType() ) : AmmoInfo.GetAmmoData( GetSecondaryAmmoType() ),
			BaseCombatWeapon = this,
			Position = GameObject.Root.WorldPosition
		};
	}
	/// <summary>
	/// Get the spread for a single bullet based on WeaponData and SpreadType
	/// </summary>
	/// <param name="isPrimary">Is this a primary attack?</param>
	/// <param name="shotsFired">Cumulative bullets fired</param>
	/// <param name="bulletIndex">Index of this bullet</param>
	/// <param name="bulletCount">Total number of bullets fired (e.g. shotgun pellet count)</param>
	/// <returns>Spread in degrees</returns>
	protected virtual float GetSpreadForBullet( bool isPrimary, int shotsFired, int bulletIndex = 0, int bulletCount = 1 )
	{
		var baseSpread = isPrimary
			? WeaponData.SpreadDegreesPrimary
			: WeaponData.SpreadDegreesSecondary;

		if ( WeaponData.SpreadType == SpreadType.SPREAD_DYNAMIC )
		{
			float t = WeaponData.DynamicSpreadType switch
			{
				DynamicSpreadType.PER_BULLET_FIRED => (float)shotsFired / WeaponData.PrimaryAmmoCapacity,
				DynamicSpreadType.PER_CONSECUTIVE_SHOT =>
					Math.Clamp( (float)_shotsFired / WeaponData.PrimaryAmmoCapacity
						+ (bulletCount > 1 ? (float)bulletIndex / (bulletCount - 1) : 0f)
						* ((float)_shotsFired / WeaponData.PrimaryAmmoCapacity), 0f, 1f ),
				_ => 0f
			};

			t = Math.Clamp( t, 0f, 1f );
			baseSpread *= WeaponData.SpreadProgressionCurve.Evaluate( t );
		}

		return baseSpread * WeaponData.SpreadScale;
	}
}
