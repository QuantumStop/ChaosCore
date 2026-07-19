namespace Core;
#if IGNIS
using Sandbox.UI.Dev;
#endif
using System;
using System.Threading.Tasks;

public abstract partial class BaseGUIManager
{
	/// <summary>
	/// Opens or closes the overlay depending on its current state.
	/// </summary>
	protected virtual void ToggleOverlay()
	{
		if ( IsOverlayActive() )
			CloseOverlay();
		else
			ShowOverlay();
	}
#if IGNIS
	private void UpdateDevUIState()
	{
		var devUIWantsInput = DeveloperMode.Open && DeveloperMode.Focused;

		if ( devUIWantsInput == _lastDevUIWantsInput )
			return;

		if ( devUIWantsInput )
			ShowOverlayForDevUI();
		else if ( WasConsoleToggled() )
			CloseOverlay();

		_lastDevUIWantsInput = devUIWantsInput;
		_lastConsoleToggleSerial = DeveloperMode.ConsoleToggleSerial;
	}


	protected bool WasConsoleToggled() => DeveloperMode.ConsoleToggleSerial != _lastConsoleToggleSerial;
#endif
	/// <summary>
	/// Returns whether this manager currently considers its menu overlay open.
	/// </summary>
	protected bool IsOverlayActive() => IsMenuOverlayActive;
#if IGNIS
	/// <summary>
	/// Returns whether the developer console is currently taking input.
	/// </summary>
	protected static bool ConsoleWantsInput() => DeveloperMode.Open && DeveloperMode.Focused;
#endif
	/// <summary>
	/// Returns whether game input should be blocked by GUI or console state.
	/// </summary>
#if IGNIS
	public bool BlocksInput() => OverlayBlocksInput() || ConsoleWantsInput();
#else
	public bool BlocksInput() => OverlayBlocksInput();
#endif
	/// <summary>
	/// Returns whether this manager's overlay should block game input.
	/// </summary>
	protected bool OverlayBlocksInput() => IsMenuOverlayActive;

	/// <summary>
	/// Applies input and focus side effects for the current overlay state.
	/// </summary>
	protected virtual void ApplyOverlayState() => SetMouseUnlocked( BlocksInput() );

	/// <summary>
	/// Opens the overlay, prepares its panel, clears input, and gives it focus.
	/// </summary>
	protected virtual void ShowOverlay()
	{
		if ( IsMenuOverlayActive )
			return;

		if ( !TryPrepareOverlay( IsMainMenu ) )
			return;

		IsMenuOverlayActive = true;

		OnPause?.Invoke();

		Input.ClearActions();
		FocusOverlay();

		ApplyOverlayState();
	}

	/// <summary>
	/// Game was paused
	/// </summary>
	public Action OnPause { get; set; }

	/// <summary>
	/// Game was unpaused
	/// </summary>
	public Action OnUnpause { get; set; }

	/// <summary>
	/// Opens the overlay as a visual/input companion for the developer console without stealing console focus.
	/// </summary>
	private void ShowOverlayForDevUI()
	{
		if ( IsMenuOverlayActive )
			return;

		if ( !TryPrepareOverlay( IsMainMenu ) )
			return;

		IsMenuOverlayActive = true;

		ApplyOverlayState();
	}

	/// <summary>
	/// Closes the overlay and releases overlay focus/input ownership.
	/// </summary>
	protected virtual void CloseOverlay()
	{
		if ( !IsMenuOverlayActive )
			return;

		IsMenuOverlayActive = false;

		OnUnpause?.Invoke();

		FocusOverlayClosed();
		ApplyOverlayState();
	}

	/// <summary>
	/// Resumes gameplay from an overlay action and closes the developer console if it still owns input.
	/// </summary>
	protected void ResumeFromOverlay()
	{
		CloseOverlay();
#if IGNIS
		if ( DeveloperMode.Open && DeveloperMode.Focused )
			DeveloperMode.DevUI = 0;
#endif
	}

	/// <summary>
	/// Called before a scene load starts so derived managers can close transient GUI state.
	/// </summary>
	public virtual void PrepareForSceneLoad() => CloseOverlay();

	/// <summary>
	/// Enters a loading overlay state before expensive scene load work begins.
	/// </summary>
	public virtual Task EnterLoadingOverlayAsync() => Task.CompletedTask;

	/// <summary>
	/// Provides the scene about to be loaded so loading overlays can prepare contextual metadata.
	/// </summary>
	public virtual void SetLoadingOverlayScene( SceneFile scene )
	{
	}

	/// <summary>
	/// Provides the scene path about to be loaded so loading overlays can prepare contextual metadata.
	/// </summary>
	public virtual void SetLoadingOverlayScene( string scenePath )
	{
	}

	/// <summary>
	/// Cancels any in-progress loading overlay state.
	/// </summary>
	public virtual void CancelLoadingOverlay()
	{
	}

	/// <summary>
	/// Returns true when the user requested an overlay toggle this frame.
	/// </summary>
	protected virtual bool ToggleRequested() => Input.EscapePressed;

	/// <summary>
	/// Ensures the overlay panel exists and is ready for the requested menu/game context.
	/// </summary>
	protected virtual bool TryPrepareOverlay( bool isMenu ) => HasRootPanel();

	/// <summary>
	/// Gives focus to the overlay after it opens.
	/// </summary>
	protected virtual void FocusOverlay() { }

	/// <summary>
	/// Called after overlay focus is released.
	/// </summary>
	protected virtual void FocusOverlayClosed() { }
}
