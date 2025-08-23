[Title( "Weapon Item" )]
public class BaseWeaponItem : BaseItem
{
	[DebugExpose( group: "BaseWeaponItem", DisplayMember = "ResourcePath" ), Property] public WeaponParse WeaponData { get; set; }
	[DebugExpose( group: "BaseWeaponItem" ), Property] public bool SkipFirstEquipAnim { get; set; } = false;
	[DebugExpose( group: "BaseWeaponItem" ), Property] public bool FillReserveAmmoForWeapon { get; set; } = false;
	public Vector3 PositionImpulse { get; set; }
	public Vector3 AngularImpulse { get; set; }

	[DebugExpose( group: "BaseWeaponItem" ), Property, ReadOnly, Feature( "Debug" )] public int InternalAmmoCountPrimary { get; set; } = 1;


	public BaseWeaponItem()
	{
		WeaponData = WeaponParse.GetWeaponData( GetType().Name );
	}

	public void DecreaseInternalMag( int howmuch )
	{
		if ( InternalAmmoCountPrimary > 0 ) InternalAmmoCountPrimary -= howmuch;
	}

	protected override void OnValidate()
	{
		if ( GetType().Name != "BaseWeaponItem" && (WeaponData == null || GetType().Name != WeaponData.ResourceName) )
			WeaponData = WeaponParse.GetWeaponData( GetType().Name );

		base.OnValidate();
	}

	protected override string GetModel()
	{
		if ( WeaponData != null )
		{
			if ( WeaponData.WeaponWorldmodel == null )
				return "models/dev/error.vmdl";

			return WeaponData.WeaponWorldmodel.ResourcePath;
		}

		return base.GetModel();
	}

	protected override void OnStart()
	{
		base.OnStart();
		Physics?.PhysicsBody.ApplyImpulse( PositionImpulse );
		Physics?.PhysicsBody.ApplyAngularImpulse( AngularImpulse );

		// Log.Info( "Enabled" );

		if ( WeaponData.HasPrimaryAmmoType && !FillReserveAmmoForWeapon )
			InternalAmmoCountPrimary = WeaponData.PrimaryAmmoCapacity;
	}

	public override void OnPickup( BasePlayer Activator = null )
	{

		if ( WeaponData.WeaponViewmodel == null )   // no viewmodel? goodbye!
			return;

		var weapon = BasePlayer.Local.GiveWeaponByName( WeaponData.ResourceName, null, false, this );

		if ( !weapon.IsValid() )    // if we are just filling ammo this stops it
			return;

		base.OnPickup( Activator ); // putting this here is easier than to figure out the pickup check override

		if ( SkipFirstEquipAnim )
			weapon.FirstEquip = false;

		if ( FillReserveAmmoForWeapon )
			BasePlayer.Local.AddReserveAmmo( weapon.WeaponData.PrimaryAmmoType.ResourceName, 10000 );

		BasePlayer.Local.SwitchToWeapon( weapon );
		DestroyItem();
	}
}
