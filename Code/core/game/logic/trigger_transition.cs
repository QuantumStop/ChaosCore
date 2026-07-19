namespace Core;

[Description( "A volume thats used to control which entities go through the level transition." )]
[Icon( "multiple_stop" )]
public class trigger_transition : BaseTrigger
{
	//[Title( "Linked changelevel" )][Property] public trigger_changelevel linkedTrigger { get; set; }



	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
	}

	protected override void OnTriggerIn()
	{
		base.OnTriggerIn();

		foreach ( var items in trackedItems.Keys )
		{
			items.Flags = GameObjectFlags.DontDestroyOnLoad;
		}

	}

	protected override void OnTriggerOut()
	{
		base.OnTriggerIn();

		foreach ( var items in trackedItems.Keys )
		{
			items.Flags = ~GameObjectFlags.DontDestroyOnLoad;
		}

	}



}
