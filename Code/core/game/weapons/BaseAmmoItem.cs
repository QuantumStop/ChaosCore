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
	[Property, Range( 1, 1000, true, false ), Step( 1 )] public int Amount { get; set; } = 1;
	/// <summary>
	/// Use AmmoData.AmmoModelLarge instead, for big ammo pickups
	/// </summary>
	[Property] public bool UseLargeModel { get; set; } = false;

	/// <summary>
	/// Fill the whole reserve for this
	/// </summary>
	[Property] public bool FillMax { get; set; } = false;
	public Vector3 PositionImpulse { get; set; }
	public Vector3 AngularImpulse { get; set; }

	public BaseAmmoItem() { AmmoData = AmmoInfo.GetAmmoData( GetType().Name ); }

	protected override void OnValidate()
	{
		if ( GetType().Name != "BaseAmmoItem" && (AmmoData == null || GetType().Name != AmmoData.ResourceName) )
			AmmoData = AmmoInfo.GetAmmoData( GetType().Name );

		base.OnValidate();
	}

	protected override string GetModel()
	{
		if ( AmmoData != null )
		{
			if ( AmmoData.AmmoModel == null )
				return "models/dev/error.vmdl";

			if ( UseLargeModel )
				return AmmoData.AmmoModelLarge.ResourcePath;

			return AmmoData.AmmoModel.ResourcePath;
		}

		return base.GetModel();
	}

	protected override void OnStart()
	{
		base.OnStart();
		Physics?.PhysicsBody.ApplyImpulse( PositionImpulse );
		Physics?.PhysicsBody.ApplyAngularImpulse( AngularImpulse );
	}

	protected override bool PickupCheck()
	{
		return BasePlayer.Local.AddReserveAmmo( AmmoData.ResourceName, FillMax ? 10000 : Amount, true ) > 0 ? false : true;
	}

	public override void OnPickup( BasePlayer Activator = null )
	{
		if ( !AmmoData.IsValid() )
			return;

		if ( AmmoData.AmmoModel == null )
			return;

		base.OnPickup( Activator );
		BasePlayer.Local.AddReserveAmmo( AmmoData.ResourceName, FillMax ? 10000 : Amount );

		DestroyItem();
	}
}
