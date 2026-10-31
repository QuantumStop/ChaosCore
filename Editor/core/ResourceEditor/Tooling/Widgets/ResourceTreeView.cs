namespace Core.Editor;

public sealed partial class ResourceEditor
{
	private sealed class ResourceTreeView : TreeView
	{
		public ResourceTreeView( Widget parent ) : base( parent )
		{
			IndentWidth = 18;
			ExpandWidth = 18;
			ItemSpacing = 0;
			Margin = new Sandbox.UI.Margin( 2, 2, 2, 2 );
		}

		internal static void DrawName( Rect rect, string name )
		{
			if ( rect.Width <= 0 )
				return;

			Paint.DrawText( rect, Paint.GetElidedText( name, rect.Width, ElideMode.Right, TextFlag.SingleLine ), TextFlag.LeftCenter | TextFlag.SingleLine );
		}

		internal static void DrawLinkBadge( ref Rect nameRect, int count, bool usedBy, bool showCount = true )
		{
			if ( count == 0 )
				return;

			var text = showCount ? count.ToString() : "";
			var width = 18 + (showCount ? Paint.MeasureText( text ).x + 4 : 0);
		
			if ( nameRect.Width < width + 36 )
				return;

			var badge = new Rect( nameRect.Right - width, nameRect.Top, width, nameRect.Height );
			nameRect = nameRect.Shrink( 0, 0, width + 4, 0 );
			Paint.SetPen( usedBy ? _usedColor : Theme.Blue );
			Paint.DrawIcon( new Rect( badge.Left, badge.Top, 18, badge.Height ), usedBy ? "link" : "attach_file", 14, TextFlag.Center );
		
			if ( showCount )
				Paint.DrawText( badge.Shrink( 18, 0, 0, 0 ), text, TextFlag.LeftCenter | TextFlag.SingleLine );
		}
		
		protected override void PaintItem( VirtualWidget item )
		{
			Paint.Antialiasing = false;
			Paint.ClearPen();
			Paint.SetBrush( Color.Lerp( Theme.SurfaceBackground, Theme.Text, 0.16f ).WithAlpha( 1 ) );
			var hasExpander = item.HasChildren && item.Object is not TreeNode { ExpanderHidden: true };
			var branchEnd = item.Rect.Left + item.Column * IndentWidth + (hasExpander ? 2 : ExpandWidth - 2);

			var node = item.Object as TreeNode;
			for ( var level = item.Column - 1; level >= 0 && node?.Parent is not null; level--, node = node.Parent )
			{
				var x = item.Rect.Left + level * IndentWidth + ExpandWidth * 0.5f;
				var continues = HasFollowingSibling( node );
				var immediate = level == item.Column - 1;
				if ( continues || immediate )
				{
					var bottom = continues ? item.Rect.Bottom : item.Rect.Center.y + 1;
					Paint.DrawRect( new Rect( x, item.Rect.Top, 1, bottom - item.Rect.Top ) );
				}

				if ( immediate )
					Paint.DrawRect( new Rect( x + 1, item.Rect.Center.y, branchEnd - x - 1, 1 ) );
			}

			Paint.Antialiasing = true;
			base.PaintItem( item );
		}

		/// <summary>
		/// Keeps ancestor connectors only where another visible sibling follows.
		/// </summary>
		private bool HasFollowingSibling( TreeNode node )
		{
			var found = false;
			foreach ( var sibling in node.Parent.Children )
			{
				if ( found && sibling.Enabled && (ShouldDisplayChild?.Invoke( sibling ) ?? true) )
					return true;

				if ( sibling == node )
					found = true;
			}

			return false;
		}
	}
}
