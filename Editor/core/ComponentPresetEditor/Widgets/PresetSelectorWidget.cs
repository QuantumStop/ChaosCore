namespace Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Core;
using Core.Editor;

[CustomEditor( typeof( GenericPresetResource ) )]
public class PresetSelectorCW : ControlWidget
{
	public override bool SupportsMultiEdit => true;

	private readonly SerializedProperty _property;
	private readonly ComboBox _comboBox;

	public PresetSelectorCW( SerializedProperty property ) : base( property )
	{
		_property = property;

		Layout = Layout.Row();
		Layout.Spacing = 4;

		_comboBox = new ComboBox( this );

		RebuildPresets();

		Layout.Add( _comboBox, 1 );

		var tools = new IconButton( "edit_note", OpenAuthoringMenu )
		{
			FixedSize = Theme.RowHeight,
			ToolTip = "Preset authoring"
		};

		Layout.Add( tools );

		var apply = new Button.Primary( "Apply", "done", this )
		{
			FixedHeight = Theme.RowHeight,
			ToolTip = "Apply preset"
		};

		apply.Clicked += ApplySelectedPreset;

		Layout.Add( apply );
	}

	private void RebuildPresets()
	{
		_comboBox.Clear();

		var current = _property.GetValue<GenericPresetResource>();

		var presetKey = ResolvePresetKey();
		var target = GetCaptureTarget( _property );

		var icon = ResolvePresetIcon( presetKey, target );

		_comboBox.AddItem( "Default", icon, () => SetPreset( _property, null ), selected: current is null );

		if ( string.IsNullOrWhiteSpace( presetKey ) )
			return;


		var knownPaths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var preset in PresetService.FindFor( target, presetKey ) )
		{
			if ( preset?.Asset is null || preset.Resource is null )
				continue;

			var path = preset.Asset.Path ?? preset.Asset.RelativePath;

			if ( !knownPaths.Add( path ) )
				continue;

			var captured = preset;

			_comboBox.AddItem( GetPresetLabel( captured ), captured.Icon, () => SelectPresetAsset( captured ),
				path, IsCurrentPreset( current, captured.Resource ) );
		}
	}

	private static void SetPreset( SerializedProperty property, GenericPresetResource preset )
	{
		if ( property is null )
			return;

		property.Parent?.NoteStartEdit( property );

		property.SetValue( preset );

		property.Parent?.NoteFinishEdit( property );
	}

	private void SelectPresetAsset( PresetAsset preset )
	{
		if ( preset?.Asset is null )
			return;

		var resource = preset.Asset.LoadResource<GenericPresetResource>();

		if ( resource is null )
			return;

		// IMPORTANT: Setting the serialized property may cause the inspector
		// to rebuild and destroy this ControlWidget.

		// Do not access this widget after SetPreset().
		SetPreset( _property, resource );
	}

	private void ApplySelectedPreset()
	{
		var preset = _property.GetValue<GenericPresetResource>();

		if ( preset is null )
			return;

		var parent = _property.Parent;
		var targets = parent?.Targets;

		if ( targets is null )
			return;

		// Gather the properties before mutation so edit/undo tracking
		// starts before PresetService.Apply changes anything.
		var affectedNames = targets.Where( x => x is not null )
			.SelectMany( target => PresetService.GetAffectedProperties( preset, target ) )
			.Distinct( StringComparer.OrdinalIgnoreCase ).ToArray();

		var affectedProperties = affectedNames.Select( parent.GetProperty ).Where( x => x is not null ).ToArray();

		foreach ( var property in affectedProperties )
			parent.NoteStartEdit( property );

		foreach ( var target in targets )
		{
			if ( target is null )
				continue;

			PresetService.Apply( preset, target );
		}

		foreach ( var property in affectedProperties )
			parent.NoteFinishEdit( property );
	}

	private void OpenAuthoringMenu()
	{
		var menu = new ContextMenu();

		var selected = _property.GetValue<GenericPresetResource>();

		var target = GetCaptureTarget( _property );
		var presetKey = ResolvePresetKey();

		var targetInfo = target is null || string.IsNullOrWhiteSpace( presetKey ) ? null : PresetRegistry.Find( presetKey, target.GetType() );

		if ( targetInfo is not null )
		{
			var modes = targetInfo.Modes.OrderBy( x => x.Key ).ToArray();

			if ( modes.Length <= 1 )
			{
				var mode = modes.FirstOrDefault();

				menu.AddOption( "New From Current", "add", () =>
				{
					if ( mode is not null )
						CreatePresetFromTarget( targetInfo, mode );
				}
				).Enabled = mode is not null;
			}
			else
			{
				var create = menu.AddMenu( "New From Current", "add" );

				foreach ( var mode in modes )
				{
					var capturedMode = mode;

					create.AddOption( mode.Key.ToTitleCase(), targetInfo.Icon, () => CreatePresetFromTarget( targetInfo, capturedMode ) );
				}
			}
		}
		else
		{
			menu.AddOption( "New From Current", "add", () => { } ).Enabled = false;
		}

		menu.AddOption( "Update Selected From Current", "save", UpdatePresetFromTarget ).Enabled = selected is not null;

		menu.AddSeparator();

		menu.AddOption( "Open Selected Preset", "open_in_new", OpenSelectedPreset ).Enabled = selected is not null;

		menu.OpenAtCursor();
	}

	private void CreatePresetFromTarget( PresetTargetInfo targetInfo, PresetModeInfo mode )
	{
		if ( GetCaptureTarget( _property ) is not { } target )
			return;

		var presetFolder = Path.Combine( Project.Current.GetAssetsPath(), "presets" );

		Directory.CreateDirectory( presetFolder );

		var suffix = mode.Key == PresetModes.Default ? targetInfo.Key : $"{targetInfo.Key}_{mode.Key}";

		var filename = EditorUtility.SaveFileDialog( $"Create {targetInfo.Name}", "preset",
			Path.Combine( presetFolder, $"new_{MakeSafeFilename( suffix )}.preset" ) );

		if ( string.IsNullOrWhiteSpace( filename ) )
			return;

		var captured = PresetService.Capture( targetInfo.Key, target, Path.GetFileNameWithoutExtension( filename ), mode.Key );

		if ( captured is null )
		{
			Log.Warning( $"Couldn't capture '{targetInfo.Name}' " + $"mode '{mode.Key}' from '{target.GetType().Name}'." );

			return;
		}

		var asset = PresetService.SaveAs( captured, filename );

		if ( asset is null )
			return;

		var loaded = PresetService.Load( asset );

		if ( loaded?.Resource is null )
			return;

		// Doing browser work BEFORE we're assigning the serialized
		// preset reference. SetPreset may cause this inspector widget
		// to be rebuilt/destroyed, which isn't ideal.
		MainAssetBrowser.Instance?.Local.OnAssetCreated( asset, filename );

		MainAssetBrowser.Instance?.Local.UpdateAssetList();

		MainAssetBrowser.Instance?.Local.FocusOnAsset( asset );

		// This MUST be the final action in this method!
		// We must not touch this widget afterwards.
		SetPreset( _property, loaded.Resource );
	}

	private void UpdatePresetFromTarget()
	{
		var selected = _property.GetValue<GenericPresetResource>();

		if ( selected is null )
			return;

		if ( GetCaptureTarget( _property ) is not { } target )
			return;

		var captured = PresetService.Capture( selected.PresetType, target, selected.PresetName, selected.PresetMode );

		if ( captured?.Resource is null )
			return;

		// Update only the captured payload.
		// Existing metadata remains in charge.
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

		MainAssetBrowser.Instance?.Local.UpdateAssetList();

		// No RebuildPresets() on purpose, the identity of 
		// the selected preset shouldn't change here.
	}

	private void OpenSelectedPreset()
	{
		var resource = _property.GetValue<GenericPresetResource>();

		if ( resource is null )
			return;

		var asset = FindPresetAsset( resource );

		if ( asset is null )
			return;

		IAssetEditor.OpenInEditor( asset, out _ );

		MainAssetBrowser.Instance?.Local.FocusOnAsset( asset );
	}

	private string ResolvePresetKey()
	{
		PresetRegistry.Invalidate();

		if ( _property.TryGetAttribute<PresetSelectorAttribute>( out var attribute ) )
		{
			if ( !string.IsNullOrWhiteSpace( attribute.PresetKey ) )
				return attribute.PresetKey;
		}

		var target = GetCaptureTarget( _property );

		if ( target is null )
			return null;

		return PresetRegistry.FindForTarget( target.GetType() ).FirstOrDefault()?.Key;
	}

	private static object GetCaptureTarget( SerializedProperty property )
	{
		return property.Parent?.Targets?.FirstOrDefault();
	}

	private static Asset FindPresetAsset( GenericPresetResource resource )
	{
		if ( resource is null )
			return null;

		if ( !string.IsNullOrWhiteSpace( resource.SourceAssetPath ) )
		{
			var asset = AssetSystem.FindByPath( resource.SourceAssetPath );

			if ( asset is not null ) return asset;
		}

		if ( !string.IsNullOrWhiteSpace( resource.ResourcePath ) )
			return AssetSystem.FindByPath( resource.ResourcePath );

		return null;
	}

	private static bool IsCurrentPreset( GenericPresetResource current, GenericPresetResource candidate )
	{
		if ( current is null || candidate is null )
			return false;

		if ( ReferenceEquals( current, candidate ) )
			return true;

		var currentPath = current.SourceAssetPath ?? current.ResourcePath;

		var candidatePath = candidate.SourceAssetPath ?? candidate.ResourcePath;

		return !string.IsNullOrWhiteSpace( currentPath ) && string.Equals( currentPath, candidatePath, StringComparison.OrdinalIgnoreCase );
	}

	private static string GetPresetLabel( PresetAsset preset )
	{
		var mode = PresetModes.Normalize( preset.Resource?.PresetMode );

		if ( mode == PresetModes.Default )
			return preset.Name;

		return $"{preset.Name} ({mode.ToTitleCase()})";
	}

	private static string ResolvePresetIcon( string key, object target )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return "tune";

		var info = target is null ? PresetRegistry.Find( key ) : PresetRegistry.Find( key, target.GetType() );

		return info?.Icon ?? "tune";
	}

	private static string MakeSafeFilename( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return "preset";

		var invalid = Path.GetInvalidFileNameChars();

		return new string( value.Trim().ToLowerInvariant().Select( c => invalid.Contains( c ) || char.IsWhiteSpace( c ) ? '_' : c ).ToArray() );
	}
}
