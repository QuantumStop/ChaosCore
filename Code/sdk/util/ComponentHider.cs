[Title( "Hide All Components" )]
[Description( "Hides all components using the" )]
[Category( "Core" )]
public class ComponentHider : Component, Component.ExecuteInEditor
{
	protected override void OnAwake()
	{
		base.OnStart();

		foreach ( var p in Components.GetAll<Core.GameProp>( FindMode.EverythingInSelf ) )
		{
			p.ApplyVisibilityFlags();
		}

		this.Destroy();
	}
}
