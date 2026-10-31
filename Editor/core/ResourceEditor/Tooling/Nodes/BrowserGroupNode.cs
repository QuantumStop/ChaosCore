namespace Core.Editor;

using System;

public sealed partial class ResourceEditor
{
	private sealed class BrowserGroupNode : TreeNode
	{
		private readonly ResourceEditor _editor;
		private readonly string _title;
		private readonly string _icon;
		private readonly ResourceEditorType[] _types;
		private readonly ResourceEditorType _type;
		private readonly Pixmap _image;
		private readonly bool _projectIcon;

		public BrowserGroupNode( ResourceEditor editor, string key, string title, string icon, ResourceEditorType[] types, ResourceEditorType type = null, Pixmap image = null, bool projectIcon = false ) : base( key )
		{
			_editor = editor;
			_title = title;
			_icon = icon;
			_types = types;
			_type = type;
			_image = image;
			_projectIcon = projectIcon;
			Height = Theme.RowHeight + 4;
		}

		public override bool ExpanderFills => true;

		public override void OnPaint( VirtualWidget item )
		{
			PaintSelection( item );
			Paint.SetPen( Theme.Text );

			var image = _projectIcon ? _editor.LoadProjectIcon() : _image;
			var iconSize = _projectIcon || image is not null ? _projectIconSize : 16;
			var icon = new Rect( item.Rect.Left + 2, item.Rect.Center.y - iconSize * 0.5f, iconSize, iconSize );

			if ( image is not null )
			{
				Paint.BilinearFiltering = true;
				Paint.Draw( icon, image );
				Paint.BilinearFiltering = false;
			}
			else if ( _type is not null )
				_editor.DrawAssetIcon( icon, null, _type );
			else
				Paint.DrawIcon( icon, _icon, 16, TextFlag.Center );

			Paint.SetPen( Theme.Text );
			ResourceTreeView.DrawName( item.Rect.Shrink( iconSize + 8, 0, 4, 0 ), _title );
		}

		public override bool OnContextMenu()
		{
			if ( _type is not null )
			{
				_editor.ShowCreateMenu( [_type] );
				return true;
			}

			if ( Value is not string key || !key.StartsWith( "group:", StringComparison.Ordinal ) || key.EndsWith( ":" + ResourceOrganizationRegistry.FallbackKey, StringComparison.Ordinal ) )
				return true;

			var menu = new Menu( _editor );
			menu.AddOption( "Add resource...", "add", () => _editor.OpenAddWindow( _types ) );
			menu.OpenAtCursor();

			return true;
		}
	}
}
