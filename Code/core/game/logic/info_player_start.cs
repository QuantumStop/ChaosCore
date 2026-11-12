using Core;

[Category( "Game" ), Title( "info_player_start" )]
public class info_player_start : BaseEntity
{
	protected override string GetEditorVis() => "models/editor/playerstart.vmdl";
	[Property]
	public bool Primary
	{
		get;
		set
		{
			if ( field == value ) return;
			field = value;

			if ( value ) foreach ( var component in Scene.Components.GetAll<info_player_start>() )
			{
				if ( component == this ) continue;

				component.Primary = false;
			}
		}
	}

	//	OUTPUTS
	[Property, ActionGraphIgnore] public ChaosOutput OnPlayerSpawned { get; set; }
}
