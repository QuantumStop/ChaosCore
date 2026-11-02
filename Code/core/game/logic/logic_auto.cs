
namespace Core;

/// <summary>
/// Logic that runs immediately after certain game event. Usually immediately when a map starts.
/// </summary>
[Icon( "start" )]
[Category( "Logic" )]
public class logic_auto : BaseEntity, ISceneLifecycleEvents
{
	public new delegate void ChaosOutput( logic_auto activator );

	/// <summary>
	/// Fired shortly after the the scene starts (including loading saves).
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnMapSpawn { get; set; }

	/// <summary>
	/// Fired when the map is loaded to start a new game.
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnNewGame { get; set; }

	/// <summary>
	/// Fired when the map is loaded from a saved game.
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnLoadGame { get; set; }

	/// <summary>
	/// Fired when the map is loaded due to a level transition.
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnMapTransition { get; set; }

	void ISceneLifecycleEvents.OnSceneReady( SceneLoadType loadType )
	{
		if ( !this.IsValid() )
			return;

		switch ( loadType )
		{
			case SceneLoadType.SceneLoad_NewGame:
				OnNewGame?.Invoke( this );
				break;
			case SceneLoadType.SceneLoad_LoadGame:
				OnLoadGame?.Invoke( this );
				break;
			case SceneLoadType.SceneLoad_Transition:
				OnMapTransition?.Invoke( this );
				break;
		}

		OnMapSpawn?.Invoke( this );
	}
}
