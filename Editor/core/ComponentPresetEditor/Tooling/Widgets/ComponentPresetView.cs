namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Linq;

using Sandbox;

public sealed class ComponentPresetsView : Widget, AssetSystem.IEventListener
{
	private readonly ComponentPresetEditor _editor;

	private readonly LineEdit _search;
	private readonly ScrollArea _scroll;
	private readonly Widget _contents;

	private readonly List<PresetAsset> _entries = new();

	public ComponentPresetsView( ComponentPresetEditor editor ) : base( null )
	{
		_editor = editor;

		Layout = Layout.Column();
		Layout.Margin = 4;
		Layout.Spacing = 4;

		// Search

		_search = new LineEdit( this )
		{
			PlaceholderText = "Search presets..."
		};

		_search.TextChanged += _ => RebuildView();

		Layout.Add( _search );

		// Scrollable preset categories

		_scroll = new ScrollArea( this );
		Layout.Add( _scroll, 1 );

		_contents = new Widget( _scroll )
		{
			Layout = Layout.Column()
		};

		_contents.SetSizeMode( SizeMode.Flexible, SizeMode.CanShrink );

		_contents.Layout.Margin = 0;
		_contents.Layout.Spacing = 0;

		_scroll.Canvas = _contents;

		Reload();
	}

	public void Reload()
	{
		_entries.Clear();

		_entries.AddRange(
			PresetService.All()
				.OrderBy( GetCategoryName )
				.ThenBy( x => x.Target?.Name )
				.ThenBy( x => x.Name )
		);

		RebuildView();
	}

	public void UpdatePreset( PresetAsset preset )
	{
		// Editor's current PresetAsset and list entries can be
		// separate loaded instances, we reload to keep metadata synced.

		Reload();
	}

	private void RebuildView()
	{
		using var suspend = SuspendUpdates.For( _contents );

		_contents.Layout.Clear( true );

		var query = _search.Text?.Trim();

		IEnumerable<PresetAsset> visible = _entries;

		if ( !string.IsNullOrWhiteSpace( query ) )
		{
			visible = visible.Where( x =>
				Contains( x.Name, query ) ||
				Contains( x.Description, query ) ||
				Contains( x.Target?.Name, query ) ||
				Contains( x.Target?.Key, query ) ||
				Contains( x.Target?.Category, query ) ||
				Contains( x.Asset?.Name, query )
			);
		}

		var groups = visible
			.GroupBy( GetCategoryName )
			.OrderBy( x => x.Key )
			.ToArray();

		if ( groups.Length == 0 )
		{
			AddEmptyState( string.IsNullOrWhiteSpace( query ) ? "No presets found" : "No matching presets" );

			return;
		}

		foreach ( var group in groups )
			AddCategory( group.Key, group );
	}

	private void AddCategory( string category, IEnumerable<PresetAsset> presets )
	{
		var presetArray = presets.ToArray();

		if ( presetArray.Length == 0 )
			return;

		var section = new PresetCollapsibleSection( category, GetCategoryIcon( category, presetArray ), Theme.Blue, true, _contents )
		{
			StateCookieName =
				$"ComponentPresetEditor.Presets.Category.{category}"
		};

		section.RestoreState();

		// Rows should touch each other like Inspector controls.

		section.Content.Layout.Margin = 0;
		section.Content.Layout.Spacing = 0;

		foreach ( var preset in presetArray )
		{
			var row = new PresetBrowserRow( preset, _editor, section.Content );

			section.Content.Layout.Add( row );
		}

		_contents.Layout.Add( section );
	}

	private void AddEmptyState( string text )
	{
		var empty = new Widget( _contents )
		{
			Layout = Layout.Column()
		};

		empty.Layout.Margin =
			new Sandbox.UI.Margin( 8, 8 );

		var label = new Label( text, empty )
		{
			Color = Theme.Text.WithAlpha( 0.55f )
		};

		empty.Layout.Add( label );
		_contents.Layout.Add( empty );
	}

	private static string GetCategoryName( PresetAsset preset )
	{
		if ( !string.IsNullOrWhiteSpace( preset?.Target?.Category ) )
			return preset.Target.Category;

		return "General";
	}

	private static string GetCategoryIcon( string category, IReadOnlyList<PresetAsset> presets )
	{
		// If this category contains only one target type, use its icon.

		var distinctTargets = presets
			.Select( x => x.Target?.Key )
			.Where( x => !string.IsNullOrWhiteSpace( x ) )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToArray();

		if ( distinctTargets.Length == 1 )
		{
			var icon = presets
				.Select( x => x.Target?.Icon )
				.FirstOrDefault( x => !string.IsNullOrWhiteSpace( x ) );

			if ( !string.IsNullOrWhiteSpace( icon ) )
				return icon;
		}

		// This is a fallback and it's lame, will do this better in the future

		return category?.ToLowerInvariant() switch
		{
			"rendering" => "visibility",
			"lighting" => "light_mode",
			"light" => "light_mode",

			"ui" => "web",
			"user interface" => "web",

			"audio" => "volume_up",
			"weapons" => "target",
			"logic" => "account_tree",
			"visual" => "visibility",

			_ => "folder"
		};
	}

	private static bool Contains( string value, string query )
	{
		return value?.Contains( query, StringComparison.OrdinalIgnoreCase ) ?? false;
	}

	void AssetSystem.IEventListener.OnAssetChanged( Asset asset )
	{
		if ( !PresetService.IsPresetAsset( asset ) )
			return;

		Reload();
	}

	void AssetSystem.IEventListener.OnAssetSystemChanges()
	{
		Reload();
	}

	private sealed class PresetBrowserRow : Widget
	{
		private readonly PresetAsset _preset;
		private readonly ComponentPresetEditor _editor;

		public PresetBrowserRow( PresetAsset preset, ComponentPresetEditor editor, Widget parent = null ) : base( parent )
		{
			_preset = preset;
			_editor = editor;

			FixedHeight = Theme.RowHeight + 24;

			Cursor = CursorShape.Finger;
			MouseTracking = true;

			ToolTip = GetToolTipText();
		}

		private bool IsSelected => _preset?.Asset is not null && _editor?.CurrentAsset == _preset.Asset;

		protected override void OnMouseClick( MouseEvent e )
		{
			base.OnMouseClick( e );

			if ( !e.LeftMouseButton || _preset?.Asset is null )
				return;

			_editor.SelectPreset( _preset.Asset );

			Update();
		}

		protected override void OnDoubleClick( MouseEvent e )
		{
			base.OnDoubleClick( e );

			if ( !e.LeftMouseButton || _preset?.Asset is null )
				return;

			_editor.AssetOpen( _preset.Asset );
		}

		protected override void OnPaint()
		{
			base.OnPaint();

			var selected = IsSelected;

			// Selection and hover

			if ( selected )
			{
				Paint.ClearPen();
				Paint.SetBrush( Theme.Blue.WithAlpha( 0.35f ) );
				Paint.DrawRect( LocalRect, 2 );
			}
			else if ( Paint.HasMouseOver )
			{
				Paint.ClearPen();
				Paint.SetBrush( Theme.Blue.WithAlpha( 0.10f ) );
				Paint.DrawRect( LocalRect, 2 );
			}

			var rect = LocalRect.Shrink( 8, 0 );

			// Preview

			var thumbnailRect = rect;
			thumbnailRect.Width = 46;
			thumbnailRect.Height = 46;
			thumbnailRect.Top = LocalRect.Center.y - thumbnailRect.Height * 0.5f;

			var thumbnail = _preset.Asset?.GetAssetThumb( true );

			if ( thumbnail is not null )
			{
				Paint.BilinearFiltering = true;

				Paint.Draw(
					thumbnailRect,
					thumbnail
				);

				Paint.BilinearFiltering = false;
			}
			else
			{
				Paint.SetPen( selected ? Color.White : Theme.Text );

				Paint.DrawIcon( thumbnailRect, string.IsNullOrWhiteSpace( _preset.Icon ) ? "tune" : _preset.Icon, 16, TextFlag.Center );
			}

			// Name

			var textRect = rect;
			textRect.Left += 58;
			textRect.Right -= 4;

			var nameRect = textRect;
			nameRect.Bottom = LocalRect.Center.y + 1;

			Paint.SetHeadingFont( 10, 450, sizeInPixels: true );

			Paint.SetPen( selected ? Color.White : Theme.Text );

			Paint.DrawText( nameRect, _preset.Name, TextFlag.LeftBottom | TextFlag.SingleLine );

			// Target below the name

			var subRect = textRect;
			subRect.Top = LocalRect.Center.y;

			Paint.SetDefaultFont( 7 );

			Paint.SetPen( Theme.Text.WithAlpha( selected ? 0.75f : 0.55f ) );

			Paint.DrawText( subRect, _preset.Target?.Name ?? _preset.Target?.Key ?? string.Empty,
				TextFlag.LeftCenter | TextFlag.SingleLine );
		}

		private string GetToolTipText()
		{
			if ( _preset is null )
				return null;

			var text =
				$"<strong>{_preset.Name}</strong>";

			if ( !string.IsNullOrWhiteSpace( _preset.Target?.Name ) )
				text += $"<br/>{_preset.Target.Name}";

			if ( !string.IsNullOrWhiteSpace( _preset.Description ) )
				text += $"<br/><br/>{_preset.Description}";

			return text;
		}
	}
}
