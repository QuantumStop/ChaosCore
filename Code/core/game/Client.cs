namespace Core;

public partial class Client : Component, Component.INetworkListener
{
	/// <summary>
	/// The player we're currently in the view of (clientside).
	/// Usually the local player, apart from when spectating etc.
	/// </summary>
	public static Client Viewer { get; private set; }

	/// <summary>
	/// Our local player on this client.
	/// </summary>
	public static Client Local { get; private set; }

	/// <summary>
	/// Who owns this player state?
	/// </summary>
	[Sync( SyncFlags.FromHost )] public ulong SteamId { get; set; }

	/// <summary>
	/// The player's name, which might have to persist if they leave
	/// </summary>
	[Sync( SyncFlags.FromHost )] public string SteamName { get; set; }

	/// <summary>
	/// The connection of this player
	/// </summary>
	public Connection Connection => Network.Owner;

	/// <summary>
	/// Is this player connected? Clients can linger around in competitive matches to keep a player's slot in a team if they disconnect.
	/// </summary>
	public bool IsConnected => Connection is not null && (Connection.IsActive || Connection.IsHost); // smh

	/// <summary>
	/// Are we in the view of this player (clientside)
	/// </summary>
	[Property, ReadOnly] public bool IsViewer => Viewer == this;

	/// <summary>
	/// Is this the local player for this client
	/// </summary>
	[Property, ReadOnly] public bool IsLocalPlayer => !IsProxy && Connection == Connection.Local;

	/// <summary>
	/// The main Pawn of this player if one exists, will not change when the player possesses other pawns
	/// </summary>
	[Sync( SyncFlags.FromHost )] public BasePlayer MainPawn { get; set; }

	/// <summary>
	/// The pawn this player is currently in possession of (synced - unless the pawn is not networked)
	/// </summary>
	[Sync] public BasePawn Pawn { get; set; }

	protected override void OnUpdate() => DisconnectCleanupCheck();

	/// <summary>
	/// Initialize the client for the host only
	/// </summary>
	public void HostInit()
	{
		SteamId = Connection.SteamId;
		SteamName = Connection.DisplayName;
	}

	/// <summary>
	/// Initialize the client locally for that client only
	/// </summary>
	[Rpc.Owner] public void ClientInit() => Local = this;

	/// <summary>
	/// This client possessed some pawn, consider this an "event" of sorts
	/// </summary>
	/// <param name="pawn">Some pawn in question</param>
	public static void OnPossess( BasePawn pawn )
	{
		if ( !pawn.IsValid() )
		{
			Log.Warning( "Tried to possess an invalid pawn." );
			return;
		}

		if ( !Local.IsValid() )
		{
			Log.Warning( "Tried to possess a pawn but we don't have a local Client" );
			return;
		}

		Local.Pawn = pawn;

		if ( pawn.Network.Active ) Local.OnPossessRpc(); // only networked pawns, in cases when there are ones that aren't

		Viewer = pawn.Owner.IsValid() ? pawn.Owner : Local;
	}

	/// <summary>
	/// Sync to other clients what this player is currently possessing
	/// </summary>
	[Rpc.Broadcast]
	private void OnPossessRpc()
	{
		if ( IsViewer && IsProxy )
		{
			if ( !Pawn.IsValid() || IsLocalPlayer )
			{
				if ( MainPawn.IsValid() )
				{
					// Local player - always assume the main controller pawn
					MainPawn.Possess();
				}
			}
			else
			{
				// A remote player is possessing this player (spectating)
				// So enter the latest known pawn this player has possessed
				Pawn.Possess();
			}
		}
	}

	/// <summary>
	/// How long has it been since this player has disconnected
	/// </summary>
	private RealTimeSince _timeSinceDisconnected { get; set; }

	/// <summary>
	/// How long does it take to clean up a player once they disconnect?
	/// </summary>
	private const float _disconnectCleanupTime = 120f;

	void INetworkListener.OnDisconnected( Connection channel ) { if ( Connection == channel ) _timeSinceDisconnected = 0; }

	private void DisconnectCleanupCheck()
	{
		if ( IsConnected ) return;
		if ( IsProxy ) return;

		if ( _timeSinceDisconnected > _disconnectCleanupTime ) GameObject.Destroy();
	}

	static public Client GetFromConnection( Connection connection )
	{
		if ( connection is null ) return null;
		return Game.ActiveScene.GetAll<Client>().FirstOrDefault( x => x.Connection == connection );
	}
}
