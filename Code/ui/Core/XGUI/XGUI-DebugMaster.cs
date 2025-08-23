using Sandbox.UI;
using System;
using XGUI;

public class XGUI_DebugMaster_Manager : BaseEntity
{
	public XGUISystem xguiSystem => Scene.GetSystem<XGUISystem>();
	public Panel Panel { get; set; } = null;
	public static List<string> savedconmsg = new();
	[Property] public int ChildCount = 0; // Track the number of child panels

	public static XGUI_DebugMaster_Manager Local { get; set; }

	public XGUI_DebugMaster_Manager()
	{
		Local = this;
	}

	protected override void OnStart()
	{
		if ( this.xguiSystem.Component == null ) return;

		if ( ChildCount <= 0 ) xguiSystem.Component.MouseUnlocked = false;
		else xguiSystem.Component.MouseUnlocked = true;
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( xguiSystem != null )
		{
			if ( ChildCount > 0 ) xguiSystem.Component.MouseUnlocked = true;
			else xguiSystem.Component.MouseUnlocked = false;
		}
	}

	protected override void OnDirty()
	{
		base.OnDirty();
		if ( xguiSystem != null )
		{
			if ( ChildCount > 0 ) xguiSystem.Component.MouseUnlocked = true;
			else xguiSystem.Component.MouseUnlocked = false;
		}
	}

	public void ToggleConsole()
	{
		if ( !Local.xguiSystem.Panel.Children.Contains( Panel ) )
		{
			Window window = new XGUI_DebugConsole();
			Panel = window;
			CreatePanel( Panel );
			window.FocusWindow();
		}
		else DeletePanel( Panel );
	}

	public void ToggleMasterDebug()
	{
		if ( !xguiSystem.Panel.Children.Contains( Panel ) )
		{
			Window window = new XGUI_DebugMasterPanel();
			Panel = window;
			CreatePanel( Panel );
			window.FocusWindow();
		}
		else DeletePanel( Panel );
	}

	public void CreatePanel( Panel _panel )
	{
		//	Log.Info( $"Added Panel as a child: {_panel}" );
		xguiSystem.Panel.AddChild( _panel );
		ChildCount++;
	}

	public void DeletePanel( Panel _panel )
	{
		//	Log.Info( $"Deleted Panel: {_panel}" );
		_panel.Delete();
		ChildCount--;
	}

	public static void GenericCountChange()
	{
		Local.ChildCount--;
	}
}

