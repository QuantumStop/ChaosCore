namespace Core.Editor;

public static partial class PresetService
{
	public static Asset SaveAs( PresetAsset preset, string filename )
	{
		if ( preset?.Resource is null )
			return null;

		if ( string.IsNullOrWhiteSpace( filename ) )
			return null;
		
		Asset asset = AssetSystem.CreateResource( AssetExtension, filename );

		if ( asset is null )
		{
			Log.Warning( $"Unable to create preset asset '{filename}'" );

			return null;
		}

		preset.Resource.SourceAssetPath = asset.Path;

		if ( !asset.SaveToDisk( preset.Resource ) )
		{
			Log.Warning( $"Unable to save preset asset '{filename}'" );

			return null;
		}

		asset.Compile( false );

		return asset;
	}

	public static bool Save( PresetAsset preset )
	{
		if ( preset?.Asset is null || preset.Resource is null )
			return false;

		preset.Resource.SourceAssetPath = preset.Asset.Path;

		if ( !preset.Asset.SaveToDisk( preset.Resource ) )
			return false;
		
		preset.Asset.Compile( false );

		return true;
	}
}
