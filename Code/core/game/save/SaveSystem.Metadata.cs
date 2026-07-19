#if IGNIS || STANDALONE
namespace Core;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed partial class SaveSystem
{
	public void SetMetadata( string key, string value )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			throw new ArgumentException( "Metadata key cannot be null or empty.", nameof( key ) );

		_metadata[key] = value;
	}

	public string GetMetadata( string key, string defaultValue = null )
	{
		if ( key is null )
			return defaultValue;

		return _metadata.TryGetValue( key, out var value ) ? value : defaultValue;
	}

	public IReadOnlyDictionary<string, string> GetAllMetadata()
	{
		return new Dictionary<string, string>( _metadata );
	}

	public static IReadOnlyDictionary<string, string> GetFileMetadata( string path )
	{
		path = NormalizeSavePath( path );
		if ( string.IsNullOrWhiteSpace( path ) || !FileSystem.Data.FileExists( path ) )
			return null;

		try
		{
			if ( TryReadMetadataSidecar( path, out var sidecar ) && sidecar["Metadata"] is JsonObject sidecarMetadata )
				return JsonSerializer.Deserialize<Dictionary<string, string>>( sidecarMetadata.ToJsonString() );

			var data = FileSystem.Data.ReadJson<JsonObject>( path );
			WriteMetadataSidecar( path, data );

			if ( data?["Metadata"] is JsonObject metadata )
				return JsonSerializer.Deserialize<Dictionary<string, string>>( metadata.ToJsonString() );

			return new Dictionary<string, string>();
		}
		catch ( Exception e )
		{
			Log.Warning( $"SaveSystem: Failed to read metadata from '{path}': {e.Message}" );
			return null;
		}
	}

	public static int GetFileSaveVersion( string path )
	{
		path = NormalizeSavePath( path );
		if ( string.IsNullOrWhiteSpace( path ) || !FileSystem.Data.FileExists( path ) )
			return 0;

		try
		{
			if ( TryReadMetadataSidecar( path, out var sidecar ) )
				return sidecar?["Version"]?.GetValue<int>() ?? 0;

			var data = FileSystem.Data.ReadJson<JsonObject>( path );
			WriteMetadataSidecar( path, data );
			return data?["Version"]?.GetValue<int>() ?? 0;
		}
		catch
		{
			return 0;
		}
	}

	public static string GetFileSceneSource( string path )
	{
		path = NormalizeSavePath( path );
		if ( string.IsNullOrWhiteSpace( path ) || !FileSystem.Data.FileExists( path ) )
			return null;

		try
		{
			if ( TryReadMetadataSidecar( path, out var sidecar ) )
				return GetFirstSceneSource( sidecar );

			var data = FileSystem.Data.ReadJson<JsonObject>( path );
			WriteMetadataSidecar( path, data );
			return GetFirstSceneSource( data );
		}
		catch
		{
			return null;
		}
	}

	public static IReadOnlyList<SaveFileEntry> GetSaveFiles()
	{
		EnsureSaveDirectory();

		var saves = new List<SaveFileEntry>();
		foreach ( var file in FileSystem.Data.FindFile( SavesPath, "*.sav" ) )
		{
			var path = $"{SavesPath}/{file}";
			var metadata = GetFileMetadata( path );
			var title = metadata is not null && metadata.TryGetValue( "Title", out var savedTitle ) && !string.IsNullOrWhiteSpace( savedTitle )
				? savedTitle
				: metadata is not null && metadata.TryGetValue( "SceneTitle", out var savedSceneTitle ) && !string.IsNullOrWhiteSpace( savedSceneTitle )
					? savedSceneTitle
					: Path.GetFileNameWithoutExtension( file );

			var timestamp = metadata is not null && metadata.TryGetValue( "Timestamp", out var savedTimestamp )
				? savedTimestamp
				: null;
			var saveType = metadata is not null && metadata.TryGetValue( "SaveType", out var savedSaveType )
				? savedSaveType
				: "MANUAL SAVE";
			var sortOrder = metadata is not null && metadata.TryGetValue( "SaveTicks", out var savedSaveTicks ) && long.TryParse( savedSaveTicks, out var parsedSaveTicks )
				? parsedSaveTicks
				: 0;
			var thumbnailPath = GetThumbnailPath( path );
			if ( metadata is not null )
				thumbnailPath = GetThumbnailPath( path, metadata );

			var thumbnail = FileSystem.Data.FileExists( thumbnailPath )
				? Texture.LoadFromFileSystem( thumbnailPath, FileSystem.Data )
				: null;

			var saveVersion = GetFileSaveVersion( path );
			saves.Add( new SaveFileEntry(
				file,
				path,
				title,
				timestamp,
				saveVersion == _currentSaveVersion || saveVersion == _legacyFullSceneSaveVersion,
				saveType,
				sortOrder,
				thumbnailPath,
				thumbnail
			) );
		}

		return saves
			.OrderByDescending( x => x.SortOrder )
			.ThenByDescending( x => x.Timestamp )
			.ThenBy( x => x.DisplayName )
			.ToList();
	}

	private static bool TryReadMetadataSidecar( string path, out JsonObject data )
	{
		data = null;
		var metadataPath = GetMetadataPath( path );
		if ( string.IsNullOrWhiteSpace( metadataPath ) || !FileSystem.Data.FileExists( metadataPath ) )
			return false;

		data = FileSystem.Data.ReadJson<JsonObject>( metadataPath );
		return data is not null;
	}

	private static void WriteMetadataSidecar( string path, JsonObject data )
	{
		if ( data is null )
			return;

		var metadataPath = GetMetadataPath( path );
		var sidecar = new JsonObject
		{
			["Type"] = data["Type"]?.DeepClone(),
			["Version"] = data["Version"]?.DeepClone(),
			["SceneSource"] = data["SceneSource"]?.DeepClone(),
			["SceneSources"] = data["SceneSources"]?.DeepClone(),
			["Metadata"] = data["Metadata"]?.DeepClone()
		};

		FileSystem.Data.WriteJson( metadataPath, sidecar );
	}

	private static string GetFirstSceneSource( JsonObject data )
	{
		if ( data?["SceneSources"] is JsonArray sceneSources )
		{
			foreach ( var node in sceneSources )
			{
				var source = node?.ToString();
				if ( !string.IsNullOrWhiteSpace( source ) )
					return source;
			}
		}

		return data?["SceneSource"]?.ToString();
	}
}
#endif
