namespace Core;

public abstract partial class GameManagerSystem
{
	[Property, ReadOnly] public HashSet<Client> Clients { get; set; } = [];

	private BasePlayer _singlePlayer;
	private float _nextSinglePlayerLookupTime;

	/// <summary>
	/// Track a client belonging to this active session.
	/// </summary>
	public void RegisterClient( Client client )
	{
		if ( !client.IsValid() || client.Scene != Scene )
			return;

		Clients.Add( client );
	}

	/// <summary>
	/// Stop tracking a client if for example a client left/was removed from the session.
	/// </summary>
	public void UnregisterClient( Client client ) => Clients.Remove( client );

	/// <summary>
	/// Find the closest connected player's main pawn, or the SinglePlayer pawn.
	/// </summary>
	public BasePlayer GetNearestPlayer( Vector3 position )
	{
		if ( Rules?.IsOnline != true )
			return GetSPPlayer();

		BasePlayer nearest = null;
		var nearestDistance = float.MaxValue;

		foreach ( var client in Clients )
		{
			if ( !client.IsValid() || !client.IsConnected )
				continue;

			var player = client.MainPawn;
			if ( !player.IsValid() || player.Scene != Scene )
				continue;

			var distance = player.WorldPosition.DistanceSquared( position );
			if ( distance >= nearestDistance )
				continue;

			nearest = player;
			nearestDistance = distance;
		}

		return nearest;
	}

	private BasePlayer GetSPPlayer()
	{
		var local = BasePlayer.Local;
		
		if ( local.IsValid() )
			return local;
		
		// If for whatever reason our Player was init even slightly late, 
		// then let's do a periodic lookup until they're around

		if ( _singlePlayer.IsValid() )
			return _singlePlayer;

		if ( CurrentWorldTime < _nextSinglePlayerLookupTime )
			return null;

		_nextSinglePlayerLookupTime = CurrentWorldTime + 0.2f;
		_singlePlayer = Scene.GetAllComponents<BasePlayer>().FirstOrDefault( player => player.IsValid() );
		return _singlePlayer;
	}
}
