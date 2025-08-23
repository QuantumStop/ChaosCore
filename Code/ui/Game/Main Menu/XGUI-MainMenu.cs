using Sandbox.UI;
using XGUI;
using chaoscore.Menu;

public class XGUI_MainMenu : BaseEntity
{
	public static XGUI_MainMenu Local { get; set; }

	[Property, ReadOnly] public Panel Panel { get; private set; } = null;
	[Property, Hide] public XGUISystem xguiSystem => Scene.GetSystem<XGUISystem>();

	[Property, ReadOnly] public SceneFile SelectedLevel => _SelectedLevel;
	public static GameChaptersDefinition GameChapters { get; set; }

	private static SceneFile _SelectedLevel { get; set; }

	protected override void OnStart()
	{
		Local = this;

		GameChapters = ResourceLibrary.Get<GameChaptersDefinition>("scripts/chaos_chapters.chptdef");

		var mainMenuPanel = new Main_Menu
		{
			MenuLogic = this
		};

		CreatePanel( mainMenuPanel );

		if ( this.xguiSystem.Component == null )
			return;

		xguiSystem.Component.MouseUnlocked = true;
	}

	// Alongside creating panels we can do other logic and play sounds, etc
	public void StartNewGame()
	{
		var lvlselect = new Level_Select();

		CreatePanel( lvlselect );
		lvlselect.FocusWindow();
		Sound.Play( "ui_click" );
	}

	public void LoadGame()
	{
		Log.Info( "Loading game..." );
		Sound.Play( "ui_click" );
	}

	public void OpenOptions()
	{
		Log.Info( "Opening options..." );
		Sound.Play( "ui_click" );
	}

	public void QuitGame()
	{
		Log.Info( "Quitting..." );
		Sound.Play( "ui_click" );
		Game.Close(); // close the game without confirmation (for now)
	}

	public void CreatePanel( Panel _panel )
	{
		xguiSystem.Panel.AddChild( _panel );
	}

	public void SetLevelToLoad( SceneFile NewSelectedLevel )
	{
		_SelectedLevel = NewSelectedLevel;
	}

	public void ClearCurrentLevel()
	{
		_SelectedLevel = null;
	}

	public void LoadCurrentLevel()
	{
		Log.Info( _SelectedLevel );

		var sceneLoad = new SceneLoadOptions();

		// sceneLoad.ShowLoadingScreen = false;
		sceneLoad.SetScene( _SelectedLevel );

		Scene.Load( sceneLoad );
	}
}
