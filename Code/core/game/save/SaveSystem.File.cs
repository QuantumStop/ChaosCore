#if IGNIS || STANDALONE
namespace Core;

using System.IO;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public sealed partial class SaveSystem
{
	private const int SaveReadBufferSize = 64 * 1024;

	public static string NormalizeSavePath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return null;

		path = path.Trim().Replace( '\\', '/' );
		if ( !path.StartsWith( $"{SavesPath}/", StringComparison.OrdinalIgnoreCase ) )
			path = $"{SavesPath}/{path}";

		if ( Path.GetExtension( path ).Length == 0 )
			path += ".sav";

		return path;
	}

	public static string GetThumbnailPath( string path )
	{
		path = NormalizeSavePath( path );
		return $"{SavesPath}/{Path.GetFileNameWithoutExtension( path )}.thumb.png";
	}

	private static string GetMetadataPath( string path )
	{
		path = NormalizeSavePath( path );
		return $"{SavesPath}/{Path.GetFileNameWithoutExtension( path )}.meta.json";
	}

	private static string GetThumbnailPath( string path, IReadOnlyDictionary<string, string> metadata )
	{
		path = NormalizeSavePath( path );
		if ( metadata is null || !metadata.TryGetValue( "ThumbnailVersion", out var thumbnailVersion ) || string.IsNullOrWhiteSpace( thumbnailVersion ) )
			return GetThumbnailPath( path );

		return $"{SavesPath}/{Path.GetFileNameWithoutExtension( path )}.thumb.{thumbnailVersion}.png";
	}

	private static string ResolveExistingSavePath( string path )
	{
		var normalized = NormalizeSavePath( path );
		if ( FileSystem.Data.FileExists( normalized ) )
			return normalized;

		var legacy = normalized?.EndsWith( ".sav", StringComparison.OrdinalIgnoreCase ) == true
			? normalized[..^4] + ".save"
			: normalized;

		return legacy;
	}

	private static void EnsureSaveDirectory()
	{
		if ( !FileSystem.Data.DirectoryExists( SavesPath ) )
			FileSystem.Data.CreateDirectory( SavesPath );
	}

	private static async Task<JsonObject> ReadSaveJsonAsync( string path )
	{
		using var input = FileSystem.Data.OpenRead( path );
		using var output = new MemoryStream();
		var buffer = new byte[SaveReadBufferSize];

		while ( true )
		{
			var read = await input.ReadAsync( buffer, 0, buffer.Length );
			if ( read <= 0 )
				break;

			await output.WriteAsync( buffer, 0, read );
			await GameTask.Delay( 1 );
		}

		var bytes = output.ToArray();
		return await Task.Run( () => JsonNode.Parse( bytes )?.AsObject() );
	}

	private void EnsureDefaultMetadata( string path )
	{
		if ( !_metadata.ContainsKey( "Title" ) )
			_metadata["Title"] = Path.GetFileNameWithoutExtension( path );

		if ( !_metadata.ContainsKey( "SaveType" ) )
			_metadata["SaveType"] = "MANUAL SAVE";

		var now = DateTime.Now;
		_metadata["Timestamp"] = now.ToString( "yyyy-MM-dd HH:mm:ss" );
		_metadata["SaveTicks"] = now.Ticks.ToString();
		_metadata["ThumbnailVersion"] = now.Ticks.ToString();
	}

	private static void DeleteThumbnails( string path )
	{
		path = NormalizeSavePath( path );
		var baseName = Path.GetFileNameWithoutExtension( path );

		foreach ( var file in FileSystem.Data.FindFile( SavesPath, $"{baseName}.thumb*.png" ) )
			FileSystem.Data.DeleteFile( $"{SavesPath}/{file}" );
	}

	private static void DeleteMetadataSidecar( string path )
	{
		var metadataPath = GetMetadataPath( path );
		if ( FileSystem.Data.FileExists( metadataPath ) )
			FileSystem.Data.DeleteFile( metadataPath );
	}
}
#endif
