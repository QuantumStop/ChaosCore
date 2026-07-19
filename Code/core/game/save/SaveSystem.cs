#if IGNIS || STANDALONE
namespace Core;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Sandbox.Diagnostics;

/// <summary>
/// Versioned save/load facade inspired by sandbox's SaveSystem.
/// Captures the patch between the source scene file and the current scene state ,
/// also provides a snapshot explicitly for prefabs and their content.
/// 
/// Heavily wip!
/// </summary>
public sealed partial class SaveSystem( Scene scene ) : GameObjectSystem<SaveSystem>( scene ), ISceneLoadingEvents
{
	private const int _currentSaveVersion = 2;
	private const int _legacyFullSceneSaveVersion = 1;
	public const string SavesPath = "saves";
	public static bool UseCustomComponentData { get; set; } = false;

	private readonly Dictionary<string, string> _metadata = [];
	private readonly List<LoadedSceneEntry> _loadedScenes = [];
	private bool _suppressSystemScene;

	public static int SaveVersion => _currentSaveVersion;
	public string LoadedSavePath { get; private set; }
	public bool HasLoadedSave => LoadedSavePath is not null;
	public string PrimarySceneSource => _loadedScenes.FirstOrDefault()?.ResourcePath;
	public int LoadedSceneSourceCount => _loadedScenes.Count;

	public static Logger Log { get; } = new Logger( nameof( SaveSystem ) );
	public static event Action<string> OnSave;

	public bool Save( string path )
	{
		SetLastOperation( "save" );

		path = NormalizeSavePath( path );
		if ( string.IsNullOrWhiteSpace( path ) )
		{
			const string error = "path empty";
			Log.Warning( "Cannot save - path is null or empty." );
			return FinishLastOperation( false, error );
		}

		if ( !Scene.IsValid() )
		{
			const string error = "no scene";
			Log.Warning( "Cannot save - no valid scene." );
			return FinishLastOperation( false, error );
		}

		EnsureSaveDirectory();
		EnsureDefaultMetadata( path );
		EnsureLoadedSceneSourceTracked();
		SetLastOperation( "save" );

		if ( _loadedScenes.Count == 0 )
		{
			const string error = "no scene source";
			Log.Warning( "Cannot save - no tracked scene source. The scene must be loaded from a SceneFile." );
			return FinishLastOperation( false, error );
		}

		Log.Info( $"Saving '{path}' against scene source '{_loadedScenes.FirstOrDefault()?.ResourcePath}'." );

		Scene.RunEvent<ISaveEvents>( x => x.BeforeSave( path ) );

		var baseline = BuildCompositeBaseline();
		if ( baseline is null )
		{
			const string error = "baseline failed";
			Log.Warning( "Failed to build baseline from loaded scene sources." );
			return FinishLastOperation( false, error );
		}

		var current = BuildCurrentSceneJson( Scene );
		if ( current is null )
		{
			const string error = "serialize failed";
			Log.Warning( "Failed to serialize current scene state." );
			return FinishLastOperation( false, error );
		}

		var patch = Json.CalculateDifferences( baseline, current, GameObject.DiffObjectDefinitions );
		AddPrefabInstanceOverrides( patch, baseline, current );

		var prefabSnapshots = CollectChangedPrefabSnapshots( baseline, current, patch, out var prefabDebugInfo );
		SetPrefabDebugCounts( prefabDebugInfo );
		SetPatchDebugCounts( patch, prefabSnapshots );
		if ( ShouldWriteDebugData )
			Log.Info( $"Recorded {prefabSnapshots.Count} changed prefab snapshot(s)." );


		var sceneSources = new JsonArray();
		foreach ( var entry in _loadedScenes )
			sceneSources.Add( JsonValue.Create( entry.ResourcePath ) );

		var primarySceneFile = GetPrimarySceneFile();
		var customData = UseCustomComponentData ? CollectCustomComponentData() : null;
		var savedRoots = CollectSavedRoots();
		var runtimeObjectState = CollectRuntimeObjectState();

		var requiredPackages = CollectRequiredPackages( _loadedScenes, current );
		var networkOwnership = CollectNetworkOwnership( Scene );
		var syncState = CollectSyncState( Scene );
		SetPayloadDebugCounts( savedRoots, runtimeObjectState, customData, requiredPackages, syncState );

		var data = new JsonObject
		{
			["Type"] = "chaos_save",
			["Version"] = _currentSaveVersion,
			["SavedTimeNow"] = Time.Now,
			["WorldTime"] = WorldTime.Now,
			["SceneId"] = Scene.Id.ToString(),
			["SceneSources"] = sceneSources,
			["SceneSource"] = _loadedScenes.FirstOrDefault()?.ResourcePath,
			["SceneProperties"] = primarySceneFile is not null ? SerializeScenePropertyDiffs( Scene, primarySceneFile ) : null,
			["Metadata"] = JsonSerializer.SerializeToNode( _metadata ),
			["Patch"] = Json.ToNode( patch ),
			["PrefabSnapshots"] = prefabSnapshots,
			["SavedRoots"] = savedRoots,
			["RuntimeObjectState"] = runtimeObjectState,
			["CustomComponentData"] = customData,
			["RequiredPackages"] = requiredPackages,
			["NetworkOwnership"] = networkOwnership,
			["SyncState"] = syncState
		};

		try
		{
			var dir = Path.GetDirectoryName( path );
			if ( !string.IsNullOrWhiteSpace( dir ) )
				FileSystem.Data.CreateDirectory( dir );

			FileSystem.Data.WriteJson( path, data );
			WriteMetadataSidecar( path, data );
			WriteThumbnail( path );
			LoadedSavePath = path;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed to write save file '{path}': {e.Message}" );
			return FinishLastOperation( false, e.Message );
		}

		Scene.RunEvent<ISaveEvents>( x => x.AfterSave( path ) );
		var result = FinishLastOperation( true );
		OnSave?.Invoke( path );
		return result;
	}

	public async Task<bool> Load( string path )
	{
		SetLastOperation( "load" );

		path = ResolveExistingSavePath( path );
		if ( string.IsNullOrWhiteSpace( path ) )
		{
			const string error = "path empty";
			Log.Warning( "Cannot load - path is null or empty." );
			return FinishLastOperation( false, error );
		}

		if ( !Scene.IsValid() )
		{
			const string error = "no scene";
			Log.Warning( "Cannot load - no valid scene." );
			return FinishLastOperation( false, error );
		}

		if ( !FileSystem.Data.FileExists( path ) )
		{
			Log.Warning( $"Save file '{path}' does not exist." );
			return FinishLastOperation( false, "file missing" );
		}

		JsonObject data;

		try
		{
			data = await ReadSaveJsonAsync( path );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed to read save file '{path}': {e.Message}" );
			return FinishLastOperation( false, e.Message );
		}

		if ( data is null )
		{
			Log.Warning( $"Save file '{path}' is empty or invalid." );
			return FinishLastOperation( false, "invalid file" );
		}

		var type = data["Type"]?.ToString();
		var version = data["Version"]?.GetValue<int>() ?? 0;
		if ( type != "chaos_save" )
		{
			Log.Warning( $"Save file '{path}' is incompatible. Type={type}, Version={version}, RequiredVersion={_currentSaveVersion}." );
			return FinishLastOperation( false, "bad type" );
		}

		if ( version == _legacyFullSceneSaveVersion )
			return LoadLegacyFullSceneSave( path, data );

		if ( version != _currentSaveVersion )
		{
			Log.Warning( $"Save file '{path}' uses version {version}, but this build requires version {_currentSaveVersion}." );
			return FinishLastOperation( false, "bad version" );
		}

		AdjustSaveDataToCurrentTime( data );

		Scene.RunEvent<ISaveEvents>( x => x.BeforeLoad( path ) );

		var sceneFiles = ResolveSceneSources( data["SceneSources"] as JsonArray );
		if ( sceneFiles.Count == 0 )
		{
			Log.Warning( $"Save file '{path}' has no valid scene sources." );
			return FinishLastOperation( false, "no scene sources" );
		}

		if ( data["RequiredPackages"] is JsonArray requiredPackages )
			await MountRequiredPackages( requiredPackages );

		var patchedSceneFile = BuildSceneFileFromSaveData( sceneFiles, data, out var prefabApplyDebugInfo );
		if ( patchedSceneFile is null )
			return FinishLastOperation( false, "patch failed" );

		var options = new SceneLoadOptions
		{
			ShowLoadingScreen = true,
			MinimumLoadingScreenSeconds = 3.0f
		};
		options.SetScene( patchedSceneFile );

		// Stage roots before scene load so normal startup code can avoid spawning duplicates.
		StageSavedRootsForSceneLoad( data["SavedRoots"] as JsonArray );
		StageWorldTimeForSceneLoad( data );

		_suppressSystemScene = true;
		Game.ChangeScene( options );

		var newSystem = Current;
		if ( newSystem is null )
		{
			Log.Warning( "Could not find SaveSystem after loading patched scene." );
			return FinishLastOperation( false, "new system missing" );
		}

		newSystem._loadedScenes.Clear();
		foreach ( var sceneFile in sceneFiles )
		{
			if ( string.IsNullOrWhiteSpace( sceneFile.ResourcePath ) )
				continue;

			newSystem._loadedScenes.Add( new LoadedSceneEntry
			{
				ResourcePath = sceneFile.ResourcePath,
				SceneFileId = sceneFile.Id
			} );
		}

		newSystem._metadata.Clear();
		if ( data["Metadata"] is JsonObject metadata )
		{
			foreach ( var (key, value) in metadata )
				newSystem._metadata[key] = value?.ToString();
		}

		if ( UseCustomComponentData )
			newSystem.RestoreCustomComponentData( data["CustomComponentData"] as JsonArray );
		if ( data["SyncState"] is JsonObject syncState )
			RestoreSyncState( newSystem.Scene, syncState );

		if ( data["NetworkOwnership"] is JsonObject networkOwnership )
			RestoreNetworkOwnership( newSystem.Scene, networkOwnership );

		newSystem.RestoreWorldTime( data );
		newSystem.SetPrefabApplyDebugCounts( prefabApplyDebugInfo );
		newSystem.SetPayloadDebugCounts(
			data["SavedRoots"] as JsonArray,
			data["RuntimeObjectState"] as JsonArray,
			data["CustomComponentData"] as JsonArray,
			data["RequiredPackages"] as JsonArray,
			data["SyncState"] as JsonObject
		);
		newSystem.SetLoadedPrefabSnapshotDebugCount( data["PrefabSnapshots"] as JsonArray );
		newSystem.RestoreSavedRoots( data["SavedRoots"] as JsonArray );
		newSystem.RestoreRuntimeObjectState( data["RuntimeObjectState"] as JsonArray );

		newSystem.LoadedSavePath = path;
		newSystem.Scene.RunEvent<ISaveEvents>( x => x.AfterLoad( path ) );
		newSystem.SetLastOperation( "load" );
		newSystem.FinishLastOperation( true );
		return true;
	}

	public bool Delete( string path )
	{
		path = NormalizeSavePath( path );
		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		if ( FileSystem.Data.FileExists( path ) )
			FileSystem.Data.DeleteFile( path );

		DeleteMetadataSidecar( path );
		DeleteThumbnails( path );

		return true;
	}

	private bool LoadLegacyFullSceneSave( string path, JsonObject data )
	{
		SetLastOperation( "load" );
		Scene.RunEvent<ISaveEvents>( x => x.BeforeLoad( path ) );

		if ( data["SceneObject"] is not JsonObject sceneObject )
		{
			Log.Warning( $"Legacy save file '{path}' has no scene data." );
			return FinishLastOperation( false, "legacy scene missing" );
		}

		AdjustSaveDataToCurrentTime( data );

		Scene.Deserialize( sceneObject );
		if ( UseCustomComponentData )
			RestoreCustomComponentData( data["CustomComponentData"] as JsonArray );

		RestoreWorldTime( data );
		RestoreSavedRoots( data["SavedRoots"] as JsonArray );
		RestoreRuntimeObjectState( data["RuntimeObjectState"] as JsonArray );

		_metadata.Clear();
		if ( data["Metadata"] is JsonObject metadata )
		{
			foreach ( var (key, value) in metadata )
				_metadata[key] = value?.ToString();
		}

		LoadedSavePath = path;
		Scene.RunEvent<ISaveEvents>( x => x.AfterLoad( path ) );
		return FinishLastOperation( true );
	}

}

public record SaveFileEntry(
	string FileName,
	string Path,
	string DisplayName,
	string Timestamp,
	bool IsCompatible,
	string SaveType,
	long SortOrder,
	string ThumbnailPath,
	Texture Thumbnail
);
#endif
