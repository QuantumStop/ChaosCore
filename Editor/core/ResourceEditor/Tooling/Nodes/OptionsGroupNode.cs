namespace Core.Editor;

using System;

public sealed partial class ResourceEditor
{
	private sealed class OptionsGroupNode : TreeNode
	{
		private readonly ResourceGroupOptions _group;
		private readonly Action _select;
		private readonly Action _remove;

		public OptionsGroupNode( ResourceGroupOptions group, Action select, Action remove ) : base( group )
		{
			_group = group;
			_select = select;
			_remove = remove;
			Height = Theme.RowHeight + 4;
		}

		public override void OnSelectionChanged( bool selected )
		{
			if ( selected )
				_select();
		}

		public override string GetTooltip() => $"{_group.Name} ({_group.ResourceTypes.Count} resource types)";

		public override void OnPaint( VirtualWidget item )
		{
			PaintSelection( item );
			Paint.SetPen( Theme.Text );
			Paint.DrawIcon( new Rect( item.Rect.Left + 2, item.Rect.Top, 18, item.Rect.Height ), _group.Icon, 16, TextFlag.Center );
			Paint.DrawText( item.Rect.Shrink( 26, 0, 30, 0 ), _group.Name, TextFlag.LeftCenter | TextFlag.SingleLine );
			Paint.SetPen( Theme.Text.WithAlpha( 0.65f ) );
			Paint.DrawText( new Rect( item.Rect.Right - 28, item.Rect.Top, 26, item.Rect.Height ), _group.ResourceTypes.Count.ToString(), TextFlag.RightCenter );
		}

		public override bool OnContextMenu()
		{
			var menu = new Menu( TreeView );
			menu.AddOption( "Remove group", "delete", _remove );
			menu.OpenAtCursor();
			return true;
		}
	}
}
