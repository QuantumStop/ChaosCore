using Core;

public class info_player_start : BaseEntity
{
	protected override string GetEditorVis() { return "models/editor/playerstart.vmdl"; }
	[Property] public bool Primary { get; set; }
	public bool RemoveOnLevelLoad { get; set; } = true;

	protected override void OnValidate()
	{
		base.OnValidate();

		if ( Primary ) foreach ( var component in Scene.Components.GetAll<info_player_start>() )
			{
				if ( component == this )
					continue;

				component.Primary = false;
			}
	}

	public void OnPlayerSpawnedInternal()
	{
		foreach ( var component in Components.GetAll<BaseItem>() )
			component.PickUp = true;
	}

	//	INPUTS
	//	disable removing on level load, useful if you have async logic that needs to run on this

	public void DontRemoveOnLevelLoad( GameObject activator, GameObject caller )
	{
		RemoveOnLevelLoad = false;
	}

	// protected override void RegisterInputs() 
	// {
	// 	base.RegisterInputs();
	// 	RegisterInput("DontRemoveOnLevelLoad", Input_DontRemoveOnLevelLoad);
	// }

	//	OUTPUTS
	[Property, ActionGraphIgnore] public ChaosOutput OnPlayerSpawned { get; set; }
}
