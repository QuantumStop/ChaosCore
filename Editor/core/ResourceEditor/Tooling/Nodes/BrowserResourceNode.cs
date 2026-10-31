namespace Core.Editor;

using System.Collections.Generic;
using System.Linq;

public sealed partial class ResourceEditor
{
	private sealed class BrowserResourceNode : TreeNode
	{
		private readonly ResourceEditor _editor;
		private readonly Entry _entry;
		private Asset[] _references;
		private Asset[] _users;

		public BrowserResourceNode( ResourceEditor editor, Entry entry, string groupKey ) : base( (entry, groupKey) )
		{
			_editor = editor;
			_entry = entry;
			Height = Theme.RowHeight + 4;
		}

		public override bool HasChildren
		{
			get
			{
				EnsureLinks();

				return _references.Length + _users.Length > 0;
			}
		}

		private void EnsureLinks()
		{
			if ( _references is not null )
				return;

			_references = _editor.GetLinks( _entry, false );
			_users = _editor.GetLinks( _entry, true );
		}

		public void RefreshLinks()
		{
			if ( _references is null )
				return;

			var references = _editor.GetLinks( _entry, false );

			var users = _editor.GetLinks( _entry, true );

			if ( _references.Select( LinkKey ).SequenceEqual( references.Select( LinkKey ) ) && _users.Select( LinkKey ).SequenceEqual( users.Select( LinkKey ) ) )
				return;

			_references = references;
			_users = users;
			Dirty();
		}

		protected override void BuildChildren()
		{
			EnsureLinks();

			var children = new List<TreeNode>();

			foreach ( var usedBy in new[] { false, true } )
			{
				foreach ( var group in (usedBy ? _users : _references).GroupBy( x => x.AssetType.FileExtension ).OrderBy( x => x.First().AssetType.FriendlyName ) )
				{
					var assets = group.ToArray();

					if ( assets.Length == 1 )
						children.Add( new BrowserLinkedNode( _editor, _entry, assets[0], usedBy ) );
					else
						children.Add( new BrowserLinkedTypeNode( _editor, _entry, assets, usedBy ) );
				}
			}

			SetItems( children );
		}

		public override void OnSelectionChanged( bool state )
		{
			if ( state )
				_editor.OpenEntry( _entry );
		}

		public override string GetTooltip() => _entry.Path;

		public override bool OnContextMenu()
		{
			var menu = new Menu( _editor );
			menu.AddOption( "Duplicate (pending Save)", "content_copy", () =>
			{
				_editor.OpenEntry( _entry );

				if ( _editor._current == _entry )
					_editor.SaveAs();
			} ).Enabled = !_entry.Removed;
			menu.AddOption( _entry.Removed ? "Cancel removal" : "Remove (pending Save)", "delete", () => _editor.ToggleRemoval( _entry ) );
			menu.OpenAtCursor();

			return true;
		}

		public override void OnPaint( VirtualWidget item )
		{
			PaintSelection( item );

			if ( _editor._current == _entry )
			{
				Paint.ClearPen();
				Paint.SetBrush( Theme.Blue.WithAlpha( 0.25f ) );
				Paint.DrawRect( new Rect( 0, item.Rect.Top, TreeView.Width, item.Rect.Height ) );
			}

			Paint.SetPen( Theme.Text );
			_editor.DrawAssetIcon( new Rect( item.Rect.Left + 2, item.Rect.Top + 4, 16, 16 ), _entry.Asset, _entry.Type );
			Paint.SetPen( Theme.Text );

			var state = _entry.Removed ? " [Remove]" : _entry.Asset is null ? " [New]" : _entry.Dirty || (_editor._current == _entry && _editor._dirty) ? " *" : "";
			var nameRect = item.Rect.Shrink( 24, 0, 4, 0 );
			ResourceTreeView.DrawLinkBadge( ref nameRect, _users?.Length ?? 0, true );
			ResourceTreeView.DrawLinkBadge( ref nameRect, _references?.Length ?? 0, false );
			Paint.SetPen( Theme.Text );
			ResourceTreeView.DrawName( nameRect, _entry.Name + state );
		}
	}
}
