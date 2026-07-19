using Sandbox.UI;
using System;
using XGUI;

namespace Core;

public class XGUI_DebugMaster_Manager : BaseEntity
{
	protected override string GetEditorVis() => null; // dont want to see obsolete in player prefab
	public XGUISystem XguiSystem => Scene.GetSystem<XGUISystem>();
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
		if ( XguiSystem.Component == null ) return;

		if ( ChildCount <= 0 ) XguiSystem.Component.MouseUnlocked = false;
		else XguiSystem.Component.MouseUnlocked = true;
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( XguiSystem is not null )
		{
			if ( ChildCount > 0 ) XguiSystem.Component?.MouseUnlocked = true;
			else XguiSystem.Component?.MouseUnlocked = false;
		}
	}
	/*
		protected override void OnDirty()
		{
			base.OnDirty();
			if ( xguiSystem != null )
			{
				if ( ChildCount > 0 ) xguiSystem.Component.MouseUnlocked = true;
				else xguiSystem.Component.MouseUnlocked = false;
			}
		}
	*/
	public void ToggleMasterDebug()
	{
		if ( !XguiSystem.Panel.Children.Contains( Panel ) )
		{
			Window window = new XGUIDebugMasterPanel();
			Panel = window;
			CreatePanel( Panel );
			window.FocusWindow();
		}
		else DeletePanel( Panel );
	}

	public void CreatePanel( Panel _panel )
	{
		//	Log.Info( $"Added Panel as a child: {_panel}" );
		XguiSystem.Panel.AddChild( _panel );
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

