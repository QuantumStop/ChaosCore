using System;
using System.Reflection;

namespace Editor;

public static class RealFullscreen
{
	public static bool Fullscreened = false;
	public static Vector2 Resolution;
	[Event( "tools.editorwindow.postcreateview" )]

	public static void GetScreenResolution()
	{
		EditorWindow.Focus();
		EditorWindow.Show();
		var pos = Application.CursorPosition;
		// the cheatiest way to find the actual screen resolution  
		Application.CursorPosition = new Vector2( 99999, 99999 );
		Resolution = Application.CursorPosition;
		//restore mouse pos after that gross hack
		Application.CursorPosition = pos;
	}

	static Widget OldParent;

	//[Event( "command editor.realfulltoggle" )]
	[Shortcut( "editor.real-fullscreen", "F11", ShortcutType.Application )]
	public static void RealFullscreenToggle()
	{
		GetScreenResolution();
		BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
		var GameFrameType = Assembly.GetAssembly( typeof( Widget ) ).GetType( "Editor.GameFrame" );
		var GameFrame = GameFrameType.GetProperty( "Singleton" ).GetValue( null, null ) as Widget;
		var Canvas = GameFrameType.GetField( "EngineCanvas", flags ).GetValue( GameFrame ) as Widget;
		var EngineView = GameFrameType.GetField( "EngineView", flags ).GetValue( GameFrame ) as Widget;
		Fullscreened = !Fullscreened;
		if ( Fullscreened )
		{
			OldParent = Canvas.Parent;
			Canvas.IsWindow = true;
			Canvas.IsFramelessWindow = true;
			Canvas.ShowWithoutActivating = true;
			Canvas.Parent = null;
			Canvas.Visible = true;
			Canvas.Position = Vector2.Zero;

			Canvas.Width = Math.Max( Resolution.x + 1, 500 );
			Canvas.Height = Math.Max( Resolution.y + 1, 500 );

			//Log.Info( EngineView.FocusProxy );

		}
		else
		{
			Canvas.Parent = GameFrame;
			Canvas.IsFramelessWindow = false;
			Canvas.Visible = true;

			GameFrame.Layout.Clear( false );

			//var LayoutHeader = GameFrame.Layout.Add( Layout.Row( true ) );
			//var LayoutHeader = GameFrameType.GetField( "LayoutHeader", flags ).GetValue( GameFrame );
			GameFrame.Layout.Add( Canvas, 1 );
			var StatusBar = GameFrameType.GetField( "statusBar", flags ).GetValue( GameFrame ) as Widget;
			var LayoutFooter = GameFrame.Layout.Add( Layout.Row( false ) );
			LayoutFooter.Add( StatusBar );
			GameFrame.Layout.Add( LayoutFooter, 1 );

			//Canvas.Raise();
			//GameFrameType.GetField( "LayoutFooter", flags ).SetValue( GameFrame, LayoutFooter );
			/*
			BindingFlags eventflags = BindingFlags.Public | BindingFlags.Static;
			var asm = Assembly.Load( "Sandbox.Game" );
			Log.Info( asm );
			var EventType = asm.GetType( "Sandbox.Event" );
			Log.Info( EventType );
			var eventrunner = EventType.GetMethod( "Run", eventflags, new Type[] { typeof( string ) } );
			Log.Info( eventrunner );
			eventrunner.Invoke( null, new string[] { "refresh" } );
			*/

			//Event.Run( "tools.refresh" );
		}
	}
}

