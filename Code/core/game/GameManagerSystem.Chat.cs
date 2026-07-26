namespace Core;

using Sandbox.Network;
using Sandbox.Platform;

public abstract partial class GameManagerSystem : GameObjectSystem, IChatEvent
{
	public ChatHistory ChatHistory { get; } = new();

	[ConCmd( "say", Help = "Send a chat message." )]
	public static void Say( string message )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return;

		if ( !Chat.Enabled )
		{
			Log.Warning( "Chat is disabled." );
			return;
		}

		Chat.Say( message );
	}

	void IChatEvent.OnChatMessage( ChatMessageEvent e )
	{
		if ( !Networking.IsHost )
			return;

		if ( e.Sender is null )
		{
			AcceptChatMessage( "i", e.Message, extraClass: "system" );
			return;
		}

		AcceptChatMessage( e.Sender.DisplayName, e.Message, (long)e.Sender.SteamId, channel: "game" );
	}

	public static void AcceptChatMessage( string name, string message, long steamId = 0, string channel = "", string extraClass = "" )
	{
		if ( !Networking.IsHost )
			return;

		var entry = Current?.ChatHistory.AddMessage( name, message, steamId, channel, extraClass );
		if ( entry is null || string.IsNullOrWhiteSpace( entry.Value.Message ) )
			return;

		ReceiveChatEntry( entry.Value.Id, entry.Value.Name, entry.Value.Message, entry.Value.SteamId, entry.Value.ExtraClass, entry.Value.Channel, entry.Value.CreatedAt );
	}

	public static void SendChatHistorySnapshot( Connection target )
	{
		if ( !Networking.IsHost || !target.IsActive )
			return;

		BeginChatHistorySnapshot( target.SteamId );

		foreach ( var entry in Current?.ChatHistory.Entries ?? [] )
			SendChatHistorySnapshotEntry( target.SteamId, entry.Id, entry.Name, entry.Message, entry.SteamId, entry.ExtraClass, entry.Channel, entry.CreatedAt );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void ReceiveChatEntry( int id, string name, string message, long steamId, string extraClass, string channel, float createdAt )
	{
		Current?.ChatHistory.AddEntry( new ChatEntry( id, name, message, steamId, extraClass, channel, createdAt ) );
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void BeginChatHistorySnapshot( ulong targetSteamId )
	{
		if ( Connection.Local.SteamId != targetSteamId )
			return;

		Current?.ChatHistory.Clear();
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	static void SendChatHistorySnapshotEntry( ulong targetSteamId, int id, string name, string message, long steamId, string extraClass, string channel, float createdAt )
	{
		if ( Connection.Local.SteamId != targetSteamId )
			return;

		Current?.ChatHistory.AddEntry( new ChatEntry( id, name, message, steamId, extraClass, channel, createdAt ) );
	}

}
