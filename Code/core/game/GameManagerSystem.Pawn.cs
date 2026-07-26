using System;

namespace Core;

partial class GameManagerSystem : GameObjectSystem, Component.INetworkListener
{
	private GameObject CreateClientObject( Connection connection, out Client client )
	{
		var clientObj = new GameObject { Name = $"{connection.DisplayName} [CLIENT]" };

		client = clientObj.Components.Create<Client>();
		client.SteamId = connection.SteamId;
		client.SteamName = connection.DisplayName;
		clientObj.Network.SetOrphanedMode( NetworkOrphaned.ClearOwner );

		return clientObj;
	}

	private Client GetOrCreateClient( Connection channel )
	{
		Client possibleClient = Scene.GetAllComponents<Client>().FirstOrDefault( x => x.Connection is null && x.SteamId == channel.SteamId );

		if ( possibleClient.IsValid() ) return possibleClient;

		CreateClientObject( channel, out Client client );

		if ( !client.IsValid() ) return null;

		return client;
	}

	void Component.INetworkListener.OnActive( Connection channel ) => OnActive( channel );
	void Component.INetworkListener.OnDisconnected( Connection channel ) => OnDisconnected( channel );

	public virtual void OnActive( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' has finished loading!" );

		var Client = GetOrCreateClient( channel );

		if ( !Client.IsValid() ) throw new Exception( $"Something went wrong when trying to create Client for {channel.DisplayName}" );

		OnPlayerJoined( Client, channel );
	}

	private static void OnPlayerJoined( Client client, Connection channel )
	{
		if ( !client.Network.Active ) client.GameObject.NetworkSpawn( channel ); // network spawn it so we have a "local" owner 
		else client.Network.AssignOwnership( channel );

		client.HostInit();
	}

	protected virtual void OnDisconnected( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' is disconnecting" );
		var cl = Scene.GetAllComponents<Client>().FirstOrDefault( x => x.Connection == channel );

		if ( !cl.IsValid() )
		{
			Log.Warning( $"No Client found for {channel.DisplayName}" );
		}
	}
}
