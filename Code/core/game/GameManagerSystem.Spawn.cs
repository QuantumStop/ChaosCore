
namespace Core;

using Sandbox.Utility;

public abstract partial class GameManagerSystem : GameObjectSystem
{
	/// <summary>
	/// The thing you should set
	/// </summary>
	[Property, ReadOnly] public static bool UsePlayerStart { get; set; } = true;
	/// <summary>
	/// UsePlayerStart but with tools check on top, set the above and check for the below
	/// because we dont want to know where to spawn when in the real game, we just spawn
	/// </summary>
	/// <returns>Spawn with this rule</returns>
	public static bool UsePlayerStartRule => !Application.IsEditor || (Application.IsEditor && UsePlayerStart);
	/// <summary>
	/// Use this if you dont want for player to spawn (menu scene, debug scene, etc)
	/// </summary>
	public bool DontSpawnPlayer { get; set; } = false;
	/// <summary>
	/// Called before Player is spawned in, usually to spawn additional managers
	/// </summary>
	protected virtual void PreSpawn()
	{
		TimeScale = 1f; // reset timescale
		CurrentWorldDelta = 0f;

		if ( !Scene.GetAllComponents<DebrisManager>().Any() )
			Scene.CreateObject().Components.GetOrCreate<DebrisManager>();

		Parallel.ForEach( Scene.GetAll<BaseEntity>(), x => x.OnStartOnceInternal() );

		if ( Rules is null )
		{ Log.Error( "No GameRules were given!" ); return; }

		// check if this is a level transition
		if ( Rules.CanTransition ) ExitLevelTransition();
	}

	/// <summary>
	/// Called after Player is spawned in, usually to decide spawn items
	/// </summary>
	protected virtual void PostSpawn() { }

	public GameObject Player { get; set; }

	/// <summary>
	/// Used to spawn the player, do not call the base class in the override
	/// </summary>
	protected virtual void PlayerSpawn()
	{
		if ( DontSpawnPlayer || !Scene.IsValid() )
			return;
#if IGNIS || STANDALONE
		// Save loads restore the saved player root directly, so don't spawn a fresh prefab on top of it.
		if ( SaveSystem.HasStagedSavedRoot( "LocalPlayer" ) )
			return;
#endif
		var playerPrefab = GameObject.GetPrefab( "prefabs/player.prefab" );

		if ( !playerPrefab.IsValid() )
		{
			Log.Error( "Could not find player prefab: prefabs/player.prefab" );
			return;
		}

		//	otherwise spawn player at editor camera position
		SceneTraceResult tr = Scene.Trace.Ray( LastEditorCameraPosition.Position, LastEditorCameraPosition.Position - Vector3.Up * 64f ).Run();

		Player = playerPrefab.Clone();

		Player.WorldPosition = Application.IsEditor ? LastEditorCameraPosition.Position - Vector3.Up * 64f * tr.Fraction : Vector3.Zero;

		if ( Player.Components.TryGet<BasePlayer>( out var playerComponent ) )
		{
			playerComponent.Controller.EyeAngles = Application.IsEditor ? LastEditorCameraPosition.Rotation : Angles.Zero;
			playerComponent.Controller.Controller.Velocity = Vector3.Zero;
			playerComponent.Controller.Controller.BaseVelocity = Vector3.Zero;
		}
	}
}
