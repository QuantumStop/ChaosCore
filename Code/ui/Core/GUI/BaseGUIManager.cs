namespace Core;
#if IGNIS
using Sandbox.UI.Dev;
#endif
using System;
using XGUI;

/// <summary>
/// Global GUI system that owns the runtime XGUI host, menu overlay lifecycle, input blocking,
/// cold/warm boot state, and loading-overlay hooks used by game-specific GUI managers.
/// </summary>
[Title( "Base GUI Manager" )]
public abstract partial class BaseGUIManager : GameObjectSystem
{
	/// <summary>
	/// Active GUI manager for the current scene.
	/// </summary>
	public static BaseGUIManager Local { get; private set; }

	[ShowIf( nameof( IsMainMenu ), true ), Group( "Boot" ), Property, ReadOnly] protected bool _isColdBoot { get; set; } = false;

	/// <summary>
	/// True once the current cold boot intro has finished and the menu can run its normal intro effects.
	/// </summary>
	[ShowIf( nameof( IsMainMenu ), true ), Group( "Boot" ), Property, ReadOnly] public bool IsColdBootDone { get; set; } = false;

	/// <summary>
	/// True while the in-game menu overlay is open.
	/// </summary>
	[Property, ReadOnly] public bool IsMenuOverlayActive { get; private set; }

	/// <summary>
	/// True when the current scene is the main menu.
	/// </summary>
	[Property, ReadOnly] public bool IsMainMenu => _currentSceneType == SceneType.Menu;

	public CameraComponent MenuCamera { get; set; }

	private GameObject _hostObject;
	private bool _ownsHostObject;
	private bool _lastDevUIWantsInput;
	private int _lastConsoleToggleSerial;
	private bool _waitingForXguiComponentReady;
	private bool _waitingForPanelReady;
	private bool _hasEnsuredXguiRoot;
	private static bool _forceWarmBootOnNextMenu;
	private static bool _hasConsumedColdBootForPlaySession;
	private static bool _lastObservedGamePlaying;
	private static GameObject _sharedXguiHostObject;

	/// <summary>
	/// Name used for the hidden runtime object that hosts XGUI components for this manager.
	/// </summary>
	protected virtual string _hostObjectName => "Base GUI Manager";

	public BaseGUIManager( Scene scene ) : base( scene )
	{
		Local = this;

		Listen( Stage.SceneLoaded, 5, OnStart, "Base GUI Manager OnStart" );
		Listen( Stage.StartUpdate, 5, OnUpdate, "Base GUI Manager OnUpdate" );
	}

	private void OnPanelReady( XGUIRootPanel _ )
	{
		UnsubscribeFromPanelReady();
		Init();
	}

	public override void Dispose()
	{
		_waitingForXguiComponentReady = false;
		UnsubscribeFromPanelReady();

		OnGuiManagerDispose();
		if ( MenuCamera.IsValid() )
			MenuCamera.Destroy();

		if ( _ownsHostObject && _hostObject != _sharedXguiHostObject )
			_hostObject?.Destroy();

		if ( Local == this )
		{
			SetMouseUnlocked( false );
			Local = null;
		}

		GC.SuppressFinalize( this );
		base.Dispose();
	}

	private void OnStart()
	{
		Local = this;

		UpdateColdBootPlayState();

		var canColdBoot = CanColdBootInCurrentLaunch();
		_isColdBoot = IsMainMenu && canColdBoot && !_hasConsumedColdBootForPlaySession && !_forceWarmBootOnNextMenu;
		InvalidateSharedHostIfDetached();

		if ( IsMainMenu && canColdBoot )
		{
			_hasConsumedColdBootForPlaySession = true;
			_forceWarmBootOnNextMenu = false;
		}
#if IGNIS
		_lastConsoleToggleSerial = DeveloperMode.ConsoleToggleSerial;
#endif
		if ( !IsMainMenu )
		{
			CloseOverlay();
			RemoveRuntimeMenuCamera();
		}

		if ( HasValidRootPanel() )
		{
			EnsureMenuCamera();
			Init();
			return;
		}

		EnsureXguiRootComponent();
		EnsureMenuCamera();

		if ( HasXguiRootComponent() )
			SubscribeToPanelReady();
		else
			SubscribeToXguiComponentReady();
	}

	private void OnUpdate()
	{
		if ( Local != this )
			return;

		UpdateColdBootPlayState();
		OnGuiManagerUpdate();

		UpdateXguiComponentReady();

		if ( _waitingForXguiComponentReady || _waitingForPanelReady )
			return;

		if ( ToggleRequested() && !IsMainMenu )
			ToggleOverlay();
#if IGNIS
		UpdateDevUIState();
#endif
		ApplyOverlayState();
	}

	/// <summary>
	/// Per-frame hook for derived managers before GUI readiness can early-out the base update.
	/// </summary>
	protected virtual void OnGuiManagerUpdate() { }

	/// <summary>
	/// Current game scene classification reported by the game manager.
	/// </summary>
	protected SceneType _currentSceneType => GameManagerSystem.Current?.SceneType ?? SceneType.Debug;

	/// <summary>
	/// Forces the next menu scene entry to use warm boot behavior instead of the cold boot intro.
	/// </summary>
	public static void MarkReturningToMenu() => _forceWarmBootOnNextMenu = true;

	/// <summary>
	/// Resets static cold boot tracking, mainly for editor play-state transitions.
	/// </summary>
	public static void ClearColdBootPlayState()
	{
		_hasConsumedColdBootForPlaySession = false;
		_forceWarmBootOnNextMenu = false;
		_lastObservedGamePlaying = false;
	}

	private static void UpdateColdBootPlayState()
	{
		if ( Game.IsPlaying )
		{
			if ( !_lastObservedGamePlaying )
			{
				_hasConsumedColdBootForPlaySession = false;
				_forceWarmBootOnNextMenu = false;
			}
		}
		else
		{
			ClearColdBootPlayState();
			return;
		}

		_lastObservedGamePlaying = Game.IsPlaying;
	}

	private static bool CanColdBootInCurrentLaunch()
	{
		if ( !Game.IsPlaying )
			return false;

		if ( !Application.IsEditor )
			return true;
#if IGNIS || STANDALONE
		return IsEditorGameModePlay();
#endif
		return false;
	}
#if IGNIS || STANDALONE
	private static bool IsEditorGameModePlay()
	{
		foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
		{
			var editorSceneType = assembly.GetType( "Editor.EditorScene", false );
			if ( editorSceneType is null )
				continue;

			var playModeProperty = editorSceneType.GetProperty( "PlayMode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static );
			return playModeProperty?.GetValue( null ) is true;
		}

		return false;
	}
#endif
	private void EnsureMenuCamera()
	{
		if ( !IsMainMenu )
		{
			RemoveRuntimeMenuCamera();
			return;
		}

		if ( HasMainCamera( false ) )
		{
			RemoveRuntimeMenuCamera();
			return;
		}

		if ( HasMainCamera() )
			return;

		_hostObject ??= ResolveHostObject();

		var camera = _hostObject.Components.GetOrCreate<CameraComponent>();
		camera.IsMainCamera = true;
		camera.FieldOfView = 60f;
		camera.EnablePostProcessing = true;

		MenuCamera = camera;
	}

	private void RemoveRuntimeMenuCamera()
	{
		var camera = MenuCamera.IsValid()
			? MenuCamera
			: IsUsableSharedHost()
				? _sharedXguiHostObject.Components.Get<CameraComponent>()
				: null;

		if ( !camera.IsValid() )
			return;

		camera.Destroy();
		MenuCamera = null;
	}

	private bool HasMainCamera( bool includeRuntimeHost = true )
	{
		foreach ( var camera in Scene.GetAllComponents<CameraComponent>() )
		{
			if ( !camera.IsValid() || !camera.IsMainCamera )
				continue;

			if ( !includeRuntimeHost && IsUsableSharedHost() && camera.GameObject == _sharedXguiHostObject )
				continue;

			if ( !includeRuntimeHost && _hostObject.IsValid() && camera.GameObject == _hostObject )
				continue;

			return true;
		}

		return false;
	}

	/// <summary>
	/// Sets the XGUI root component mouse unlock state when the GUI owns input.
	/// </summary>
	protected void SetMouseUnlocked( bool mouseUnlocked )
	{
		var component = GetXguiSystem()?.Component;
		if ( !component.IsValid() )
			return;

		component.MouseUnlocked = mouseUnlocked;
	}

	/// <summary>
	/// Gets or creates a component on the hidden GUI host object.
	/// </summary>
	protected T GetOrCreateHostComponent<T>() where T : Component, new()
	{
		_hostObject ??= ResolveHostObject();
		return _hostObject.Components.GetOrCreate<T>();
	}

	private void EnsureXguiRootComponent()
	{
		if ( _hasEnsuredXguiRoot )
			return;

		_hasEnsuredXguiRoot = true;

		if ( HasValidRootPanel() )
			return;

		if ( HasXguiRootComponent() )
			return;

		if ( IsUsableSharedHost() )
		{
			_hostObject = _sharedXguiHostObject;
			_hostObject.Components.GetOrCreate<XGUIRootComponent>();
			return;
		}

		GetOrCreateHostComponent<XGUIRootComponent>();
	}

	private bool HasXguiRootComponent()
	{
		var host = FindXguiRootHost();
		if ( host.IsValid() )
		{
			_hostObject ??= host;
			return true;
		}

		return false;
	}

	private GameObject FindXguiRootHost()
	{
		foreach ( var component in Scene.GetAllComponents<XGUIRootComponent>() )
		{
			if ( component.GameObject.IsValid() )
				return component.GameObject;
		}

		return null;
	}

	private GameObject ResolveHostObject() => FindHostObject() ?? FindXguiRootHost() ?? CreateHostObject();

	private GameObject FindHostObject()
	{
		if ( IsUsableSharedHost() )
			return _sharedXguiHostObject;

		foreach ( var gameObject in Scene.GetAllObjects( true ) )
		{
			if ( gameObject.Name != _hostObjectName )
				continue;

			if ( _hostObject.IsValid() && gameObject == _hostObject )
				continue;

			return gameObject;
		}

		return null;
	}

	private GameObject CreateHostObject()
	{
		var host = Scene.CreateObject();
		host.Name = _hostObjectName;
		host.Flags |= GameObjectFlags.Hidden | GameObjectFlags.NotSaved | GameObjectFlags.DontDestroyOnLoad;
		host.Tags.Add( "ui" );
		_sharedXguiHostObject = host;
		_ownsHostObject = true;
		return host;
	}

	private void InvalidateSharedHostIfDetached()
	{
		if ( !_sharedXguiHostObject.IsValid() )
			return;

		if ( _sharedXguiHostObject.Scene == Scene )
			return;

		_sharedXguiHostObject = null;
		if ( _hostObject.IsValid() && _hostObject.Scene != Scene )
			_hostObject = null;
	}

	private bool IsUsableSharedHost() => _sharedXguiHostObject.IsValid() && _sharedXguiHostObject.Scene == Scene;


	private bool HasValidRootPanel()
	{
		var rootPanel = GetXguiSystem()?.Panel;
		return rootPanel.IsValid();
	}

	private void SubscribeToXguiComponentReady() => _waitingForXguiComponentReady = true;

	private void UpdateXguiComponentReady()
	{
		if ( !_waitingForXguiComponentReady )
			return;

		if ( !HasXguiRootComponent() )
			return;

		_waitingForXguiComponentReady = false;

		if ( HasValidRootPanel() )
			Init();
		else
			SubscribeToPanelReady();
	}

	private void SubscribeToPanelReady()
	{
		if ( _waitingForPanelReady )
			return;

		var system = GetXguiSystem();
		if ( system is null )
			return;

		system.OnPanelReady += OnPanelReady;
		_waitingForPanelReady = true;
	}

	private void UnsubscribeFromPanelReady()
	{
		if ( !_waitingForPanelReady )
			return;

		var system = GetXguiSystem();
		if ( system is not null )
			system.OnPanelReady -= OnPanelReady;

		_waitingForPanelReady = false;
	}

	/// <summary>
	/// Cleanup hook for derived GUI managers before the shared host and XGUI state are released.
	/// </summary>
	protected virtual void OnGuiManagerDispose() { }

	/// <summary>
	/// Returns the scene's XGUI system, if one exists.
	/// </summary>
	protected XGUISystem GetXguiSystem() => Scene?.GetSystem<XGUISystem>();
}
