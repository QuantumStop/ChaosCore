namespace Core.Editor;

using System;

public sealed partial class ResourceEditor
{
	private sealed class ChoiceNode( string title, Action choose, Type type = null, ResourceEditor editor = null, ResourceEditorType resourceType = null ) : TreeNode( title )
	{
		private readonly string _title = title;
		private readonly Action _choose = choose;
		private readonly Type _type = type;
		private readonly ResourceEditor _editor = editor;
		private readonly ResourceEditorType _resourceType = resourceType;

		public override void OnSelectionChanged( bool state )
		{
			if ( state )
				_choose();
		}

		public override string GetTooltip() => _type?.FullName ?? _title;

		public override void OnPaint( VirtualWidget item )
		{
			PaintSelection( item );
			Paint.SetPen( Theme.Text );

			if ( _resourceType is not null )
				_editor.DrawAssetIcon( new Rect( item.Rect.Left, item.Rect.Center.y - 8, 16, 16 ), null, _resourceType );
			else
				Paint.DrawIcon( new Rect( item.Rect.Left, item.Rect.Top, 20, item.Rect.Height ), _type?.IsEnum == true ? "list" : "description", 14, TextFlag.Center );

			Paint.SetPen( Theme.Text );
			Paint.DrawText( item.Rect.Shrink( 24, 0, 2, 0 ), _title, TextFlag.LeftCenter | TextFlag.SingleLine );
		}
	}
}
