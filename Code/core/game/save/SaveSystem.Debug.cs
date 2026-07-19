#if IGNIS || STANDALONE
namespace Core;

using System.Text.Json.Nodes;

public sealed partial class SaveSystem
{
	public int LastPrefabSnapshotCount { get; private set; }
	public int LastPatchAddedObjectCount { get; private set; }
	public int LastPatchRemovedObjectCount { get; private set; }
	public int LastPatchPropertyOverrideCount { get; private set; }
	public int LastPatchMovedObjectCount { get; private set; }
	public int LastSavedRootCount { get; private set; }
	public int LastRuntimeObjectStateCount { get; private set; }
	public int LastCustomDataCount { get; private set; }
	public int LastRequiredPackageCount { get; private set; }
	public int LastSyncEntryCount { get; private set; }
	public int LastPrefabBaselineOwnerCount { get; private set; }
	public int LastPrefabCurrentOwnerCount { get; private set; }
	public int LastPrefabChangedIdCount { get; private set; }
	public int LastPrefabAppliedSnapshotCount { get; private set; }
	public int LastPrefabSkippedSnapshotCount { get; private set; }
	public string LastOperation { get; private set; } = "none";
	public string LastResult { get; private set; } = "none";
	public string LastError { get; private set; }

	private static bool ShouldWriteDebugData => GameManagerSystem.ShowSaveSystem;

	private void ClearTransientDebugData()
	{
		if ( !ShouldWriteDebugData )
			return;

		LastPrefabSnapshotCount = 0;
		LastPatchAddedObjectCount = 0;
		LastPatchRemovedObjectCount = 0;
		LastPatchPropertyOverrideCount = 0;
		LastPatchMovedObjectCount = 0;
		LastSavedRootCount = 0;
		LastRuntimeObjectStateCount = 0;
		LastCustomDataCount = 0;
		LastRequiredPackageCount = 0;
		LastSyncEntryCount = 0;
		LastPrefabBaselineOwnerCount = 0;
		LastPrefabCurrentOwnerCount = 0;
		LastPrefabChangedIdCount = 0;
		LastPrefabAppliedSnapshotCount = 0;
		LastPrefabSkippedSnapshotCount = 0;
		LastOperation = "none";
		LastResult = "none";
		LastError = null;
	}

	private void SetLastOperation( string operation )
	{
		if ( !ShouldWriteDebugData )
			return;

		LastOperation = operation;
		LastResult = "running";
		LastError = null;
	}

	private bool FinishLastOperation( bool success, string error = null )
	{
		if ( !ShouldWriteDebugData )
			return success;

		LastResult = success ? "ok" : "fail";
		LastError = error;
		return success;
	}

	private void SetPrefabDebugCounts( PrefabSnapshotDebugInfo debugInfo )
	{
		if ( !ShouldWriteDebugData )
			return;

		LastPrefabBaselineOwnerCount = debugInfo.BaselineOwnerCount;
		LastPrefabCurrentOwnerCount = debugInfo.CurrentOwnerCount;
		LastPrefabChangedIdCount = debugInfo.ChangedPrefabIdCount;
	}

	private void SetPrefabApplyDebugCounts( PrefabSnapshotApplyDebugInfo debugInfo )
	{
		if ( !ShouldWriteDebugData )
			return;

		LastPrefabAppliedSnapshotCount = debugInfo.AppliedCount;
		LastPrefabSkippedSnapshotCount = debugInfo.SkippedCount;
	}

	private void SetPatchDebugCounts( Json.Patch patch, JsonArray prefabSnapshots )
	{
		if ( !ShouldWriteDebugData )
			return;

		LastPrefabSnapshotCount = prefabSnapshots?.Count ?? 0;
		LastPatchAddedObjectCount = patch.AddedObjects?.Count ?? 0;
		LastPatchRemovedObjectCount = patch.RemovedObjects?.Count ?? 0;
		LastPatchPropertyOverrideCount = patch.PropertyOverrides?.Count ?? 0;
		LastPatchMovedObjectCount = patch.MovedObjects?.Count ?? 0;
	}

	private void SetPayloadDebugCounts(
		JsonArray savedRoots,
		JsonArray runtimeObjectState,
		JsonArray customData,
		JsonArray requiredPackages,
		JsonObject syncState )
	{
		if ( !ShouldWriteDebugData )
			return;

		LastSavedRootCount = savedRoots?.Count ?? 0;
		LastRuntimeObjectStateCount = runtimeObjectState?.Count ?? 0;
		LastCustomDataCount = customData?.Count ?? 0;
		LastRequiredPackageCount = requiredPackages?.Count ?? 0;
		LastSyncEntryCount = syncState?.Count ?? 0;
	}

	private void SetLoadedPrefabSnapshotDebugCount( JsonArray prefabSnapshots )
	{
		if ( !ShouldWriteDebugData )
			return;

		LastPrefabSnapshotCount = prefabSnapshots?.Count ?? 0;
	}
}
#endif
