namespace Core;

using System;

public abstract partial class GameManagerSystem
{
	[ConVar( "host_timescale", ConVarFlags.Replicated, Help = "Affects the scale of time, making things faster or slower." )]
	public static float TimeScale { get; set; } = 1f;

	private bool _playerLockedForPause;

	[Property, ReadOnly, Feature( "Debug" )] public float CurrentWorldTime { get; private set; }
	[Property, ReadOnly, Feature( "Debug" )] public float CurrentWorldDelta { get; private set; }
	[Property, ReadOnly, Feature( "Debug" )] public float WorldTimeOffset { get; private set; }

	private static bool _pauseRequested { get; set; }
	private bool _isWorldTimeInitialized { get; set; }
	private bool _hasWorldTimeUpdate;

	public static float WorldNow => GetWorldNow();
	public static float WorldDelta => HasActiveWorldTimeManager() ? Current.CurrentWorldDelta : 0f;

	private static float GetWorldNow()
	{
		// Keeps world time continuous across save loads by using saved time during loading,
		// then binding an offset to our clock on the very first runtime update.
		if ( !HasActiveWorldTimeManager() )
		{
			return SaveSystem.TryGetStagedWorldTime(
				out var stagedWorldTime
			)
				? stagedWorldTime
				: Current?._isWorldTimeInitialized == true ? Current.CurrentWorldTime : 0f;
		}

		if ( !Current._hasWorldTimeUpdate )
			return Current.CurrentWorldTime;

		return MathF.Max(
			0f,
			Time.Now + Current.WorldTimeOffset
		);
	}

	private static bool HasActiveWorldTimeManager() => Current?._isWorldTimeInitialized == true && ReferenceEquals( Current.Scene, Game.ActiveScene );

	public void SetWorldTime( float time )
	{
		CurrentWorldTime = MathF.Max( 0f, time );
		WorldTimeOffset = _hasWorldTimeUpdate ? CurrentWorldTime - Time.Now : 0f;
		CurrentWorldDelta = 0f;
		_isWorldTimeInitialized = true;
	}

	private void ResetWorldTimeForSceneStart()
	{
		_hasWorldTimeUpdate = false;
		if ( SaveSystem.TryGetStagedWorldTime( out var stagedWorldTime ) )
		{
			SetWorldTime( stagedWorldTime );
			return;
		}

		SetWorldTime( 0f );
	}

	protected void UpdateWorldTime()
	{
		// Bind startup/save time to the runtime clock, never the loading clock.
		if ( !_hasWorldTimeUpdate )
		{
			WorldTimeOffset = CurrentWorldTime - Time.Now;
			CurrentWorldDelta = 0f;
			_hasWorldTimeUpdate = true;
			return;
		}

		var previousWorldTime = CurrentWorldTime;

		CurrentWorldTime = MathF.Max( 0f, Time.Now + WorldTimeOffset );
		CurrentWorldDelta = MathF.Max( 0f, CurrentWorldTime - previousWorldTime );
	}

	protected void ApplySceneTimeScale()
	{
		var paused = IsPaused;
		Scene.TimeScale = Rules.ShouldPause && paused ? 0f : TimeScale;
		ApplyPlayerPauseLock( paused );
	}

	private void ApplyPlayerPauseLock( bool paused )
	{
		var player = BasePlayer.Local;
		if ( !player.IsValid() || !player.Controller.IsValid() )
			return;

		if ( paused )
		{
			if ( _playerLockedForPause || !player.Controller.AllowMovement )
				return;

			player.LockPlayer( true );
			_playerLockedForPause = true;
			return;
		}

		if ( !_playerLockedForPause )
			return;

		player.UnlockPlayer( true );
		_playerLockedForPause = false;
	}

	/// <summary>
	/// Game.IsPaused but actual real pause
	/// </summary>
	public static bool IsPaused
	{
		get => ShouldBePaused();
		set
		{
			if ( value && BaseGUIManager.Local?.IsMenuOverlayActive == true )
				return;

			_pauseRequested = value;
		}
	}

	public static bool ShouldBePaused()
	{
		if ( BaseGUIManager.Local?.IsMenuOverlayActive == true )
			return true;

		if ( _pauseRequested )
			return true;

		return false;
	}


	[ConCmd( "pause", Help = "Pause the game" )]
	public static bool TogglePause()
	{
		// Make sure we don't overstep
		if ( BaseGUIManager.Local?.IsMenuOverlayActive == true )
			return true;

		_pauseRequested = !_pauseRequested;
		return _pauseRequested;
	}
}
