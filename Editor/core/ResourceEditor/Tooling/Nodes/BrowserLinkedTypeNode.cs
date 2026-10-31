namespace Core.Editor;

using System.Linq;

public sealed partial class ResourceEditor
{
	private sealed class BrowserLinkedTypeNode : TreeNode
	{
		private readonly ResourceEditor _editor;
		private readonly Asset[] _assets;
		private readonly bool _usedBy;

		public BrowserLinkedTypeNode( ResourceEditor editor, Entry owner, Asset[] assets, bool usedBy ) : base( (owner, "linked-type:" + assets[0].AssetType.FileExtension, usedBy) )
		{
			_editor = editor;
			_assets = assets;
			_usedBy = usedBy;
			Height = Theme.RowHeight + 4;
			// SetItems enumerates twice, so retain the same nodes when it assigns their parents.
			SetItems( [.. assets.Select( x => new BrowserLinkedNode( editor, owner, x, usedBy ) )] );
		}

		public override bool ExpanderFills => true;

		public override string GetTooltip() => $"{(_usedBy ? "Used by" : "References")}: {_assets[0].AssetType.FriendlyName} ({_assets.Length})";

		public override void OnPaint( VirtualWidget item )
		{
			PaintSelection( item );
			_editor.DrawAssetIcon( new Rect( item.Rect.Left + 2, item.Rect.Top + 4, 16, 16 ), _assets[0] );
			Paint.SetPen( _usedBy ? _usedColor : Theme.Blue );
			var nameRect = item.Rect.Shrink( 24, 0, 4, 0 );
			ResourceTreeView.DrawLinkBadge( ref nameRect, 1, _usedBy, false );
			Paint.SetPen( _usedBy ? _usedColor : Theme.Blue );
			ResourceTreeView.DrawName( nameRect, $"{_assets[0].AssetType.FriendlyName} ({_assets.Length})" );
		}
	}
}
