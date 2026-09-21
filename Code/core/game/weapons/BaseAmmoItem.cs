#if FMOD
using FMODSbox;
#endif

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
	[Property, Space, Range( 1, 1000, true, false ), Step( 1 ), ShowIf( nameof( FillMax ), false ), Feature( "Custom Amount" )] public int Amount { get; set; } = 1;
	/// <summary>
	/// We don't have that many weapons using the same ammo, which is why its probably ok to have a default value, and this helps set it for this ammo item
	/// </summary>
	[Button, ShowIf( nameof( FillMax ), false ), Feature( "Custom Amount" )]
	private void SetToDefaultAmount()
	{
		if ( AmmoData.IsValid() )
			Amount = AmmoData.DefaultAmmo;
	}

	[Property, FeatureEnabled( "Custom Amount" )] private bool _amount { get; set; } = false;

	/// <summary>Use AmmoData.AmmoModelLarge instead, for big ammo pickups</summary>
	[Property, Space] public bool UseLargeModel { get; set; } = false;
	/// <summary>Fill the whole reserve for this</summary>
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

	protected override bool PickupCheck() => LastOwner?.GetReserveAmmo( AmmoData?.ResourceName ) < AmmoData?.MaxAmmo;

	public override void OnPickup( BasePlayer Activator = null )
	{
		if ( !AmmoData.IsValid() || !AmmoData.AmmoModel.IsValid() ) return;

		PickUp = false;

		if ( !PickupCheck() ) return;

		if ( Activator.IsValid() ) OnPickupOutput?.Invoke( Activator );
#if FMOD
		FMODSound.Play( GetPickupSound() );
#else
		Activator.PlayPickupSteal( GetPickupSound(), SoundStealChannel(), WorldPosition );
#endif

		Activator.AddReserveAmmo( AmmoData.ResourceName, FillMax ? 10000 : _amount ? Amount : AmmoData.DefaultAmmo );

		DestroyItem();
	}
}
