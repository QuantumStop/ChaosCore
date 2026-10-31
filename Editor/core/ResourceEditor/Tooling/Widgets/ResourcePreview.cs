namespace Core.Editor;

public sealed partial class ResourceEditor
{
	private sealed class ResourcePreview : Widget
	{
		private readonly ResourceEditor _editor;

		public ResourcePreview( ResourceEditor editor, Widget parent ) : base( parent )
		{
			_editor = editor;
			FixedSize = 56;
		}

		protected override void OnPaint()
		{
			var entry = _editor._current;

			if ( entry is null )
			{
				if ( _editor._activeSchemaDraft is not null )
				{
					Paint.SetPen( Theme.Text );
					Paint.DrawIcon( LocalRect, _editor._activeSchemaDraft.Schema.Icon, 36, TextFlag.Center );
				}

				return;
			}

			var thumbnail = entry.Asset?.GetAssetThumb( true );

			if ( thumbnail is not null )
			{
				Paint.BilinearFiltering = true;
				Paint.Draw( LocalRect.Shrink( 2 ), thumbnail );
				Paint.BilinearFiltering = false;
			}
			else
			{
				var typeIcon = entry.Asset?.AssetType?.Icon64;

				if ( typeIcon is not null )
					Paint.Draw( LocalRect.Shrink( 2 ), typeIcon );
				else
				{
					Paint.SetPen( Theme.Text );
					Paint.DrawIcon( LocalRect, entry.Type.Icon, 36, TextFlag.Center );
				}
			}
		}
	}
}
