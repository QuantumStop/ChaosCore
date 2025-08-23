namespace chaoscore;

using Core;

[Category( "Items" )]
public class item_suit : BaseItem
{
	protected override string GetModel() { return "models/editor/playerstart_vest.vmdl"; }
	protected override bool IsStatic() { return true; }
	public override void OnPickup( BasePlayer Activator = null )
	{
		if ( BasePlayer.Local.HasSuit ) { PickUp = false; return; }

		base.OnPickup( Activator );

		BasePlayer.Local.HasSuit = true;
		DestroyItem();
	}

	public override void OnRemove()
	{
		if ( !BasePlayer.Local.HasSuit )
			return;

		base.OnRemove();
		DestroyItem();

		BasePlayer.Local.HasSuit = false;
	}
}
