namespace Core;

/// <summary>A volume thats used to control which entities go through the level transition.</summary>
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

		foreach ( var items in TrackedItems.Keys )
		{
			items.Flags = GameObjectFlags.DontDestroyOnLoad;
		}

	}

	protected override void OnTriggerOut()
	{
		base.OnTriggerIn();

		foreach ( var items in TrackedItems.Keys )
		{
			items.Flags = ~GameObjectFlags.DontDestroyOnLoad;
		}

	}



}
