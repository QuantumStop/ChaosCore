#if IGNIS || STANDALONE
namespace Core;

using System;

public sealed partial class SaveSystem
{
	private void WriteThumbnail( string path )
	{
		var camera = Scene.Camera;
		if ( !camera.IsValid() )
			return;

		var hadUiExcluded = camera.RenderExcludeTags.Has( "ui" );
		var hadDevUiExcluded = camera.RenderExcludeTags.Has( "devui" );
		var hadFirstPersonExcluded = camera.RenderExcludeTags.Has( "firstperson" );

		try
		{
			camera.RenderExcludeTags.Add( "ui" );
			camera.RenderExcludeTags.Add( "devui" );
			camera.RenderExcludeTags.Add( "firstperson" );

			DeleteThumbnails( path );

			var bitmap = new Bitmap( 512, 288 );
			camera.RenderToBitmap( bitmap );
			FileSystem.Data.WriteAllBytes( GetThumbnailPath( path, _metadata ), bitmap.ToPng() );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed to write thumbnail for '{path}': {e.Message}" );
		}
		finally
		{
			if ( !hadUiExcluded )
				camera.RenderExcludeTags.Remove( "ui" );

			if ( !hadDevUiExcluded )
				camera.RenderExcludeTags.Remove( "devui" );

			if ( !hadFirstPersonExcluded )
				camera.RenderExcludeTags.Remove( "firstperson" );
		}
	}
}
#endif
