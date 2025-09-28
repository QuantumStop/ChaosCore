using System;
using Sandbox.UI;
using WorldPanel = Sandbox.WorldPanel;

namespace Core;

/// <summary>
/// Interactive Screen is used for managing player interactive screens.
/// It will store logic for the screen and controls it's variables.
/// </summary>

[Icon( "present_to_all" )]
[Category( "UI" )]
public class InteractiveScreen : BaseEntity, Component.ExecuteInEditor
{
	protected override string GetEditorVis() { return null; }

	[DebugExpose, Property, Header( "Screen" ), MakeDirty] public InteractPanel Screen { get; set; }

	private float _renderScale = 1f;
	private Vector2 _screenSize = new Vector2( 1024, 768 );
	private float _screenInteractionRange = 30f;
	private bool _showCreatedComponents;

	[Property, MakeDirty]
	public float RenderScale
	{
		get => _renderScale;
		set
		{
			if ( _renderScale != value )
			{
				_renderScale = value;
				HandleProcedural( null, Procedural.Update );
			}
		}
	}

	[DebugExpose]
	[Property, MakeDirty]
	public Vector2 ScreenSize
	{
		get => _screenSize;
		set
		{
			if ( _screenSize != value )
			{
				_screenSize = value;
				HandleProcedural( null, Procedural.Update );
			}
		}
	}

	[DebugExpose]
	[Property, MakeDirty, Title( "Max Interaction Distance" )]
	public float ScreenInteractionRange
	{
		get => _screenInteractionRange;
		set
		{
			if ( _screenInteractionRange != value )
			{
				_screenInteractionRange = value;
				HandleProcedural( null, Procedural.Update );
			}
		}
	}

	[Property, MakeDirty, Feature( "Debug" ), Title( "Show WorldPanel" )]
	public bool ShowCreatedComponents
	{
		get => _showCreatedComponents;
		set
		{
			if ( _showCreatedComponents != value )
			{
				_showCreatedComponents = value;
				UpdateVisibilityTags();
			}
		}
	}

	[Property, MakeDirty, Feature( "Debug" ), ReadOnly] private List<Component> ProceduralComponents { get; set; } = new();

	// Maximum number of buttons we can have, they appear only if they are set in Razor.
	[DebugExpose][Group( "Outputs" ), SingleAction][Property, MakeDirty] public Action Button_1 { get; set; }

	[DebugExpose][Group( "Outputs" ), SingleAction][Property, MakeDirty] public Action Button_2 { get; set; }

	[DebugExpose][Group( "Outputs" ), SingleAction][Property, MakeDirty] public Action Button_3 { get; set; }

	[DebugExpose][Group( "Outputs" ), SingleAction][Property, MakeDirty] public Action Button_4 { get; set; }

	[DebugExpose][Group( "Outputs" ), SingleAction][Property, MakeDirty] public Action Button_5 { get; set; }

	public enum Procedural
	{
		Add,
		Update,
		Delete
	}


	protected override void OnAwake()
	{
		var proceduralWorldPanel = ProceduralComponents.FirstOrDefault( x => x is WorldPanel ) as WorldPanel;

		if ( proceduralWorldPanel == null )
		{
			var newWorldPanel = GameObject.AddComponent<WorldPanel>();
			HandleProcedural( newWorldPanel, Procedural.Add );
			HandleProcedural( null, Procedural.Update );

			UpdateVisibilityTags();
		}

		if ( Screen == null ) Screen ??= this.GameObject?.GetComponent<InteractPanel>();
	}

	protected override void OnDirty()
	{
		// Let's add the worldpanel this way, we don't really need to see it in the editor.
		base.OnDirty();

		Screen ??= this.GameObject.GetComponent<InteractPanel>();
	}

	protected override void OnStart()
	{
		if ( Screen == null ) return;

		// We need to refresh the button map to get the latest buttons from the interact panel.
		MapActiveButtons();
	}

	protected override void OnDestroy()
	{
		if ( ProceduralComponents is null )
			return;

		foreach ( var c in ProceduralComponents )
			c.Destroy();

		ProceduralComponents?.Clear();
		if ( Screen != null )
			Screen.ActiveButtons.Clear();
	}

	public void HandleProcedural( Component component, Procedural action )
	{
		switch ( action )
		{
			case Procedural.Add:

				if ( !ProceduralComponents.Contains( component ) )
					ProceduralComponents.Add( component );

				break;

			case Procedural.Update:

				foreach ( var _component in ProceduralComponents )
				{
					if ( _component is WorldPanel c )
					{
						c.PanelSize = ScreenSize;
						c.InteractionRange = ScreenInteractionRange;
						c.RenderScale = RenderScale;
					}
				}

				break;

			case Procedural.Delete:

				if ( ProceduralComponents.Contains( component ) )
				{
					ProceduralComponents.Remove( component );
					component.Destroy();
				}

				break;
		}

		UpdateVisibilityTags();
	}

	private void UpdateVisibilityTags()
	{
		if ( ProceduralComponents is null )
			return;

		foreach ( var c in ProceduralComponents )
		{
			if ( !ShowCreatedComponents )
			{
				c.Flags = ComponentFlags.Hidden;
			}
			else
			{
				c.Flags = ComponentFlags.None;
			}
		}
	}

	public Action[] Buttons = new Action[5];

	private void MapActiveButtons()
	{
#if STANDALONE
		// Your original reflection-based version
		for ( int i = 1; i <= 5; i++ )
		{
			string key = $"button_{i}";
			var action = GetType().GetProperty( $"Button_{i}" )?.GetValue( this ) as Action;

			if ( action != null && Screen != null )
				Screen.ActiveButtons[key] = action;
		}
#else
		// Reflection-free version (safe for s&box)
		if ( Screen == null ) return;

		for ( int i = 0; i < Buttons.Length; i++ )
		{
			var action = Buttons[i];
			if ( action != null )
			{
				Screen.ActiveButtons[$"button_{i + 1}"] = action;
			}
		}
#endif
	}
}
