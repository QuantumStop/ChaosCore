using Sandbox.ModelEditor.Nodes;

namespace Core;

/// <summary>
/// The new and cool prop component which you should use instead of the old one
/// </summary>
[EditorHandle( "" )]
[Title( "Game Prop" )]
[Category( "Game" )]
[Icon( "inventory" )]
[Description( "A better prop, again" )]
public partial class GameProp : BaseUsable, Component.ExecuteInEditor, Component.IDamageable
{
	protected override string GetEditorVis()
	{
		if ( _model == null )
			return "models/editor/axis_helper_thick.vmdl";

		else return null;
	}

	readonly ComponentFlags procFlags = ComponentFlags.NotSaved | ComponentFlags.NotCloned | ComponentFlags.Hidden;

	/// <summary>
	/// Adds the component flags to all procedural components
	/// </summary>
	public void ApplyVisibilityFlags()
	{
		if ( ProceduralComponents == null )
			return;

		foreach ( var c in ProceduralComponents )
		{
			c.Flags = procFlags;
		}
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();
		ClearProcedurals();
		UpdateComponents();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		ClearProcedurals();
	}

	protected override void OnDirty()
	{
		base.OnDirty();

		if ( Model.IsValid() )
		{
			if ( OverrideHealth )
				Health = NewHealth;
			else
			{
				if ( Model.TryGetData<ModelPropData>( out var data ) )
				{
					if ( !OverrideHealth )
						Health = ((data.Health > 0f) ? data.Health : Health);
				}
			}
		}
	}
}
