using Sandbox.UI;
using System;
using System.Reflection;

public class ConsoleWindow : XGUI.Window
{
	public TextEntry conentry { get; set; }
	public TextEntry filterentry { get; set; }
	public TextEntry searchentry { get; set; }


	public static List<string> conmsg = new();
	public static List<string> confilteredmsg = new();

	internal static bool _hasAddedHooks = false;

	private static bool _enableDevMode = false;

	public ConsoleWindow()
	{
		SetupHooks();
	}

	public static void SetupHooks()
	{
		if ( _hasAddedHooks ) return;
		Log.Info( "[ConsoleWindow] Setting up hooks for console logging." );

		// Find the assembly
		var asm = AppDomain.CurrentDomain.GetAssemblies()
			.FirstOrDefault( a => a.GetName().Name == "Sandbox.System" );
		if ( asm == null ) return;

		// Find the Logging type
		var loggingType = asm.GetType( "Sandbox.Diagnostics.Logging" );
		if ( loggingType == null )
		{
			Log.Warning( "[ConsoleWindow] Could not find Logging type in Sandbox.System assembly." );
			return;
		}

		// Get the private static field 'OnMessage'
		var field = loggingType.GetField( "OnMessage", BindingFlags.Static | BindingFlags.NonPublic );
		if ( field == null )
		{
			Log.Warning( "[ConsoleWindow] Could not find OnMessage field in Logging type." );
			return;
		}

		// Get the current delegate
		var current = field.GetValue( null ) as Delegate;

		// Create our handler
		Action<LogEvent> handler = OnLog;

		// Remove the handler if it already exists (optional, for safety)
		if ( current != null )
		{
			try
			{
				field.SetValue( null, Delegate.Remove( current, handler ) );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[ConsoleWindow] Failed to remove existing OnMessage handler: {ex.Message}" );
			}
		}

		// Combine with existing delegate
		var combined = Delegate.Combine( current, handler );

		// Set the field back
		field.SetValue( null, combined );

		_hasAddedHooks = true;
	}

	//-- Core Console Commands and Console Variables go here --//

	public static void OnLog( LogEvent logEvent )
	{
		// Add the log message to the console message list
		conmsg.Add( $"[{DateTime.Now:HH:mm:ss}] {logEvent.Message}" );
		XGUI_DebugMaster_Manager.savedconmsg.Add( $"[{DateTime.Now:HH:mm:ss}] {logEvent.Message}" );
	}

	[ConCmd( "Clear" )]
	static void ClearHistory()
	{
		// Clear active
		conmsg.Clear();

		// Clear the backup too, we don't need it
		XGUI_DebugMaster_Manager.savedconmsg.Clear();
	}

	[ConCmd( "Debug_Find" )]
	static void TryFind()
	{
		var consoleSystemType = TypeLibrary.GetType( "Sandbox.ConsoleSystem" )?.TargetType;

		if ( consoleSystemType != null )
		{
			var methods = consoleSystemType.Assembly.GetTypes()
			.SelectMany( t => t.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic ) )
			.Where( m => m.GetCustomAttributes( typeof( ConCmdAttribute ), false ).Any() )
			.ToList();

			var commandList = new List<string>();
			foreach ( var method in methods )
			{
				var conCmdAttribute = method.GetCustomAttribute<ConCmdAttribute>();
				if ( conCmdAttribute != null )
				{
					commandList.Add( method.Name );
				}
			}

			foreach ( var command in commandList )
			{
				conmsg.Add( $"[Debug_Find] Found Console Command: {command}" );
			}

			Log.Info( $"[Debug_Find] Found {commandList.Count} console commands." );
		}
		else
		{
			Log.Warning( "[Debug_Find] Could not find Sandbox.ConsoleSystem." );
		}

	}

	[ConVar( "developer", Help = "Enable developer mode for this session" )]
	public static bool EnableDevMode
	{
		get => _enableDevMode;
		set
		{
			_enableDevMode = value;

			if ( value )
			{
				Log.Info( "[DevMode] Enabling developer mode via environment variable." );
			}
			else
			{
				Log.Info( "[DevMode] Disabling developer mode." );
			}
		}
	}


	//--- End block of CONCMDS/CONVARS --//


	public override void OnClose()
	{
		XGUI_DebugMaster_Manager.GenericCountChange();
	}


	public void CompareMSG()
	{
		confilteredmsg.Clear();

		if ( filterentry != null && !string.IsNullOrWhiteSpace( filterentry.Text ) )
		{
			foreach ( var message in conmsg )
			{
				if ( message.Contains( filterentry.Text, StringComparison.OrdinalIgnoreCase ) )
				{
					confilteredmsg.Add( message );
				}
			}
		}
		else
		{
			confilteredmsg.AddRange( conmsg );
		}

		StateHasChanged();
	}

	public async void Submit()
	{
		string command = conentry.Text;

		if ( !string.IsNullOrWhiteSpace( command ) && command != "Clear" && command != "clear" )
		{
			ParseAndExecute( command );
			await Task.Delay( 50 );

			var rawValue = ConsoleSystem.GetValue( command );
			var concmdValue = rawValue != null && rawValue.ToBool();

			conmsg.Add( $"[{DateTime.Now:HH:mm:ss}] {command}" );
			XGUI_DebugMaster_Manager.savedconmsg.Add( $"[{DateTime.Now:HH:mm:ss}] {command}" );

			StateHasChanged();
		}

	}

	public void Clear()
	{
		if ( conentry.Text == "Clear" || conentry.Text == "clear" )
		{
			ConsoleSystem.Run( "Clear" );
			conentry.Text = string.Empty;
		}
		else conentry.Text = string.Empty;
	}

	public static void ParseAndExecute( string input )
	{
		if ( string.IsNullOrWhiteSpace( input ) )
			return;

		var parts = input.Trim().Split( ' ', 2, StringSplitOptions.RemoveEmptyEntries );
		var name = parts[0];
		var value = parts.Length > 1 ? parts[1] : null;


		if ( TrySetConVar( name, value ) ) return;
		if ( TryRunConCmd( name ) ) return;

		// Try internal engine ConsoleSystem.Run(string)
		if ( TryEngineConsoleRun( input ) ) return;

		Log.Warning( $"[Reflection] Command or ConVar '{name}' not found." );
	}

	private static bool TrySetConVar( string name, string value )
	{
		if ( value == null )
			return false;

		var field = AppDomain.CurrentDomain
		.GetAssemblies()
		.SelectMany( a => a.GetTypes() )
		.SelectMany( t => t.GetFields( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic ) )
		.FirstOrDefault( f =>
		{
			var attr = f.GetCustomAttribute<ConVarAttribute>();
			return attr != null && attr.Name == name;
		} );

		if ( field == null )
			return false;

		try
		{
			object converted = value;

			if ( field.FieldType == typeof( bool ) && bool.TryParse( value, out var bval ) )
				converted = bval;
			else if ( field.FieldType == typeof( int ) && int.TryParse( value, out var ival ) )
				converted = ival;
			else if ( field.FieldType == typeof( float ) && float.TryParse( value, out var fval ) )
				converted = fval;

			field.SetValue( null, converted );
			Log.Info( $"[Reflection] Set ConVar '{name}' = {value}" );

			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Reflection] Failed to set ConVar '{name}': {ex.Message}" );

			return false;
		}
	}

	private static bool TryRunConCmd( string name )
	{
		var method = AppDomain.CurrentDomain
			.GetAssemblies()
			.SelectMany( a => a.GetTypes() )
			.SelectMany( t => t.GetMethods( BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic ) )
			.FirstOrDefault( m =>
			{
				var attr = m.GetCustomAttribute<ConCmdAttribute>();
				return attr != null && attr.Name == name;
			} );

		if ( method == null )
			return false;

		try
		{
			method.Invoke( null, null ); // Supports no-arg commands
			Log.Info( $"[Reflection] Ran ConCmd '{name}'" );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Reflection] Failed to run ConCmd '{name}': {ex.Message}" );
			return false;
		}
	}

	private static bool TryEngineConsoleRun( string input )
	{

		try
		{
			var asm = AppDomain.CurrentDomain.GetAssemblies()
				.FirstOrDefault( a => a.GetName().Name == "Sandbox.Engine" );
			var internalType = asm.GetType( "Sandbox.ConVarSystem" );

			if ( internalType == null )
			{
				Log.Warning( "[Reflection] Could not find internal ConVarSystem type" );
				return false;
			}

			var runMethod = internalType?.GetMethod( "Run", BindingFlags.Static | BindingFlags.NonPublic );

			if ( runMethod == null )
			{
				Log.Warning( "[Reflection] Could not find internal ConVarSystem.Run" );
				return false;
			}

			runMethod.Invoke( null, new object[] { input } );
			//Log.Info( $"[Engine] Ran internal command: {input}" );
			return true;
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Engine] Failed to invoke internal ConVarSystem.Run: {ex.Message}" );
			return false;
		}

	}




}
