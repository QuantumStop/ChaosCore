[Title( "Clear All Components" )]
[Description( "Clears all components on a gameobject including itself (to clear out hidden ones)" )]
[Category( "Core" )]
public class ComponentClear : Component, Component.ExecuteInEditor
{
	protected override void OnAwake()
	{
		base.OnStart();

		foreach ( var p in this.Components.GetAll<Component>( FindMode.EverythingInSelf ) )
		{
			if ( p != this ) { Log.Info( p ); } // dont output self because you can tell
			p.Destroy();
		}
	}
}
