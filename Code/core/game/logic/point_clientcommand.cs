using Sandbox;
using System;
using System.Reflection;

namespace Core;

[Icon( "terminal" )]

[Description( "An entity that issues commands to the client console, as if it was typed in by the player (if activator is a player, or the local player in single player)." )]
public class point_clientcommand : BaseEntity
{
	public new delegate void ChaosOutput( point_clientcommand activator );

	/// <summary>
	/// Fired when a command was issued to client's console.
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnCommandIssued { get; set; }

	/// <summary>
	///  The command to issue to the client's console. If this is empty, the entity will not issue any command.	
	/// </summary>
	/// <param name="activator"></param>
	/// <param name="command"></param>
	/// <returns></returns>
	public BaseEntity Command( BaseEntity activator = null, string command = null )
	{
		if ( activator == null )
			activator = this;

		RunCMD( command );

		OnCommandIssued?.Invoke( this );

		return activator;
	}

	public virtual void RunCMD( string command )
	{
		if ( string.IsNullOrWhiteSpace( command ) )
			return;

		ConsoleSystem.Run( command, this );
	}

	[Button( "Switch Fullbright" )]
	public void TryEngineCommand()
	{
		ConsoleBypassDemo.TryRunProtectedCommand( "mat_fullbright", "1" );
	}

}


public static class ConsoleBypassDemo
{
	public static void TryRunProtectedCommand( string commandName, params string[] args )
	{
		// Get the internal ConsoleCommand struct type
		var consoleSystemType = typeof( ConsoleSystem );
		var consoleCommandType = consoleSystemType.GetNestedType( "ConsoleCommand", BindingFlags.NonPublic );

		if ( consoleCommandType == null )
		{
			Log.Warning( "Could not find ConsoleCommand type." );
			return;
		}

		// Create an instance of ConsoleCommand using Activator
		var constructor = consoleCommandType.GetConstructor( BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof( string ), typeof( string[] ) }, null );
		if ( constructor == null )
		{
			Log.Warning( "Could not get ConsoleCommand constructor." );
			return;
		}

		object consoleCommand = constructor.Invoke( new object[] { commandName, args } );

		// Get RunInternal method
		var runInternal = consoleSystemType.GetMethod( "RunInternal", BindingFlags.NonPublic | BindingFlags.Static );
		if ( runInternal == null )
		{
			Log.Warning( "Could not find RunInternal method." );
			return;
		}

		try
		{
			runInternal.Invoke( null, new object[] { consoleCommand } );
			Log.Info( $"Tried to run: {commandName} {string.Join( " ", args )}" );
		}
		catch ( TargetInvocationException ex )
		{
			Log.Error( $"Exception from RunInternal: {ex.InnerException?.Message ?? ex.Message}" );
		}
		catch ( Exception ex )
		{
			Log.Error( $"Reflection failed: {ex}" );
		}
	}
}
