namespace Core;

using System;

public abstract partial class GameManagerSystem
{
	private IDisposable _sceneReadyHook;
	private SceneLoadType? _pendingMapLoadType;
	private float _sceneReadyDeadline;

	internal void QueueSceneReady( SceneLoadType loadType )
	{
		_pendingMapLoadType = loadType;
		_sceneReadyDeadline = CurrentWorldTime + 0.2f;
		_sceneReadyHook ??= Scene.AddHook( Stage.FinishUpdate, int.MaxValue,
			DispatchSceneReady, GetType().Name, "Scene ready" );
	}

	private void DispatchSceneReady()
	{
#if IGNIS || STANDALONE
		if ( SaveSystem.HasPendingSavedRoots )
			return;
#endif
		if ( CurrentWorldTime < _sceneReadyDeadline || !_pendingMapLoadType.HasValue )
			return;

		var loadType = _pendingMapLoadType.Value;
		StopSceneReady();
		Scene.RunEvent<ISceneLifecycleEvents>( listener => listener.OnSceneReady( loadType ) );
	}

	private void StopSceneReady()
	{
		_sceneReadyHook?.Dispose();
		_sceneReadyHook = null;
		_pendingMapLoadType = null;
	}
}
