[Title( "Show All Components" )]
[Description( "Shows all components on a gameobject" )]
[Category( "Core" )]
public class ComponentShower : Component, Component.ExecuteInEditor
{
	protected override void OnAwake()
	{
		base.OnStart();

		foreach ( var p in this.Components.GetAll<Component>( FindMode.EverythingInSelf ) )
		{
			if ( p != this ) { Log.Info( p ); } // dont output self because you can tell
			p.Flags = 0;
		}

		this.Destroy();
	}
}
