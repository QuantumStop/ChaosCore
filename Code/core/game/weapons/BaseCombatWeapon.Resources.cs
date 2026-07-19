using System.Text.Json.Serialization;
#if FMOD
using FMODSbox;
#endif
namespace Core;

//[GameResource( "Weapon Script", "wpn", "A weapon script, like scripts/weapons/weapon_glock.txt", Icon = "plumbing", IconFgColor = "#2b2b17", IconBgColor = "#acac5c" )]
/// <summary>
/// A weapon script, like scripts/weapons/weapon_glock.txt
/// </summary>
[AssetType( Name = "Weapon Script", Extension = "wpn", IconColor = "#acac5c" )]
public class WeaponParse : GameResource
{
	[Order( 0 )] public Model WeaponViewmodel { get; set; }
	[Order( 0 )] public Model WeaponWorldmodel { get; set; }
	[Space]
	[HideIf( nameof( WeaponType ), WeaponType.WEAPON_MELEE ), Order( 0 )] public Model BulletCasingModel { get; set; }
	[Space]
	[HideIf( nameof( WeaponType ), WeaponType.WEAPON_MELEE ), Order( 0 )] public PrefabFile BulletCasingParticle { get; set; }
	[Space]
	[Category( "Features" ), Order( 2 ), ShowIf( nameof( WeaponType ), WeaponType.WEAPON_EXPLOSIVE )] public PrefabFile ExplosionParticle { get; set; }

	[Space]
	[Order( 0 )] public WeaponType WeaponType { get; set; }

	[Order( 0 )] public CrosshairData WeaponCrosshair { get; set; } = null;

	[Header( "Reloading" )]
	[Category( "Features" ), Order( 2 ), ShowIf( nameof( CanStageReload ), true ), Range( 1, 6 ), Step( 1 ), Description( "How many stages, starting from 1" )] public int StageAmount { get; set; } = 3;

	[Header( "Firing" )]
	[Category( "Features" ), Order( 2 ), HideIf( nameof( WeaponType ), WeaponType.WEAPON_MELEE )] public bool FiresUnderwater { get; set; }
	[Category( "Features" ), Order( 2 ), HideIf( nameof( WeaponType ), WeaponType.WEAPON_MELEE )] public bool ReloadsSingly { get; set; }
	[Category( "Features" ), Order( 2 ), Header( "Muzzle Light" ), ShowIf( nameof( HasBullets ), true )] public Texture MuzzleLightCookie { get; set; } = Texture.Load( "materials/cookies/muzzle_light01.tif" );
	[Category( "Features" ), Order( 2 ), ShowIf( nameof( HasBullets ), true ), Range( 0.01f, 0.1f ), Step( 0.01f )] public float MuzzleLightTime { get; set; } = 0.02f;
	[Category( "Features" ), Order( 2 ), ShowIf( nameof( HasBullets ), true ), Range( 1.0f, 90.0f ), Step( 1 )] public float MuzzleLightFOV { get; set; } = 60f;

	[Header( "Muzzle Effects" )]
	[Category( "Features" ), Order( 2 ), ShowIf( nameof( HasBullets ), true )] public PrefabFile MuzzleFlashEffect { get; set; }
	[Category( "Features" ), Order( 2 ), ShowIf( nameof( HasBullets ), true )] public PrefabFile TracerEffect { get; set; }


	/// <summary> Min/Max of how much the recoil grows over time (if at all). </summary>
	[Header( "Visual" ), Category( "Recoil" ), Order( 2 )]
	[Property]
	public Curve RecoilStrengthCurve { get; set; } = new Curve(
	new[]
	{
		new Curve.Frame( 0f, 0f ),
		new Curve.Frame( 1f, 1f )
	}
	);

	[Category( "Explosive" ), Order( 3 ), ShowIf( nameof( WeaponType ), WeaponType.WEAPON_EXPLOSIVE )]
	public ExplosionType ExplosionType { get; set; } = ExplosionType.Trace;

	[Category( "Explosive" ), Order( 3 ), ShowIf( nameof( WeaponType ), WeaponType.WEAPON_EXPLOSIVE ),
	 Range( 0f, 1024f, clamped: true ), Step( 1 )]
	public float ExplosionRadius { get; set; } = 180f;

	[Category( "Explosive" ), Order( 3 ), ShowIf( nameof( WeaponType ), WeaponType.WEAPON_EXPLOSIVE ),
	 Range( 0f, 500f ), Step( 1 )]
	public float ExplosionDamage { get; set; } = 100f;

	[Category( "Explosive" ), Order( 3 ), ShowIf( nameof( WeaponType ), WeaponType.WEAPON_EXPLOSIVE ),
	 Range( 0.01f, 1.0f ), Step( 0.01f )]
	public float ExplosionDuration { get; set; } = 0.05f;

	/// <summary> Only used when ExplosionType == Volumetric. Performance cap on voxel grid. </summary>
	[Category( "Explosive" ), Order( 3 ),
	 ShowIf( nameof( ExplosionType ), ExplosionType.Volumetric ),
	 Range( 4f, 40f, true ), Step( 1 )]
	public float ExplosionMaxVoxels { get; set; } = 10f;

	/// <summary> Damage falloff from center to edge. </summary>
	[Category( "Explosive" ), Order( 3 ), ShowIf( nameof( WeaponType ), WeaponType.WEAPON_EXPLOSIVE )]
	public Curve ExplosionCurve { get; set; } = Curve.Linear;

	/// <summary> Push force falloff from center to edge. </summary>
	[Category( "Explosive" ), Order( 3 ), ShowIf( nameof( WeaponType ), WeaponType.WEAPON_EXPLOSIVE )]
	public Curve ExplosionPushBack { get; set; } = new Curve( new[]
	{
	new Curve.Frame( 0f, 1f ),
	new Curve.Frame( 1f, 0f )
} );


	/// <summary> Time (in seconds) after which recoil fully resets </summary>
	[Category( "Recoil" ), Order( 2 ), Range( 0, 10 ), Step( 0.05f )] public float RecoilResetThreshold { get; set; } = 2f;


	/// <summary> How much time (in seconds) it takes to get to maximum speed. </summary>
	[Category( "Recoil" ), Order( 2 ), Range( 0, 10 ), Step( 0.05f )] public float RecoilRampSpeed { get; set; } = 2.0f;


	/// <summary> How strong the kickback from recoil is. </summary>
	[Header( "Physical" ), Category( "Recoil" ), Order( 2 ), Range( 0, 5 ), Step( 0.05f )] public float RecoilPushForce { get; set; } = 1f;


	[Header( "Ammo" )]
	[Category( "Primary" ), Order( 3 ), ShowIf( nameof( HasBullets ), true )] public AmmoInfo PrimaryAmmoType { get; set; }

	[ShowIf( nameof( HasPrimaryAmmoType ), true )]
	[Category( "Primary" ), Order( 3 ), Range( 0, 9999, true, false ), Step( 1 )] public int PrimaryAmmoCapacity { get; set; }

	[ShowIf( nameof( HasPrimaryAmmoType ), true )]
	[Category( "Primary" ), Order( 3 ), Range( 0, 9999, true, false ), Step( 1 )] public int DefaultPrimaryAmmo { get; set; }
	[ShowIf( nameof( HasPrimaryAmmoType ), true )]
	[Category( "Primary" ), Order( 3 ), Range( 0, 9999, true, false ), Step( 1 )] public int BulletsPerShot { get; set; } = 1;
	[Header( "Timing" )]

	[ShowIf( nameof( HasPrimaryAmmoType ), true )]
	[Category( "Primary" ), Order( 3 ), Description( "In RPM. Used in 60 / X calculation" ), Title( "Primary Fire Rate" ), Range( 1, 2000, true, false ), Step( 1 )] public int PrimaryFireRateRPM { get; set; } = 100;
	[Header( "Spread" )]
	[Category( "Primary" ), Order( 3 ), Range( 0, 20 ), Step( 0.1f ), Description( "In Degrees. Used in sin( X / 2 ) calculation" ), Title( "Primary Spread (degrees)" )] public float SpreadDegreesPrimary { get; set; } = 1f;
	[ShowIf( nameof( SpreadType ), SpreadType.SPREAD_DYNAMIC )]
	[Category( "Primary" ), Order( 3 )]
	public Curve SpreadProgressionCurve { get; set; } = new Curve(
	new[]
	{
		new Curve.Frame( 0f, 0f ),
		new Curve.Frame( 1f, 1f )
	}
	);
	[ShowIf( nameof( HasPrimaryAmmoType ), true )]
	[Category( "Primary" ), Order( 3 ), Description( "Method for how we do spread" )] public SpreadType SpreadType { get; set; } = SpreadType.SPREAD_STATIC;
	[ShowIf( nameof( SpreadType ), SpreadType.SPREAD_DYNAMIC )]
	[Category( "Primary" ), Order( 3 )] public DynamicSpreadType DynamicSpreadType { get; set; } = DynamicSpreadType.PER_CONSECUTIVE_SHOT;
	[ShowIf( nameof( HasPrimaryAmmoType ), true )]
	[Category( "Primary" ), Order( 3 ), Description( "Spread the scale if degrees are not enough" ), Range( 1, 10 )] public float SpreadScale { get; set; } = 1f;
#if FMOD
	[Header( "Sound" )]
	[ShowIf( nameof( HasPrimaryAmmoType ), true )]
	[Category( "Primary" ), Order( 3 )] public FMODEventResource AttackSoundPrimary { get; set; }
	[Category( "Primary" ), Order( 3 )] public bool WantNearEmptySound { get; set; }
#else
	[Header( "Sound" )]
	[ShowIf( nameof( HasPrimaryAmmoType ), true )]
	[Category( "Primary" ), Order( 3 )] public List<SoundEvent> AttackSoundsPrimary { get; set; } = [];
#endif
	[Header( "Ammo" )]
	[Category( "Secondary" ), Order( 4 ), ShowIf( nameof( HasBullets ), true )] public AmmoInfo SecondaryAmmoType { get; set; }

	[ShowIf( nameof( HasSecondaryAmmoType ), true )]
	[Category( "Secondary" ), Order( 4 )] public int SecondaryAmmoCapacity { get; set; }

	[ShowIf( nameof( HasSecondaryAmmoType ), true )]
	[Category( "Secondary" ), Order( 4 )] public int DefaultSecondaryAmmo { get; set; }
	[Header( "Timing" )]

	[ShowIf( nameof( HasSecondaryAmmoType ), true )]
	[Category( "Secondary" ), Order( 4 ), Description( "In RPM. Used in 60 / X calculation" ), Title( "Secondary Fire Rate" ), Range( 1, 2000, true, false ), Step( 1 )] public int SecondaryFireRateRPM { get; set; } = 1;
	[Header( "Spread" )]
	[ShowIf( nameof( HasSecondaryAmmoType ), true )]
	[Category( "Secondary" ), Order( 4 ), Range( 0, 20 ), Step( 0.1f ), Description( "In Degrees. Used in sin( X / 2 ) calculation" ), Title( "Secondary Spread (degrees)" )] public float SpreadDegreesSecondary { get; set; } = 1f;
#if FMOD
	[Header( "Sound" )]
	[ShowIf( nameof( HasSecondaryAmmoType ), true )]
	[Category( "Secondary" ), Order( 3 )] public FMODEventResource AttackSoundSecondary { get; set; }
#else
	[Header( "Sound" )]
	[ShowIf( nameof( HasSecondaryAmmoType ), true )]
	[Category( "Primary" ), Order( 3 )] public List<SoundEvent> AttackSoundsSecondary { get; set; } = [];
#endif

	[Category( "Inventory" ), Order( 5 ), Title( "Bucket (X)" ), Range( 0, 7 ), Step( 1 )] public int Bucket { get; set; }
	[Category( "Inventory" ), Order( 5 ), Title( "Bucket Position (Y)" ), Range( 0, 7 ), Step( 1 )] public int Position { get; set; }
	[Category( "Inventory" ), Order( 5 ), Range( 0, 12 ), Step( 1 )] public int Weight { get; set; } = 0;
	[Category( "Inventory" ), Order( 5 ), FilePath] public string Icon { get; set; }
	[Category( "Inventory" ), Order( 5 ), Title( "Print Name" )] public string Name { get; set; }

	[Category( "NPC" ), Order( 6 )] public WeaponEquipSlot EquipSlot { get; set; }

	/// <summary>
	/// This is used to pass custom data that we would otherwise have to define and support in resource manually, which might be pain.
	/// <para>Float should cover most use cases, as the majority of properties are ints/floats anyway (just round and cast to int).</para>
	/// </summary>
	[Category( "Custom" ), Order( 7 ), Title( "Custom Data (float)" )] public Dictionary<string, float> CustomDataFloat { get; set; }
	[Category( "Custom" ), Order( 7 ), Title( "Custom Data (string)" )] public Dictionary<string, string> CustomDataString { get; set; }
	[Category( "Custom" ), Order( 7 )] public bool IgnoreAmmo { get; set; } = false;

	// This will let you hide features based on having certain ammo type. Doesn't work directly within ShowIf/HideIf otherwise
	[Hide] public bool HasPrimaryAmmoType => PrimaryAmmoType.IsValid();
	[Hide] public bool HasSecondaryAmmoType => SecondaryAmmoType.IsValid();
	[Hide] private bool HasBullets => WeaponType != WeaponType.WEAPON_MELEE && WeaponType != WeaponType.WEAPON_EXPLOSIVE;
	[Hide] private bool CanStageReload => (WeaponType != WeaponType.WEAPON_MELEE) && (WeaponType != WeaponType.WEAPON_SHOTGUN) && (WeaponType != WeaponType.WEAPON_EXPLOSIVE);

	public static WeaponParse GetWeaponData( string weaponname ) => ResourceLibrary.Get<WeaponParse>( "scripts/weapons/" + weaponname + ".wpn" );
	protected override Bitmap CreateAssetTypeIcon( int width, int height ) => CreateSimpleAssetTypeIcon( "plumbing", width, height, "#acac5c", "#2b2b17" );
}

//[GameResource( "Ammo Data", "amn", "Ammunition file\r\n", Icon = "create", IconFgColor = "#2b2b17", IconBgColor = "#acac5c" )]
[AssetType( Name = "Ammo Data", Extension = "amn", IconColor = "#acac5c" )]
public class AmmoInfo : GameResource
{
	[Hide, JsonIgnore] new public bool IsValid { get; set; } = false;
	/// <summary> Model for this ammo (regular amount) </summary>
	[Category( "Visual" )] public Model AmmoModel { get; set; }
	/// <summary> Model for this ammo (bigger amount) </summary>
	[Category( "Visual" )] public Model AmmoModelLarge { get; set; }
	/// <summary> Name for this ammo </summary>
	[Category( "Visual" )] public string AmmoName { get; set; }
	/// <summary> HUD icon of this ammo </summary>
	[Category( "Visual" )] public string HudSymbol { get; set; }
	/// <summary> Multiplier for the decal size </summary>
	[Category( "Visual" ), Range( 0, 1 ), Step( 1 )] public float HoleSize { get; set; } = 0.25f;
	/// <summary> Damage from Player using this bullet</summary>
	[Category( "Player" ), Title( "Damage" )] public int DamagePlayer { get; set; }
	/// <summary> Max carry for this bullet </summary>
	[Category( "Player" )] public int MaxAmmo { get; set; }
	/// <summary> Most common amount for this ammo type </summary>
	[Category( "Player" )] public int DefaultAmmo { get; set; }
	/// <summary> Damage from NPC using this bullet</summary>
	[Category( "NPC" ), Title( "Damage" ), Order( 10 )] public int DamageNPC { get; set; }
	/// <summary> Amount of grains per one bullet, used in push force calculation </summary>
	[Category( "Physics" ), Order( 5 )] public int Grains { get; set; } = 171;
	/// <summary> Speed of the bullet in 12 hammer units/s, used in push force calculation</summary>
	[Category( "Physics" ), Order( 5 ), Title( "Ft Per Second" )] public int FtPerSec { get; set; } = 1000;

	protected override Bitmap CreateAssetTypeIcon( int width, int height ) => CreateSimpleAssetTypeIcon( "create", width, height, "#acac5c", "#2b2b17" );

	public static AmmoInfo GetAmmoData( string type )
	{
		var info = ResourceLibrary.Get<AmmoInfo>( "scripts/ammo/" + type + ".amn" );

		info?.IsValid = true; // ?

		return info;
	}
}

public enum WeaponType
{
	[Description( "An automatic or semi-automatic weapon" )]
	WEAPON_GENERIC,
	[Description( "A shotgun" )]
	WEAPON_SHOTGUN,
	[Description( "A melee weapon" )]
	WEAPON_MELEE,
	[Description( "An explosive weapon" )]
	WEAPON_EXPLOSIVE
};

public enum SpreadType
{
	[Description( "No spread scaling" ), Icon( "gps_fixed" ), Title( "Static" )]
	SPREAD_STATIC = 0,

	[Description( "Progressive spread per bullet" ), Icon( "scatter_plot" ), Title( "Progressive" )]
	SPREAD_DYNAMIC
}

public enum DynamicSpreadType
{
	[Description( "Spread scales with bullets fired from magazine" ), Icon( "bar_chart" ), Title( "Per Bullet" )]
	PER_BULLET_FIRED = 0,

	[Description( "Spread scales with consecutive shots" ), Icon( "trending_up" ), Title( "Per Shot" )]
	PER_CONSECUTIVE_SHOT = 1
}

public enum ExplosionType
{
	[Description( "Sphere overlap: fast, no occlusion" )]
	Trace,
	[Description( "Voxel wave: occluded by geometry, more expensive" )]
	Volumetric
}

public enum WeaponCrosshairType
{
	[Description( "No weapon is present, fall back to default crosshair" )]
	None,
	[Description( "A simple dot" )]
	CROSSHAIR_DOT,
	[Description( "A cross variant" )]
	CROSSHAIR_CROSS_A,
	[Description( "A cross variant" )]
	CROSSHAIR_CROSS_B,
	[Description( "A cross with additional features. Like for a secondary firing mode, gadget or alike" )]
	CROSSHAIR_CROSS_HYBRID,
	[Description( "A circle" )]
	CROSSHAIR_CIRCLE

};
