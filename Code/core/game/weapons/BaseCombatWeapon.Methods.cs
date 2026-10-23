using System;

namespace Core;
// this is all of the GetThing() methods
public partial class BaseCombatWeapon
{
	/// <summary>Viewmodel model resource, by default points to weapon resource, override to decide with game logic</summary>
	/// <returns>Model reference</returns>
	public virtual Model GetViewmodel() => WeaponData?.WeaponViewmodel;
	/// <summary>Worldmodel model resource, by default points to weapon resource, override to decide with game logic</summary>
	/// <returns>Model reference</returns>
	public virtual Model GetWorldmodel() => WeaponData?.WeaponWorldmodel;

	/// <summary>Clip/Magazine size of primary ammo for this weapon (not currently loaded, but the actual mag size)</summary>
	/// <returns>The amount</returns>
	public int GetPrimaryCapacity() => WeaponData.PrimaryAmmoCapacity;
	/// <summary>Clip/Magazine size of secondary ammo for this weapon (not currently loaded, but the actual mag size)</summary>
	/// <returns></returns>
	public int GetSecondaryCapacity() => WeaponData.SecondaryAmmoCapacity;
	/// <summary>Default primary quantity (when given for the first time)</summary>
	/// <returns>The amount</returns>
	public int GetPrimaryDefault() => WeaponData.DefaultPrimaryAmmo;
	/// <summary>Default secondary quantity (when given for the first time)</summary>
	/// <returns>The amount</returns>
	public int GetSecondaryDefault() => WeaponData.DefaultSecondaryAmmo;
	/// <summary>The primary ammo type of this weapon as a string</summary>
	/// <returns>The name of the ammo resource</returns>
	public string GetPrimaryAmmoType() => WeaponData.PrimaryAmmoType.ResourceName;
	/// <summary>The secondary ammo type of this weapon as a string</summary>
	/// <returns>The name of the ammo resource</returns>
	public string GetSecondaryAmmoType() => WeaponData.SecondaryAmmoType.ResourceName;
	/// <summary>Return true if this weapon has some ammo</summary>
	public bool HasLoadedAmmo()
	{
		// Weapons with no ammo types can always be selected
		if ( GetPrimaryCapacity() <= 0 && GetSecondaryCapacity() <= 0 )
			return true;

		return (PrimaryAmmoLoaded > 0) || (SecondaryAmmoLoaded > 0);
	}
	/// <summary>Any ammo at all loaded or not</summary>
	/// <returns></returns>
	public virtual bool HasUsableAmmo()
	{
		if ( WeaponData.IgnoreAmmo ) return true;

		return HasLoadedAmmo() || (Owner.Player.GetReserveAmmo( WeaponData.PrimaryAmmoType?.ResourceName ) > 0) || (Owner.Player.GetReserveAmmo( WeaponData.SecondaryAmmoType?.ResourceName ) > 0);
	}
	/// <summary>Does this weapon use primary attack?</summary>
	/// <returns>Yes/No</returns>
	public bool UsesPrimary() => IsMeleeWeapon() || AmmoResourceHasAnythingPrimary() || (GetPrimaryCapacity() > 0);
	/// <summary>Does this weapon use secondary attack?</summary>
	/// <returns>Yes/No</returns>
	public bool UsesSecondary() => AmmoResourceHasAnythingSecondary() || (GetSecondaryCapacity() > 0);
	/// <summary>Is this weapon a melee?</summary>
	/// <returns>Yes/No</returns>
	public virtual bool IsMeleeWeapon() => WeaponData.WeaponType == WeaponType.WEAPON_MELEE;
	/// <summary>Get the fire rate for primary attack</summary>
	/// <returns>The it</returns>
	public virtual float GetPrimaryFireRate() => 60f / WeaponData.PrimaryFireRateRPM;
	/// <summary>Get the fire rate for secondary attack</summary>
	/// <returns>The it</returns>
	public virtual float GetSecondaryFireRate() => 60f / WeaponData.SecondaryFireRateRPM;
	/// <summary>Get weapon type as string</summary>
	/// <returns>Weapon type</returns>
	public string GetWeaponType() => WeaponData.WeaponType.ToString();
	/// <summary>Get weapon type as string</summary>
	/// <returns>Weapon type</returns>
	public string GetWeaponCrosshairType() => WeaponData.WeaponType.ToString();
	/// <summary>Does Primary Ammo property contain anything?</summary>
	/// <returns>Yes/No</returns>
	public bool AmmoResourceHasAnythingPrimary() => WeaponData.PrimaryAmmoType.IsValid();
	/// <summary>Does Secondary Ammo property contain anything?</summary>
	/// <returns>Yes/No</returns>
	public bool AmmoResourceHasAnythingSecondary() => WeaponData.SecondaryAmmoType.IsValid();
	/// <summary>Is this weapon currently showing a first person perspective or we are seeing this from a third person perspective (camera view, other player, NPC)</summary>
	/// <returns>Showing first person perspective yes/no</returns>
	public bool IsFirstperson() => Owner.Player.IsValid() && Owner.Player.ShouldDrawViewmodel();
	/// <summary>A quick way to get a value out of custom data with all the null checks</summary>
	/// <param name="key">Parameter to find</param>
	/// <param name="empty">Default value if key isn't found</param>
	/// <returns>Associated float (or int)</returns>
	protected float GetCustomDataFloat( string key, float empty = 0f ) => GetCustomDataFloat( WeaponData, key, empty );
	/// <summary>A quick way to get a value out of custom data with all the null checks</summary>
	/// <param name="key">Parameter to find</param>
	/// <param name="empty">Default value if key isn't found</param>
	/// <returns>Associated string</returns>
	protected string GetCustomDataString( string key, string empty = " " ) => GetCustomDataString( WeaponData, key, empty );
	/// <summary>Static option if we need it to get a key out of some other weapon data</summary>
	/// <param name="data">Weapon data to check</param>
	/// <param name="key">Parameter to find</param>
	/// <param name="empty">Default value if key isn't found</param>
	/// <returns>Associated float (or int)</returns>
	public static float GetCustomDataFloat( WeaponParse data, string key, float empty = 0f )
	{
		if ( data.CustomDataFloat is null )
		{
			Log.Warning( $"{data}.CustomDataFloat was null, output will be {empty}" );
			return empty;
		}

		if ( !data.CustomDataFloat.TryGetValue( key, out float output ) )
		{
			Log.Warning( $"{key} could not be found, output will be {empty}" );
			output = empty;
		}

		return output;
	}
	/// <summary>Static option if we need it to get a key out of some other weapon data</summary>
	/// <param name="data">Weapon data to check</param>
	/// <param name="key">Parameter to find</param>
	/// <param name="empty">Default value if key isn't found</param>
	/// <returns>Associated string</returns>
	public static string GetCustomDataString( WeaponParse data, string key, string empty = " " )
	{
		if ( data.CustomDataString is null )
		{
			Log.Warning( $"{data}.CustomDataString was null, output will be {empty}" );
			return empty;
		}

		if ( !data.CustomDataString.TryGetValue( key, out string output ) )
		{
			Log.Warning( $"{key} could not be found, output will be {empty}" );
			output = empty;
		}

		return output;
	}

	/// <summary>Process the correct DamageInfo for the type of attack we are having</summary>
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
			Tags = { DamageTypes.DMG_BULLET },
			Ammo = isPrimary ? AmmoInfo.GetAmmoData( GetPrimaryAmmoType() ) : AmmoInfo.GetAmmoData( GetSecondaryAmmoType() ),
			BaseCombatWeapon = this,
			Position = GameObject.Root.WorldPosition
		};
	}
	/// <summary>Get the spread for a single bullet based on WeaponData and SpreadType</summary>
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
