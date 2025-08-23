using System;
using Core;

namespace chaoscore;

[Category( "Items" )]
public class item_healthkit : BaseItem
{
	protected override string GetModel() { return "models/items/item_medkit.vmdl"; }
	protected override string GetPickupSound() { return "medkit_pickup"; }

	[DebugExpose( label: "Heal Amount", group: "Health Charge" )]
	[Property, Range( 0, 100 ), Step( 1 )] public float HealAmount { get; set; } = 25f;

	public override void OnPickup( BasePlayer Activator = null )
	{
		if ( BasePlayer.Local.Health >= 100f ) { PickUp = false; return; }

		base.OnPickup( Activator );

		BasePlayer.Local.Health = Math.Clamp( BasePlayer.Local.Health + HealAmount, 0f, 100f );

		Core.ScreenFlash.Set( Color.Green, 0.5f );

		DestroyItem();

	}
}
