namespace Core;

/// <summary>
/// Describes why a scene was loaded.
/// </summary>
public enum SceneLoadType
{
	/// <summary>
	/// The scene was loaded to start a new game.
	/// </summary>
	SceneLoad_NewGame = 0,

	/// <summary>
	/// The scene was restored from a saved game.
	/// </summary>
	SceneLoad_LoadGame,

	/// <summary>
	/// The scene was entered through a level transition.
	/// </summary>
	SceneLoad_Transition,

	/// <summary>
	/// The scene was loaded as 3D scenery behind the main menu.
	/// </summary>
	SceneLoad_Background
}

/// <summary>
/// Receives notifications about the scene lifecycle.
/// </summary>
public interface ISceneLifecycleEvents : ISceneEvent<ISceneLifecycleEvents>
{
	/// <summary>
	/// Called once the scene is ready after startup or after being restored.
	/// </summary>
	void OnSceneReady( SceneLoadType loadType ) { }
}
