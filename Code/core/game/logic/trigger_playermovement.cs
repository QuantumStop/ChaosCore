namespace Core;

/// <summary>Trigger that can affect player's movement</summary>
[Icon( "looks" )]
public class trigger_playermovement : BaseTrigger
{
	protected override void OnTriggerIn()
	{
		if ( !_entityCollider.IsValid() )
			return;

		if ( TrackedItems.Keys.Any( go => go.GetComponentInParent<BasePlayer>().IsValid() ) )
		{
			(BasePlayer.Local.Controller as PlayerController).ForceCrouch = true; // this is bad since its always local player, not the one that actually entered
			Log.Info( "Crouch" );
		}
	}

	protected override void OnTriggerOut()
	{
		if ( !_entityCollider.IsValid() )
			return;

		Log.Info( "Uncrouch" );
		(BasePlayer.Local.Controller as PlayerController).ForceCrouch = false;
	}
}
