namespace Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Core;
using Core.Editor;

[CustomEditor( typeof( LightUnits.ColorPresets ) )]
public sealed class ColorPresetGridWidget : ControlWidget
{
	private const string _presetKey = "light_color";
	private const string _kelvinProperty = "KelvinTemperature";

	private PopupWidget _popup;
	private ColorPresetDropdownButton _selector;

	public override bool SupportsMultiEdit => true;

	public ColorPresetGridWidget( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Row();
		Layout.Spacing = 4;

		_selector = Layout.Add( new ColorPresetDropdownButton( property, this ), 1  );

		var tools = new IconButton( "edit_note", OpenAuthoringMenu )
		{
			FixedSize = Theme.RowHeight,
			ToolTip = "Preset authoring"
		};

		Layout.Add( tools );

		var apply = new Button.Primary( "Apply", "done", this )
		{
			FixedHeight = Theme.RowHeight,
			ToolTip = "Apply color preset"
		};

		apply.Clicked += ApplyColorPreset;

		Layout.Add( apply );
	}

	public override bool IsControlActive => base.IsControlActive || _popup.IsValid();

	public override bool IsControlHovered => base.IsControlHovered || _popup.IsValid();

	private void ApplyColorPreset()
	{
		var template = GetSelectedTemplate();

		float kelvinTemperature;

		if ( template is not null && PresetService.TryGetValue<float>( template, _kelvinProperty, out var capturedKelvin ) )
			kelvinTemperature = capturedKelvin;
		else
			kelvinTemperature = (float)SerializedProperty.GetValue<LightUnits.ColorPresets>();
		
		var kelvin =SerializedProperty.Parent?.GetProperty( _kelvinProperty );

		if ( kelvin is null )
			return;

		SerializedProperty.Parent.NoteStartEdit( kelvin );

		kelvin.SetValue( kelvinTemperature );

		SerializedProperty.Parent.NoteFinishEdit( kelvin );
	}

	public override void StartEditing() => _selector?.StartEditing();
	
	internal void OpenPopup()
	{
		if ( _popup.IsValid() )
			return;

		_popup = new PopupWidget( null )
		{
			Layout = Layout.Column(),
			MinimumWidth = Math.Max( ScreenRect.Width, 420 ),
			MaximumWidth = Math.Max( ScreenRect.Width, 420 ),
			MaximumHeight = 360,
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand
		};

		var list = _popup.Layout.Add( new AssetList( _popup ), 1 );

		list.MultiSelect = false;
		list.ViewMode = AssetListViewMode.MediumIcons;
		list.SetIconMode( 84, 128 );
		list.SetItems( [.. GetEntries()] );

		var current = SerializedProperty.GetValue<LightUnits.ColorPresets>();

		var currentTemplate = GetSelectedTemplate();

		list.SelectItem( list.Items.OfType<ColorPresetGridEntry>().FirstOrDefault( x => x.Matches( current, currentTemplate ) ), skipEvents: true );

		_popup.Position = ScreenRect.BottomLeft;
		_popup.Visible = true;
		_popup.AdjustSize();
		_popup.ConstrainToScreen();

		_popup.OnPaintOverride = PaintPopupBackground;
	}

	private bool PaintPopupBackground()
	{
		Paint.SetBrushAndPen( Theme.ControlBackground );
		Paint.DrawRect( Paint.LocalRect, 0 );

		return true;
	}

	private IEnumerable<ColorPresetGridEntry> GetEntries()
	{
		var assetTemperatures = new HashSet<int>();

		var assetEntries = new List<ColorPresetGridEntry>();

		foreach ( var preset in PresetService.All().Where( x => string.Equals( x.Resource?.PresetType, _presetKey, StringComparison.OrdinalIgnoreCase ) ) )
		{
			if ( preset.Asset is null || preset.Resource is null )
				continue;
			
			if ( !PresetService.TryGetValue<float>( preset.Resource, _kelvinProperty, out var temperature ) )
				continue;

			assetTemperatures.Add( (int)MathF.Round( temperature ) );

			assetEntries.Add( ColorPresetGridEntry.ForAsset( preset, temperature, SerializedProperty, _popup ) );
		}

		foreach ( var entry in assetEntries.OrderBy( x => x.KelvinTemperature ).ThenBy( x => x.Name ) )
			yield return entry;

		foreach ( var value in Enum.GetValues<LightUnits.ColorPresets>() )
		{
			if ( assetTemperatures.Contains( (int)value ) )
				continue;

			yield return ColorPresetGridEntry.ForBuiltIn( value, SerializedProperty, _popup );
		}
	}

	private void OpenAuthoringMenu()
	{
		var menu = new ContextMenu();

		var selected = GetSelectedTemplate();

		menu.AddOption( "New From Current", "add", CreatePresetFromCurrent );
		menu.AddOption( "Update Selected From Current", "save", () => UpdatePresetFromCurrent( selected ) ).Enabled = selected is not null;

		menu.AddSeparator();

		menu.AddOption( "Open Selected Preset", "open_in_new", () => OpenSelectedPreset( selected ) ).Enabled = selected is not null;

		menu.OpenAtCursor();
	}

	private void CreatePresetFromCurrent()
	{
		var target = SerializedProperty.Parent?.Targets?.FirstOrDefault();

		if ( target is null ) 
			return;

		var targetInfo = PresetRegistry.Find( _presetKey, target.GetType() );

		if ( targetInfo is null )
		{
			Log.Warning( $"No preset target '{_presetKey}' is registered for '{target.GetType().Name}'." );

			return;
		}

		var mode = targetInfo.Modes.FirstOrDefault();

		if ( mode is null ) return;

		var presetFolder = Path.Combine( Project.Current.GetAssetsPath(), "presets" );

		Directory.CreateDirectory( presetFolder );

		var filename = EditorUtility.SaveFileDialog( $"Create {targetInfo.Name}", "preset", Path.Combine( presetFolder, "new_light_color.preset" ) );

		if ( string.IsNullOrWhiteSpace( filename ) )
			return;

		var captured = PresetService.Capture( _presetKey, target, Path.GetFileNameWithoutExtension( filename ), mode.Key );

		if ( captured?.Resource is null )
		{
			Log.Warning( $"Unable to capture light color preset from '{target.GetType().Name}'." );

			return;
		}

		captured.Resource.PresetIcon = ColorPresetGridEntry.GetInfo( SerializedProperty.GetValue< LightUnits.ColorPresets >() ).Icon;

		var asset = PresetService.SaveAs( captured, filename );

		if ( asset is null ) 
			return;

		var loaded = PresetService.Load( asset );

		if ( loaded?.Resource is not null )
			SetSelectedTemplate( loaded.Resource );

		MainAssetBrowser.Instance?.Local.OnAssetCreated( asset, filename );

		MainAssetBrowser.Instance?.Local.UpdateAssetList();

		MainAssetBrowser.Instance?.Local.FocusOnAsset( asset );

		EditorUtility.InspectorObject = asset;

		_popup?.Close();
	}

	private void UpdatePresetFromCurrent( GenericPresetResource selected )
	{
		if ( selected is null ) 
			return;

		var target = SerializedProperty.Parent?.Targets?.FirstOrDefault();

		if ( target is null ) 
			return;

		var captured = PresetService.Capture( selected.PresetType, target, selected.PresetName, selected.PresetMode );

		if ( captured?.Resource is null ) 
			return;

		selected.PayloadJson = captured.Resource.PayloadJson;

		var asset = FindPresetAsset( selected );

		if ( asset is null ) 
			return;

		var wrapper = new PresetAsset
		{
			Asset = asset,
			Resource = selected,
			Target = captured.Target
		};

		if ( !PresetService.Save( wrapper ) )
			return;

		SetSelectedTemplate( selected );

		MainAssetBrowser.Instance?.Local.UpdateAssetList();
	}

	private void OpenSelectedPreset( GenericPresetResource selected )
	{
		var asset = FindPresetAsset( selected );

		if ( asset is null )
			return;

		IAssetEditor.OpenInEditor( asset, out _ );

		MainAssetBrowser.Instance?.Local.FocusOnAsset( asset );
	}

	private GenericPresetResource GetSelectedTemplate()
	{
		return SerializedProperty.Parent?.GetProperty( "ColorPresetTemplate" ) ?.GetValue< GenericPresetResource >();
	}

	internal string GetCurrentLabel()
	{
		if ( GetSelectedTemplate() is { } preset )
		{
			if ( !string.IsNullOrWhiteSpace( preset.PresetName ) )
				return preset.PresetName;

			return Path.GetFileNameWithoutExtension( preset.SourceAssetPath ?? preset.ResourcePath );
		}

		return ColorPresetGridEntry.GetInfo( SerializedProperty.GetValue< LightUnits.ColorPresets >() ).Label;
	}

	internal string GetCurrentIcon()
	{
		if ( GetSelectedTemplate() is { } preset )
			return string.IsNullOrWhiteSpace( preset.PresetIcon ) ? "light_mode" : preset.PresetIcon;

		return ColorPresetGridEntry.GetInfo( SerializedProperty.GetValue< LightUnits.ColorPresets >() ).Icon;
	}

	private void SetSelectedTemplate( GenericPresetResource preset )
	{
		var template = SerializedProperty.Parent?.GetProperty( "ColorPresetTemplate" );

		if ( template is null ) 
			return;

		SerializedProperty.Parent.NoteStartEdit( template );

		template.SetValue( preset );

		SerializedProperty.Parent.NoteFinishEdit( template );
	}

	internal static bool TryLoadLightColorPreset( string path, out GenericPresetResource preset )
	{
		preset = null;

		if ( string.IsNullOrWhiteSpace( path ) )
			return false;

		var asset = AssetSystem.FindByPath( path );

		if ( asset is null ) 
			return false;

		var loaded = PresetService.Load( asset );

		if ( loaded?.Resource is null )
			return false;

		if ( !string.Equals( loaded.Resource.PresetType, _presetKey, StringComparison.OrdinalIgnoreCase ) )
			return false;

		if ( !PresetService.TryGetValue<float>( loaded.Resource, _kelvinProperty, out _ ) )
			return false;

		preset = loaded.Resource;

		return true;
	}

	private static Asset FindPresetAsset( GenericPresetResource preset )
	{
		if ( preset is null ) return null;

		if ( !string.IsNullOrWhiteSpace( preset.SourceAssetPath ) )
		{
			var asset = AssetSystem.FindByPath( preset.SourceAssetPath );

			if ( asset is not null ) return asset;
		}

		if ( !string.IsNullOrWhiteSpace( preset.ResourcePath ) )
		{
			return AssetSystem.FindByPath( preset.ResourcePath );
		}

		return null;
	}
}

sealed class ColorPresetDropdownButton : Widget
{
	private readonly SerializedProperty _property;
	private readonly ColorPresetGridWidget _owner;

	public ColorPresetDropdownButton( SerializedProperty property, ColorPresetGridWidget owner ) : base( null )
	{
		_property = property;
		_owner = owner;
		Cursor = CursorShape.Finger;
		FixedHeight = Theme.RowHeight;
	}

	public void StartEditing()
	{
		if ( !Enabled ) 
			return;

		_owner?.OpenPopup();
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( !Enabled ) 
			return;

		if ( !e.LeftMouseButton ) 
			return;

		_owner?.OpenPopup();
	}

	protected override void OnPaint()
	{
		var color = IsUnderMouse ? Theme.Blue : Theme.TextControl;

		var rect = LocalRect.Shrink( 8, 0 );
		var iconRect = rect;

		iconRect.Width = 18;

		var textRect = rect.Shrink( 24, 0, 24, 0 );

		Paint.SetPen( _property.IsMultipleDifferentValues ? Theme.MultipleValues : color );
	
		Paint.SetDefaultFont();

		Paint.DrawIcon( iconRect, _owner?.GetCurrentIcon() ?? "light_mode", 16, TextFlag.LeftCenter );
		
		Paint.DrawText( textRect, _property.IsMultipleDifferentValues ? "Multiple Values" : _owner?.GetCurrentLabel() ?? "None", TextFlag.LeftCenter | TextFlag.SingleLine );

		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}
}

sealed class ColorPresetGridEntry : IAssetListEntry
{
	public LightUnits.ColorPresets? Value { get; }

	public string AssetPath { get; }

	public float KelvinTemperature { get; }

	public string Name => Info.Label;

	public ColorPresetInfo Info { get; }

	public SerializedProperty Property { get; }

	public PopupWidget Popup { get; }

	public Asset Asset { get; }

	private ColorPresetGridEntry( LightUnits.ColorPresets? value, Asset asset, string assetPath, float kelvinTemperature, 
		ColorPresetInfo info, SerializedProperty property, PopupWidget popup )
	{
		Value = value;
		Asset = asset;
		AssetPath = assetPath;
		KelvinTemperature = kelvinTemperature;
		Info = info;
		Property = property;
		Popup = popup;
	}

	public static ColorPresetGridEntry ForBuiltIn( LightUnits.ColorPresets value, SerializedProperty property, PopupWidget popup )
	{
		return new ColorPresetGridEntry( value, null, null, (float)value, GetInfo( value ), property, popup );
	}

	public static ColorPresetGridEntry ForAsset( PresetAsset preset, float temperature, SerializedProperty property, PopupWidget popup )
	{
		var label = preset.Name;

		var description = string.IsNullOrWhiteSpace( preset.Description ) ? $"{temperature:0}K" : preset.Description;

		var icon = string.IsNullOrWhiteSpace( preset.Icon ) ? "light_mode" : preset.Icon;

		return new ColorPresetGridEntry( null, preset.Asset, preset.Asset.Path ?? preset.Asset.RelativePath, temperature,
			new ColorPresetInfo( label, description, icon ), property, popup
		);
	}

	public string GetStatusText()
	{
		return $"{Info.Label} - {Info.Description}";
	}

	public bool OnClicked( AssetList list )
	{
		if ( Value.HasValue )
		{
			Property.Parent?.NoteStartEdit( Property );
			Property.SetValue( Value.Value );
			Property.Parent?.NoteFinishEdit( Property );

			SetTemplate( null );
		}
		else
		{
			if ( ColorPresetGridWidget.TryLoadLightColorPreset( AssetPath, out var preset ) )
				SetTemplate( preset );
		}

		Popup?.Close();

		return true;
	}

	public bool Matches( LightUnits.ColorPresets current, GenericPresetResource template )
	{
		if ( template is not null )
		{
			var templatePath = template.SourceAssetPath ?? template.ResourcePath;

			return string.Equals( AssetPath, templatePath, StringComparison.OrdinalIgnoreCase );
		}

		if ( !Value.HasValue )
			return MathF.Abs( KelvinTemperature - (float)current ) < 0.5f;
		
		return Value == current;
	}

	public void OnScrollEnter()
	{
		if ( Asset is not null && !Asset.HasCachedThumbnail )
			Asset.GetAssetThumb( true );
	}

	public void DrawIcon( Rect rect )
	{
		var pixmap = Asset?.GetAssetThumb( true ) ?? ColorPresetFallbackThumbnailCache.Get( _thumbnailKey, KelvinTemperature );

		Paint.BilinearFiltering = true;
		Paint.Draw( rect, pixmap, borderRadius: 2 );
		Paint.BilinearFiltering = false;
	}

	public void DrawOverlay( Rect rect ) { }

	public void DrawText( Rect rect )
	{
		Paint.SetDefaultFont( 7 );
		Paint.SetPen( Theme.Text );

		var topLine = rect;

		topLine.Height = 14;

		var iconRect = topLine;
		
		iconRect.Width = 14;

		Paint.DrawIcon( iconRect, Info.Icon, 12, TextFlag.LeftTop );

		var labelRect = topLine.Shrink( 16, 0, 0, 0 );

		Paint.DrawText( labelRect, Info.Label, TextFlag.LeftTop | TextFlag.SingleLine );
		Paint.SetPen( Theme.Text.WithAlpha( 0.55f ) );

		var valueRect = rect.Shrink( 16, 0, 0, 0 );

		valueRect.Top += 14;
		valueRect.Height = 14;

		Paint.DrawText( valueRect, $"{KelvinTemperature:0}K", TextFlag.LeftTop | TextFlag.SingleLine );
	}

	private string _thumbnailKey => Value?.ToString() ?? AssetPath;

	private void SetTemplate( GenericPresetResource preset )
	{
		var template = Property.Parent?.GetProperty( "ColorPresetTemplate" );

		if ( template is null ) 
			return;

		Property.Parent.NoteStartEdit( template );

		template.SetValue( preset );

		Property.Parent.NoteFinishEdit( template );
	}

	public static ColorPresetInfo GetInfo( LightUnits.ColorPresets value )
	{
		return value switch
		{
			LightUnits.ColorPresets.Match => new( "Match", "1700K", "fireplace" ),

			LightUnits.ColorPresets.Candle => new( "Candle", "1830K", "cake" ),

			LightUnits.ColorPresets.SunSunrise => new( "Sun Sunrise", "2500K", "wb_twilight" ),

			LightUnits.ColorPresets.TungstenLampA => new( "Tungsten Lamp A", "2900K", "wb_iridescent" ),

			LightUnits.ColorPresets.FluorescentLights => new( "Fluorescent Lights", "3200K", "wb_iridescent" ),

			LightUnits.ColorPresets.TungstenLampB => new( "Tungsten Lamp B", "4000K", "wb_incandescent" ),

			LightUnits.ColorPresets.SunNoon => new( "Sun Noon", "5000K", "light_mode" ),

			LightUnits.ColorPresets.Daylight => new( "Daylight", "5600K", "wb_sunny" ),

			LightUnits.ColorPresets.NeutralWhite => new( "Neutral White", "6500K", "contrast" ),

			LightUnits.ColorPresets.OutdoorShade => new( "Outdoor Shade", "7000K", "wb_shade" ),

			LightUnits.ColorPresets.Overcast => new( "Overcast", "7500K", "foggy" ),

			LightUnits.ColorPresets.PartlyCloudy => new( "Partly Cloudy", "9000K", "cloud" ),

			_ => new( value.ToString().ToTitleCase(), $"{(int)value}K", "light_mode" ) 
		};
	}
}

readonly record struct ColorPresetInfo( string Label, string Description, string Icon );

file static class ColorPresetFallbackThumbnailCache
{
	private static readonly Dictionary< string, Pixmap > _fallbackCache = [];

	public static Pixmap Get( string key, float kelvinTemperature )
	{
		return GetFallback( key, kelvinTemperature );
	}

	private static Pixmap CreateFallback( float kelvinTemperature )
	{
		const int size = 128;

		var pixmap = new Pixmap( size, size );

		var color = LightUnits.CorrelatedColorTemperatureToRGB( kelvinTemperature );

		using ( Paint.ToPixmap( pixmap ) )
		{
			Paint.ClearPen();

			Paint.SetBrushLinear( Vector2.Zero, new Vector2( 0, size ), color.Lighten( 0.25f ), color.Darken( 0.35f ) );
			Paint.DrawRect( new Rect( 0, 0, size, size ) );

			Paint.SetPen( Color.White.WithAlpha( 0.8f ) );
			Paint.DrawIcon( new Rect( 0, 0, size, size ), "light_mode", 40, TextFlag.Center );
		}

		return pixmap;
	}

	private static Pixmap GetFallback( string key, float kelvinTemperature )
	{
		if ( _fallbackCache.TryGetValue( key, out var pixmap ) )
			return pixmap;
		
		pixmap = CreateFallback( kelvinTemperature );

		_fallbackCache[key] = pixmap;

		return pixmap;
	}
}
