namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed partial class ResourceEditor
{
	private sealed class ResourceMetadata : Widget
	{
		private readonly ResourceEditor _editor;
		private readonly List<(string Text, string Icon, Color Color)> _parts = [];

		public ResourceMetadata( ResourceEditor editor, Widget parent ) : base( parent )
		{
			_editor = editor;
			FixedHeight = 20;
			MinimumWidth = 0;
		}

		protected override void OnResize()
		{
			base.OnResize();
			Update();
		}

		public void RefreshData()
		{
			var muted = Theme.Text.WithAlpha( 0.65f );
			_parts.Clear();

			if ( _editor._resource is not null )
			{
				var count = _editor._serialized?.Count( x => x.IsPublic && x.IsEditable && !x.IsMethod ) ?? 0;
				_parts.Add( (_editor._resource.GetType().Name, null, muted) );
				_parts.Add( ($"{count} properties", null, muted) );
				_parts.Add( ($"{_editor.GetLinks( _editor._current, false ).Length} references", "attach_file", Theme.Blue) );
				_parts.Add( ($"{_editor.GetLinks( _editor._current, true ).Length} used by", "link", _usedColor) );
			}
			else if ( _editor._current is not null )
			{
				_parts.Add( (_editor._current.Type.ResourceType.Name, null, muted) );
				_parts.Add( ("Unavailable", null, muted) );
			}
			else if ( _editor._activeSchemaDraft is not null )
			{
				_parts.Add( (_editor._activeSchemaDraft.Schema.ClassName, null, muted) );
				var original = _editor._activeSchemaDraft.Original;
				var count = (original?.PropertyCount ?? 0) - (original?.Schema.Properties.Count ?? 0) + _editor._activeSchemaDraft.Schema.Properties.Count;
				_parts.Add( ($"{count} properties", null, muted) );
			}

			Update();
		}

		protected override void OnPaint()
		{
			Paint.SetDefaultFont( 9 );
			var muted = Theme.Text.WithAlpha( 0.65f );
			var x = 0f;
			var y = 0f;

			const int lineHeight = 20;

			foreach ( var part in _parts )
			{
				var size = Paint.MeasureText( part.Text );
				var width = size.x + (part.Icon is null ? 0 : 18);

				if ( x > 0 && x + 12 + width > Width )
				{
					x = 0;
					y += lineHeight;
				}
				else if ( x > 0 )
				{
					Paint.SetPen( muted );
					Paint.DrawText( new Rect( x, y, 12, lineHeight ), "\u00b7", TextFlag.Center );
					x += 12;
				}

				Paint.SetPen( part.Color );

				if ( part.Icon is not null )
				{
					Paint.DrawIcon( new Rect( x, y, 16, lineHeight ), part.Icon, 15, TextFlag.Center );
					x += 18;
				}

				Paint.DrawText( new Rect( x, y, Math.Max( 0, Width - x ), lineHeight ), part.Text, TextFlag.LeftCenter | TextFlag.SingleLine );
				x += size.x;
			}

			var height = y + lineHeight;

			if ( FixedHeight != height )
				FixedHeight = height;
		}
	}
}
