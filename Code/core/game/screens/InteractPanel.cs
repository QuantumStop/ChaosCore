using Sandbox.UI;
using System;
using System.Threading.Tasks;
using WorldPanel = Sandbox.UI.WorldPanel;

namespace Core;

/// <summary>
/// InteractPanel is a component that manages base features for an interactive screen.
/// Right now it allows adding buttons and retrieving them by their ID.
/// </summary>
public class InteractPanel : PanelComponent
{
	[Property, ReadOnly] public Dictionary<string, Action> ActiveButtons = new();
	IEnumerable<Panel> GetAllChildren( Panel root )
	{
		if ( root == null )
			yield break;

		yield return root;

		foreach ( var child in root.Children )
		{
			foreach ( var descendant in GetAllChildren( child ) )
				yield return descendant;
		}
	}
	Sandbox.UI.WorldPanel GetWorldPanel( Panel worldpanel )
	{
		while ( worldpanel != null )
		{
			if ( worldpanel is Sandbox.UI.WorldPanel wp )
				return wp;

			worldpanel = worldpanel.Parent;
		}

		return null;
	}

	[Property, MakeDirty, ReadOnly] public bool IsInteracting { get; set; } = false;

	private Panel CursorPanel;
	private Panel ParentPanel;
	private Vector2 previousCursorPos = new Vector2( 0, 0 );


	protected override void OnTreeFirstBuilt()
	{
		if ( Panel == null )
			return;

		ParentPanel = Panel.FindRootPanel();
		Panel.AddClass( "interactive-screen" ); // We need this class to check it if need-be, easier this way

		CursorPanel = new Panel( Panel );
		CursorPanel.AddClass( "cursor" );

		// All the styling and stuff, so everything is correcto mundo
		CursorPanel.StyleSheet.Load( "ui/SDK/interactive/cursor.scss" );
		CursorPanel.Style.Position = PositionMode.Absolute;
		CursorPanel.Style.PointerEvents = PointerEvents.None;

		Panel.AddChild( CursorPanel ); // We could actually add/remove it at run time during an event, but i thought lets just keep it at all times

		SetEventListeners();
	}

	protected override void OnDirty()
	{
		base.OnDirty();

		if ( Panel == null || CursorPanel == null )
			return;

		HandleInteraction( IsInteracting );
	}

	protected void SetEventListeners()
	{
		ParentPanel.AddEventListener( "onmouseover", e =>
		{
			IsInteracting = true;
		}
		);

		ParentPanel.AddEventListener( "onmouseout", e =>
		{
			if ( ParentPanel.HasHovered ) return;   // Failsafe to prevent the panel from thinking its not being interacted with. Thank you, Xenthio!
			IsInteracting = default;
		}
		);
	}

	protected async void HandleInteraction( bool Interact )
	{
		if ( Interact )
			BasePlayer.Local.CurrentHiddenHUDFlags |= BasePlayer.HIDEHUD_FLAGS.HIDEHUD_CROSSHAIR;
		else
			BasePlayer.Local.CurrentHiddenHUDFlags &= ~BasePlayer.HIDEHUD_FLAGS.HIDEHUD_CROSSHAIR;

		if ( !CursorPanel.HasClass( "visible" ) && Interact )
			CursorPanel.AddClass( "visible" );

		else if ( CursorPanel.HasClass( "visible" ) && !Interact )
			CursorPanel.RemoveClass( "visible" );

		await Task.Delay( 300 ); // Hypothetical flicker prevention and to ensure buttons are not selected immediately

		// Enabling/Disabling pointer events for all buttons
		foreach ( var child in GetAllChildren( Panel ) )
		{
			if ( child is Sandbox.UI.Button button )
			{
				if ( Interact )
					button.Style.PointerEvents = PointerEvents.All;
				else
					button.Style.PointerEvents = PointerEvents.None;
			}
		}
	}

	protected override void OnMouseOver( MousePanelEvent e )
	{
		if ( e.Target is Sandbox.UI.Button button && IsInteracting )
		{
			var action = ProcessPanel( button );
			if ( action != null )
			{
				Sound.Play( "ui_hover" ); // TODO: Need to not hardcode this
			}
		}
	}

	protected override void OnMouseMove( MousePanelEvent e )
	{
		if ( CursorPanel == null || Panel == null || !IsInteracting )
			return;

		if ( BasePlayer.Local == null || BasePlayer.Local.Scene == null )
			return;

		var aimRay = BasePlayer.Local.Controller.AimRay;

		Vector2 localPos;
		float distance;
		float smoothFactor = 60.0f;

		// To make sure we even have worldpanel, we'll need it!
		var worldpanel = GetWorldPanel( Panel );
		if ( worldpanel == null )
			return;

		if ( !worldpanel.RayToLocalPosition( aimRay, out localPos, out distance ) )
			return;

		Vector2 panelSize = Panel.Box.Rect.Size;

		localPos += panelSize / 2f;

		localPos.x = Math.Clamp( localPos.x, 0, panelSize.x );
		localPos.y = Math.Clamp( localPos.y, 0, panelSize.y );

		// By default there will be some jittering, so we can smooth it out
		Vector2 deltaPosition = localPos - previousCursorPos;
		Vector2 Movement = deltaPosition * smoothFactor * Time.Delta;
		Vector2 smoothCursorPos = previousCursorPos + Movement;

		CursorPanel.Style.Left = Length.Pixels( smoothCursorPos.x * 0.5f );
		CursorPanel.Style.Top = Length.Pixels( smoothCursorPos.y * 0.5f );

		CursorPanel.Style.Dirty();

		previousCursorPos = smoothCursorPos;
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );

		// This is exact time when we start pressing the button.
		if ( e.Target is Sandbox.UI.Button button && IsInteracting )
		{
			Sound.Play( "ui_click.sound" ); // TODO: Need to not hardcode this
			var action = ProcessPanel( button );
		}
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );

		// When the button is released and we've successfully clicked it,
		// we can invoke an action associated with the button.
		if ( e.Target is Sandbox.UI.Button button && IsInteracting )
		{
			var action = ProcessPanel( button );
			action?.Invoke();
		}
	}


	// Instead of pre-mapping buttons manually, we can use this method to process panels based on their ID.
	// ActiveButtons will just contain all buttons with their IDs and actions. 
	private Action ProcessPanel( Sandbox.UI.Button button )
	{
		if ( button == null || ActiveButtons == null || !IsInteracting )
			return null;

		var id = button.GetAttribute( "data-id" );

		if ( string.IsNullOrEmpty( id ) )
			return null;

		if ( ActiveButtons.TryGetValue( id, out var action ) )
			return action;

		return null;
	}

}
