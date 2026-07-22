namespace Core;

using System;

public abstract partial class GameManagerSystem : GameObjectSystem
{
	public static GameManagerSystem Current { get; set; }

	public GameManagerSystem( Scene scene ) : base( scene )
	{
		Current = this;

		if ( Application.IsStandalone )
			PrepareStandaloneInfo();

		ResetWorldTimeForSceneStart();

		BaseGUIManager.Local?.OnPause += OnGamePaused;
		BaseGUIManager.Local?.OnUnpause += OnGameUnpaused;

		if ( !Scene.IsEditor )
		{
			Listen( Stage.SceneLoaded, 1, OnStart, "GameManager OnStart" );
			Listen( Stage.StartUpdate, 1, OnUpdate, "GameManager OnUpdate" );
			Listen( Stage.StartFixedUpdate, 1, OnFixedUpdate, "GameManager OnFixedUpdate" );
		}
		else
		{
			Listen( Stage.SceneLoaded, 2, OnEditorStart, "GameManager OnStart (Editor)" );
			Listen( Stage.StartUpdate, 2, OnEditorUpdate, "GameManager OnUpdate (Editor)" );
			Listen( Stage.StartFixedUpdate, 2, OnEditorFixedUpdate, "GameManager OnFixedUpdate (Editor)" );
		}

	}

	override public void Dispose()
	{
		if ( Application.IsEditor && Game.IsClosing )
			BaseGUIManager.ClearColdBootPlayState();

		BaseGUIManager.Local?.OnPause -= OnGamePaused;
		BaseGUIManager.Local?.OnUnpause -= OnGameUnpaused;

		IsPaused = false;
		Current = null;
		GC.SuppressFinalize( this ); // the three dots suggest i add it, i dont know why or if its bad or not
		base.Dispose();
	}

	/// <summary>
	/// Which rules are used to determine gameworks
	/// </summary>
	public static GameRules Rules { get; protected set; }
	[Property] public SceneType SceneType { get; protected set; } = SceneType.Game;

	/// <summary>
	/// Set the proper gamerules on start, leave empty if you are 
	/// </summary>
	protected abstract void DecideGameRules();

	protected virtual void OnUpdate()
	{
		UpdateWorldTime();

		if ( Input.Pressed( "Pause" ) ) TogglePause();

		Rules?.GameFrame();

		ApplySceneTimeScale();
		HandleSaveLoadInput();

		DrawAllDebugGizmos();
	}

	protected virtual void OnFixedUpdate() => Rules?.GameTick();

	protected virtual void InitScene() => DontSpawnPlayer = SceneType != SceneType.Game; // We don't want player in menu OR debug

	protected virtual void OnStart()
	{
		InitScene();

		DecideGameRules();
		Rules?.GameStart();

#if IGNIS || STANDALONE
		if ( !SaveSystem.IsRestoringSave )
			ResetWorldTimeForSceneStart();
#endif

		PreSpawn();
		PlayerSpawn();
		PostSpawn();
	}

	/// <summary>
	/// Game was paused
	/// </summary>
	protected virtual void OnGamePaused() { }
	/// <summary>
	/// Game was unpaused
	/// </summary>
	protected virtual void OnGameUnpaused() { }
}

public enum SceneType
{
	/// <summary>
	/// Debug scene, could be used for something
	/// </summary>
	Debug = 0,
	/// <summary>
	/// A menu scene where we don't spawn players
	/// </summary>
	Menu,
	/// <summary>
	/// A game scene, where game happens and the player is spawned
	/// </summary>
	Game
}
