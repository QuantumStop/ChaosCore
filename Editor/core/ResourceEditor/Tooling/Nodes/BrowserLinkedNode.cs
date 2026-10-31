namespace Core.Editor;

public sealed partial class ResourceEditor
{
	private sealed class BrowserLinkedNode : TreeNode
	{
		private readonly ResourceEditor _editor;
		private readonly Asset _asset;
		private readonly bool _usedBy;

		public BrowserLinkedNode( ResourceEditor editor, Entry owner, Asset asset, bool usedBy ) : base( (owner, LinkKey( asset ), usedBy) )
		{
			_editor = editor;
			_asset = asset;
			_usedBy = usedBy;
			Height = Theme.RowHeight + 4;
		}

		public override void OnSelectionChanged( bool state )
		{
			if ( state )
				_editor.OpenLinkedAsset( _asset );
		}

		public override string GetTooltip() => $"{(_usedBy ? "Used by" : "References")}: {_asset.RelativePath}";

		public override void OnPaint( VirtualWidget item )
		{
			PaintSelection( item );
			Paint.SetPen( _usedBy ? _usedColor : Theme.Blue );
			_editor.DrawAssetIcon( new Rect( item.Rect.Left + 2, item.Rect.Top + 4, 16, 16 ), _asset );
			Paint.SetPen( _usedBy ? _usedColor : Theme.Blue );
			var nameRect = item.Rect.Shrink( 24, 0, 4, 0 );
			ResourceTreeView.DrawLinkBadge( ref nameRect, 1, _usedBy, false );
			Paint.SetPen( _usedBy ? _usedColor : Theme.Blue );
			ResourceTreeView.DrawName( nameRect, _asset.Name );
		}
	}
}
