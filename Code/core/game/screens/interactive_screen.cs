using System;
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
	protected override string GetEditorVis() => null;
#if IGNIS
	[DebugExpose]
#endif
	[Property, Header( "Screen" )] public InteractPanel Screen { get; set; }

	[Property]
	public float RenderScale
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				HandleProcedural( null, Procedural.Update );
				Screen ??= GameObject.GetComponent<InteractPanel>();
			}
		}
	} = 1f;
#if IGNIS
	[DebugExpose]
#endif
	[Property]
	public Vector2 ScreenSize
	{
		get => field;
		set
		{
			if ( field != value )
			{
				field = value;
				HandleProcedural( null, Procedural.Update );
				Screen ??= GameObject.GetComponent<InteractPanel>();
			}
		}
	} = new( 1024, 768 );
#if IGNIS
	[DebugExpose]
#endif
	[Property, Title( "Max Interaction Distance" )]
	public float ScreenInteractionRange
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				HandleProcedural( null, Procedural.Update );
				Screen ??= GameObject.GetComponent<InteractPanel>();
			}
		}
	} = 30f;

	[Property, Feature( "Debug" ), Title( "Show WorldPanel" )]
	public bool ShowCreatedComponents
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Screen ??= GameObject.GetComponent<InteractPanel>();
				UpdateVisibilityTags();
			}
		}
	}

	[Property, Feature( "Debug" ), ReadOnly] private List<Component> ProceduralComponents { get; set; } = [];

	// Maximum number of buttons we can have, they appear only if they are set in Razor.
#if IGNIS
	[DebugExpose]
#endif
	[Group( "Outputs" ), SingleAction, Property] public Action Button_1 { get; set; }

#if IGNIS
	[DebugExpose]
#endif
	[Group( "Outputs" ), SingleAction, Property] public Action Button_2 { get; set; }
#if IGNIS
	[DebugExpose]
#endif
	[Group( "Outputs" ), SingleAction, Property] public Action Button_3 { get; set; }
#if IGNIS
	[DebugExpose]
#endif
	[Group( "Outputs" ), SingleAction, Property] public Action Button_4 { get; set; }
#if IGNIS
	[DebugExpose]
#endif
	[Group( "Outputs" ), SingleAction, Property] public Action Button_5 { get; set; }

	public enum Procedural
	{
		Add,
		Update,
		Delete
	}


	protected override void OnAwake()
	{
		if ( ProceduralComponents.FirstOrDefault( x => x is WorldPanel ) is not WorldPanel proceduralWorldPanel )
		{
			var newWorldPanel = GameObject.AddComponent<WorldPanel>();
			HandleProcedural( newWorldPanel, Procedural.Add );
			HandleProcedural( null, Procedural.Update );

			UpdateVisibilityTags();
		}

		if ( !Screen.IsValid() ) Screen ??= GameObject?.GetComponent<InteractPanel>();
	}

	protected override void OnStart()
	{
		if ( !Screen.IsValid() ) return;

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
		Screen?.ActiveButtons.Clear();
	}

	public void HandleProcedural( Component component, Procedural action )
	{
		switch ( action )
		{
			case Procedural.Add:

				if ( !ProceduralComponents.Contains( component ) ) ProceduralComponents.Add( component );
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

				if ( ProceduralComponents.Remove( component ) ) component.Destroy();
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

			if ( GetType().GetProperty( $"Button_{i}" )?.GetValue( this ) is Action action && Screen.IsValid() )
				Screen.ActiveButtons[key] = action;
		}
#else
		// Reflection-free version (safe for s&box)
		if ( !Screen.IsValid() ) return;

		for ( int i = 0; i < Buttons.Length; i++ )
		{
			Screen.ActiveButtons[$"button_{i + 1}"] = Buttons[i];
		}
#endif
	}
}
