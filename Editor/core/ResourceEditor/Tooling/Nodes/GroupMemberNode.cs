namespace Core.Editor;

using System;

public sealed partial class ResourceEditor
{
	private sealed class GroupMemberNode : TreeNode
	{
		private readonly ResourceEditor _editor;
		private readonly ResourceEditorType _type;
		private readonly string _name;
		private readonly Action _remove;

		public GroupMemberNode( ResourceEditor editor, string name, ResourceEditorType type, Action remove ) : base( name )
		{
			_editor = editor;
			_type = type;
			_name = name;
			_remove = remove;
			Height = Theme.RowHeight + 4;
		}

		public override string GetTooltip() => _name;

		public override void OnPaint( VirtualWidget item )
		{
			PaintSelection( item );
			Paint.SetPen( Theme.Text );
			_editor.DrawAssetIcon( new Rect( item.Rect.Left + 2, item.Rect.Center.y - 8, 16, 16 ), null, _type );
			Paint.SetPen( Theme.Text );
			var text = _type is null ? _name : $"{_type.Name} (.{_type.Extension})";
			Paint.DrawText( item.Rect.Shrink( 24, 0, 2, 0 ), text, TextFlag.LeftCenter | TextFlag.SingleLine );
		}

		public override bool OnContextMenu()
		{
			var menu = new Menu( _editor );
			menu.AddOption( "Remove from group", "remove", _remove );
			menu.OpenAtCursor();
			return true;
		}
	}
}
