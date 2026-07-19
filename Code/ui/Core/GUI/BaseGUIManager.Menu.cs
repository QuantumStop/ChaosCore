namespace Core;

public abstract partial class BaseGUIManager
{
	private void Init()
	{
		switch ( _currentSceneType )
		{
			case SceneType.Menu:
				MenuSetup();
				break;

			case SceneType.Game:
				OnGuiReady( false );
				break;

			default:
				OnGuiReady( false );
				break;
		}
	}

	private void MenuSetup()
	{
		OnMenuSetup();
		OnGuiReady( true );
	}

	/// <summary>
	/// Returns true when XGUI's root panel is available; subscribes for readiness if it is not.
	/// </summary>
	protected bool HasRootPanel()
	{
		if ( !(GetXguiSystem()?.Panel).IsValid() )
		{
			SubscribeToPanelReady();
			return false;
		}

		SetMouseUnlocked( false );
		return true;
	}

	/// <summary>
	/// Called when entering a menu scene before the GUI is marked ready.
	/// </summary>
	protected virtual void OnMenuSetup() { }

	/// <summary>
	/// Called when the XGUI root is ready for either menu or in-game overlay setup.
	/// </summary>
	protected virtual void OnGuiReady( bool isMenu )
	{
		if ( !HasRootPanel() )
			return;

		if ( isMenu ) IsColdBootDone = false;
	}
}
