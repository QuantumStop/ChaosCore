namespace Core;

[Title( "Ammo Item" )]
public class BaseAmmoItem : BaseItem
{
	/// <summary>
	/// The ammo resource
	/// </summary>
	[Property] public AmmoInfo AmmoData { get; set; }
	/// <summary>
	/// Amount of ammo to give
	/// </summary>
	[Property, Space, Range( 1, 1000, true, false ), Step( 1 ), ShowIf( nameof( FillMax ), false )] public int Amount { get; set; } = 1;
	/// <summary>
	/// We don't have that many weapons using the same ammo, which is why its probably ok to have a default value, and this helps set it for this ammo item
	/// </summary>
	[Button, ShowIf( nameof( FillMax ), false )]
	private void SetToDefaultAmount()
	{
		if ( AmmoData.IsValid() )
			Amount = AmmoData.DefaultAmmo;
	}

	/// <summary>
	/// Use AmmoData.AmmoModelLarge instead, for big ammo pickups
	/// </summary>
	[Property, Space] public bool UseLargeModel { get; set; } = false;
	/// <summary>
	/// Fill the whole reserve for this
	/// </summary>
	[Property] public bool FillMax { get; set; } = false;
	public Vector3 PositionImpulse { get; set; }
	public Vector3 AngularImpulse { get; set; }

	public BaseAmmoItem() { AmmoData = AmmoInfo.GetAmmoData( GetType().Name ); }

	protected override void OnValidate()
	{
		if ( GetType().Name != "BaseAmmoItem" && (!AmmoData.IsValid() || GetType().Name != AmmoData.ResourceName) )
			AmmoData = AmmoInfo.GetAmmoData( GetType().Name );

		base.OnValidate();
	}

	protected override string GetModel() => (UseLargeModel ? AmmoData?.AmmoModelLarge.ResourcePath : AmmoData?.AmmoModel.ResourcePath) ?? base.GetModel();

	protected override void OnStart()
	{
		base.OnStart();
		Physics?.PhysicsBody.ApplyImpulse( PositionImpulse );
		Physics?.PhysicsBody.ApplyAngularImpulse( AngularImpulse );
	}

	protected override bool PickupCheck()
	{
		return BasePlayer.Local.AddReserveAmmo( AmmoData.ResourceName, FillMax ? 10000 : Amount, true ) <= 0;
	}

	public override void OnPickup( BasePlayer Activator = null )
	{
		if ( !AmmoData.IsValid() )
			return;

		if ( !AmmoData.AmmoModel.IsValid() )
			return;

		base.OnPickup( Activator );
		BasePlayer.Local.AddReserveAmmo( AmmoData.ResourceName, FillMax ? 10000 : Amount );

		DestroyItem();
	}
}
