#if IGNIS || STANDALONE
namespace Core;

using System;
using System.Text.Json.Nodes;

public sealed partial class SaveSystem
{
	private const string PrefabInstanceSourceKey = "__Prefab";
	private const string PrefabInstancePatchKey = "__PrefabInstancePatch";
	private const string PrefabIdToInstanceIdKey = "__PrefabIdToInstanceId";

	private JsonObject BuildCompositeBaseline()
	{
		var sceneFiles = new List<SceneFile>();
		foreach ( var entry in _loadedScenes )
		{
			var sceneFile = ResourceLibrary.Get<SceneFile>( entry.ResourcePath );
			if ( sceneFile is null )
			{
				Log.Warning( $"Tracked scene '{entry.ResourcePath}' could not be found." );
				continue;
			}

			sceneFiles.Add( sceneFile );
		}

		return BuildCompositeBaselineFromFiles( sceneFiles, Scene.Id );
	}

	private static JsonObject BuildCompositeBaselineFromFiles( List<SceneFile> sceneFiles, Guid rootId )
	{
		if ( sceneFiles.Count == 0 )
			return null;

		var children = new JsonArray();
		foreach ( var sceneFile in sceneFiles )
		{
			if ( sceneFile?.GameObjects is null )
				continue;

			foreach ( var gameObject in sceneFile.GameObjects )
			{
				if ( gameObject is null )
					continue;

				children.Add( gameObject.DeepClone() );
			}
		}

		return new JsonObject
		{
			["__guid"] = rootId.ToString(),
			["Flags"] = 0,
			["Components"] = new JsonArray(),
			["Children"] = children
		};
	}

	private static JsonObject BuildCurrentSceneJson( Scene scene )
	{
		using var sceneScope = scene.Push();
		var children = new JsonArray();

		foreach ( var child in scene.Children )
		{
			if ( ShouldSkipSavedGameObject( child ) )
				continue;

			var serialized = child.Serialize();
			if ( serialized is null )
				continue;

			children.Add( serialized );
		}

		return new JsonObject
		{
			["__guid"] = scene.Id.ToString(),
			["Flags"] = 0,
			["Components"] = new JsonArray(),
			["Children"] = children
		};
	}

	private static void AddPrefabInstanceOverrides( Json.Patch patch, JsonObject baseline, JsonObject current )
	{
		var baselinePrefabs = new Dictionary<string, JsonObject>();
		var currentPrefabs = new Dictionary<string, JsonObject>();

		CollectPrefabInstanceStubs( baseline, baselinePrefabs );
		CollectPrefabInstanceStubs( current, currentPrefabs );

		foreach ( var (id, currentPrefab) in currentPrefabs )
		{
			if ( !baselinePrefabs.TryGetValue( id, out var baselinePrefab ) )
				continue;

			AddPrefabStubPropertyOverride( patch, id, baselinePrefab, currentPrefab, PrefabInstancePatchKey );
			AddPrefabStubPropertyOverride( patch, id, baselinePrefab, currentPrefab, PrefabIdToInstanceIdKey );
		}
	}

	private static JsonArray CollectChangedPrefabSnapshots(
		JsonObject baseline,
		JsonObject current,
		Json.Patch patch,
		out PrefabSnapshotDebugInfo debugInfo )
	{
		var snapshots = new JsonArray();
		var currentPrefabs = new Dictionary<string, JsonObject>();
		var baselineOwnerById = new Dictionary<string, string>();
		var currentOwnerById = new Dictionary<string, string>();

		CollectPrefabInstanceStubs( current, currentPrefabs );
		CollectPrefabOwnerIds( baseline, baselineOwnerById );
		CollectPrefabOwnerIds( current, currentOwnerById );

		// Log.Info(
		// 	$"Prefab ownership: baseline={baselineOwnerById.Count}, " +
		// 	$"current={currentOwnerById.Count}"
		// );

		// foreach ( var (objectId, prefabId) in baselineOwnerById )
		// 	Log.Info( $"Baseline object {objectId} belongs to prefab {prefabId}" );

		var changedPrefabIds = CollectChangedPrefabIds( patch, baselineOwnerById, currentOwnerById );
		debugInfo = new PrefabSnapshotDebugInfo(
			baselineOwnerById.Count,
			currentOwnerById.Count,
			changedPrefabIds.Count
		);

		foreach ( var id in changedPrefabIds )
		{
			if ( !currentPrefabs.TryGetValue( id, out var currentPrefab ) )
				continue;

			snapshots.Add( new JsonObject
			{
				["Id"] = id,
				["Snapshot"] = currentPrefab.DeepClone()
			} );
		}

		return snapshots;
	}


	private static string ReadJsonId( JsonNode node )
	{
		if ( node is null )
			return null;

		string value;

		try
		{
			value = node.GetValue<string>();
		}
		catch
		{
			value = node.ToString();
		}

		if ( string.IsNullOrWhiteSpace( value ) )
			return null;

		value = value.Trim().Trim( '"' );

		return Guid.TryParse( value, out var guid )
			? guid.ToString()
			: value;
	}

	private static HashSet<string> CollectChangedPrefabIds(
		Json.Patch patch,
		Dictionary<string, string> baselineOwnerById,
		Dictionary<string, string> currentOwnerById )
	{
		var prefabIds = new HashSet<string>();
		var patchNode = Json.ToNode( patch ) as JsonObject;
		if ( patchNode is null )
			return prefabIds;

		CollectPatchIds( patchNode["RemovedObjects"] as JsonArray, "Id", baselineOwnerById, currentOwnerById, prefabIds );
		CollectPatchIds( patchNode["AddedObjects"] as JsonArray, "Id", baselineOwnerById, currentOwnerById, prefabIds );
		CollectPatchIds( patchNode["AddedObjects"] as JsonArray, "Parent", baselineOwnerById, currentOwnerById, prefabIds );
		CollectPatchIds( patchNode["MovedObjects"] as JsonArray, "Id", baselineOwnerById, currentOwnerById, prefabIds );
		CollectPatchIds( patchNode["MovedObjects"] as JsonArray, "NewParent", baselineOwnerById, currentOwnerById, prefabIds );
		CollectPatchIds( patchNode["PropertyOverrides"] as JsonArray, "Target", baselineOwnerById, currentOwnerById, prefabIds );

		return prefabIds;
	}

	private static void CollectPatchIds(
		JsonArray entries,
		string property,
		Dictionary<string, string> baselineOwnerById,
		Dictionary<string, string> currentOwnerById,
		HashSet<string> prefabIds )
	{
		if ( entries is null )
			return;

		foreach ( var entry in entries )
		{
			if ( entry is not JsonObject entryObject ||
				 entryObject[property] is not JsonObject identifier )
				continue;

			AddPrefabOwnerForIdentifier( identifier, baselineOwnerById, currentOwnerById, prefabIds );
		}
	}

	private static void AddPrefabOwnerForIdentifier(
		JsonObject identifier,
		Dictionary<string, string> baselineOwnerById,
		Dictionary<string, string> currentOwnerById,
		HashSet<string> prefabIds )
	{
		var id = ReadJsonId( identifier["IdValue"] );
		if ( string.IsNullOrWhiteSpace( id ) )
			return;

		if ( currentOwnerById.TryGetValue( id, out var currentPrefabId ) )
			prefabIds.Add( currentPrefabId );

		if ( baselineOwnerById.TryGetValue( id, out var baselinePrefabId ) )
			prefabIds.Add( baselinePrefabId );
	}

	private static void CollectPrefabOwnerIds(
		JsonNode node,
		Dictionary<string, string> ownerById,
		string currentPrefabId = null )
	{
		if ( node is JsonObject jsonObject )
		{
			var id = ReadJsonId( jsonObject["__guid"] );

			var isPrefabInstance =
				jsonObject.ContainsKey( PrefabInstanceSourceKey ) &&
				!string.IsNullOrWhiteSpace( id );

			var ownerId = isPrefabInstance
				? id
				: currentPrefabId;

			if ( !string.IsNullOrWhiteSpace( id ) &&
				 !string.IsNullOrWhiteSpace( ownerId ) )
			{
				ownerById[id] = ownerId;
			}

			foreach ( var (_, value) in jsonObject )
				CollectPrefabOwnerIds( value, ownerById, ownerId );

			return;
		}

		if ( node is JsonArray jsonArray )
		{
			foreach ( var child in jsonArray )
				CollectPrefabOwnerIds( child, ownerById, currentPrefabId );
		}
	}

	private static void CollectPrefabInstanceStubs(
		JsonNode node,
		Dictionary<string, JsonObject> results )
	{
		if ( node is JsonObject jsonObject )
		{
			if ( jsonObject.ContainsKey( PrefabInstanceSourceKey ) )
			{
				var id = ReadJsonId( jsonObject["__guid"] );

				if ( !string.IsNullOrWhiteSpace( id ) )
					results[id] = jsonObject;
			}

			foreach ( var (_, value) in jsonObject )
				CollectPrefabInstanceStubs( value, results );

			return;
		}

		if ( node is JsonArray jsonArray )
		{
			foreach ( var child in jsonArray )
				CollectPrefabInstanceStubs( child, results );
		}
	}

	private static void AddPrefabStubPropertyOverride( Json.Patch patch, string id, JsonObject baseline, JsonObject current, string property )
	{
		baseline.TryGetPropertyValue( property, out var baselineValue );
		current.TryGetPropertyValue( property, out var currentValue );

		if ( JsonNode.DeepEquals( baselineValue, currentValue ) )
			return;

		patch.PropertyOverrides.Add( new Json.PropertyOverride
		{
			Target = new Json.ObjectIdentifier
			{
				Type = "GameObject",
				IdValue = id
			},
			Property = property,
			Value = currentValue?.DeepClone()
		} );
	}

	private static bool ShouldSkipSavedGameObject( GameObject gameObject )
	{
		if ( !gameObject.IsValid() )
			return true;

		if ( gameObject.Flags.Contains( GameObjectFlags.DontDestroyOnLoad ) ||
			 gameObject.Flags.Contains( GameObjectFlags.NotSaved ) ||
			 gameObject.Flags.Contains( GameObjectFlags.EditorOnly ) )
			return true;

		return HasComponentInTree<BasePlayer>( gameObject ) ||
			   // We can probably skip this, since it should be in Player's tree
			   //   HasComponentNamedInTree( gameObject, "BaseGUIManager" ) ||
			   HasComponentNamedInTree( gameObject, "ScreenPanel" );
	}

	private static bool HasComponentInTree<T>( GameObject gameObject ) where T : Component
	{
		if ( gameObject.Components.Get<T>().IsValid() )
			return true;

		foreach ( var child in gameObject.Children )
		{
			if ( HasComponentInTree<T>( child ) )
				return true;
		}

		return false;
	}

	private static bool HasComponentNamedInTree( GameObject gameObject, string componentName )
	{
		foreach ( var component in gameObject.Components.GetAll() )
		{
			var type = component.GetType();
			if ( string.Equals( type.Name, componentName, StringComparison.Ordinal ) ||
				 string.Equals( type.FullName, componentName, StringComparison.Ordinal ) )
				return true;
		}

		foreach ( var child in gameObject.Children )
		{
			if ( HasComponentNamedInTree( child, componentName ) )
				return true;
		}

		return false;
	}

	private static JsonNode SerializeScenePropertyDiffs( Scene scene, SceneFile sceneFile )
	{
		var currentProps = scene.SerializeProperties();
		var baseProps = sceneFile.SceneProperties;

		if ( currentProps is null )
			return null;

		if ( baseProps is null )
			return currentProps.DeepClone();

		var diffs = new JsonObject();
		var hasChanges = false;

		foreach ( var prop in currentProps )
		{
			if ( baseProps.TryGetPropertyValue( prop.Key, out var baseValue ) )
			{
				if ( JsonNode.DeepEquals( baseValue, prop.Value ) )
					continue;
			}

			diffs[prop.Key] = prop.Value?.DeepClone();
			hasChanges = true;
		}

		return hasChanges ? diffs : null;
	}

	private static SceneFile BuildPatchedSceneFile( SceneFile original, JsonObject patchedRoot, JsonNode savedSceneProperties )
	{
		var patchedSceneFile = new SceneFile
		{
			Id = original.Id
		};

		if ( patchedRoot["Children"] is JsonArray gameObjects )
		{
			patchedSceneFile.GameObjects = gameObjects
				.Where( x => x is JsonObject )
				.Select( x => x.DeepClone().AsObject() )
				.ToArray();
		}

		var sceneProperties = original.SceneProperties?.DeepClone()?.AsObject() ?? new JsonObject();
		if ( savedSceneProperties is JsonObject overrides )
		{
			foreach ( var prop in overrides )
				sceneProperties[prop.Key] = prop.Value?.DeepClone();
		}

		patchedSceneFile.SceneProperties = sceneProperties;
		return patchedSceneFile;
	}

	private static SceneFile BuildSceneFileFromSaveData(
		List<SceneFile> sceneFiles,
		JsonObject data,
		out PrefabSnapshotApplyDebugInfo prefabApplyDebugInfo )
	{
		prefabApplyDebugInfo = default;

		// Compatibility for the temporary full-snapshot saves we used while proving prefab restore.
		if ( data["SceneSnapshot"] is JsonObject snapshotRoot )
			return BuildPatchedSceneFile( sceneFiles[0], snapshotRoot, data["SceneProperties"] );

		return BuildPatchedSceneFileFromSaveData( sceneFiles, data, out prefabApplyDebugInfo );
	}

	private static SceneFile BuildPatchedSceneFileFromSaveData(
		List<SceneFile> sceneFiles,
		JsonObject data,
		out PrefabSnapshotApplyDebugInfo prefabApplyDebugInfo )
	{
		prefabApplyDebugInfo = default;

		var savedPatch = data["Patch"] is JsonObject patchNode
			? Json.FromNode<Json.Patch>( patchNode )
			: new Json.Patch();

		var savedSceneId = Guid.TryParse( data["SceneId"]?.ToString(), out var parsedId )
			? parsedId
			: sceneFiles[0].Id;

		var baseline = BuildCompositeBaselineFromFiles( sceneFiles, savedSceneId );
		if ( baseline is null )
		{
			Log.Warning( "Failed to build baseline from saved scene sources." );
			return null;
		}

		var patched = Json.ApplyPatch( baseline, savedPatch, GameObject.DiffObjectDefinitions );
		if ( patched is not JsonObject patchedRoot )
		{
			Log.Warning( "Failed to apply save patch." );
			return null;
		}

		ApplyPrefabSnapshots( patchedRoot, data["PrefabSnapshots"] as JsonArray, out prefabApplyDebugInfo );

		return BuildPatchedSceneFile( sceneFiles[0], patchedRoot, data["SceneProperties"] );
	}

	private static void ApplyPrefabSnapshots( JsonObject root, JsonArray snapshots, out PrefabSnapshotApplyDebugInfo debugInfo )
	{
		var appliedCount = 0;
		var skippedCount = 0;

		if ( root is null || snapshots is null || snapshots.Count == 0 )
		{
			debugInfo = new PrefabSnapshotApplyDebugInfo( appliedCount, skippedCount );
			return;
		}

		var prefabSnapshots = new Dictionary<string, JsonObject>();
		foreach ( var snapshotNode in snapshots )
		{
			if ( snapshotNode is not JsonObject snapshotEntry )
				continue;

			var id = snapshotEntry["Id"]?.ToString();
			if ( string.IsNullOrWhiteSpace( id ) || snapshotEntry["Snapshot"] is not JsonObject snapshot )
				continue;

			prefabSnapshots[id] = snapshot;
		}

		if ( prefabSnapshots.Count == 0 )
		{
			debugInfo = new PrefabSnapshotApplyDebugInfo( appliedCount, skippedCount );
			return;
		}

		ApplyPrefabSnapshots( root, prefabSnapshots, ref appliedCount );
		skippedCount = Math.Max( 0, prefabSnapshots.Count - appliedCount );
		debugInfo = new PrefabSnapshotApplyDebugInfo( appliedCount, skippedCount );
	}

	private static void ApplyPrefabSnapshots( JsonNode node, Dictionary<string, JsonObject> snapshots, ref int appliedCount )
	{
		if ( node is JsonObject jsonObject )
		{
			foreach ( var (_, value) in jsonObject.ToArray() )
				ApplyPrefabSnapshots( value, snapshots, ref appliedCount );

			return;
		}

		if ( node is not JsonArray jsonArray )
			return;

		for ( var i = 0; i < jsonArray.Count; i++ )
		{
			if ( jsonArray[i] is JsonObject child &&
				 child["__guid"] is JsonNode idNode &&
				 snapshots.TryGetValue( idNode.ToString(), out var snapshot ) )
			{
				jsonArray[i] = snapshot.DeepClone();
				appliedCount++;
				continue;
			}

			ApplyPrefabSnapshots( jsonArray[i], snapshots, ref appliedCount );
		}
	}

	private readonly record struct PrefabSnapshotDebugInfo(
		int BaselineOwnerCount,
		int CurrentOwnerCount,
		int ChangedPrefabIdCount
	);

	private readonly record struct PrefabSnapshotApplyDebugInfo(
		int AppliedCount,
		int SkippedCount
	);
}
#endif
