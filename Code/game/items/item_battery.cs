using System;
using Core;

namespace chaoscore;

[Category( "Items" )]
public class item_battery : BaseItem
{
	protected override string GetModel() { return "models/items/ohmbattery.vmdl"; }
	protected override string GetPickupSound() { return "battery_pickup"; }

	[DebugExpose( label: "Charge Amount", group: "Suit Charge" )]
	[Property, Range( 0, 100 ), Step( 1 )] public float HealAmount { get; set; } = 15f;

	public override void OnPickup( BasePlayer Activator = null )
	{
		if ( BasePlayer.Local.Armour >= 100f ) { PickUp = false; return; }

		base.OnPickup( Activator );

		BasePlayer.Local.Armour = Math.Clamp( BasePlayer.Local.Armour + HealAmount, 0f, 100f );

		Core.ScreenFlash.Set( Color.Blue, 0.5f );

		DestroyItem();
	}
}
