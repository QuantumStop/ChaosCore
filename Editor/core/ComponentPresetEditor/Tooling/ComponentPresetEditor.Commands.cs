namespace Core.Editor;

using System;
using System.IO;
using System.Linq;

public partial class ComponentPresetEditor
{
	[Shortcut( "editor.new", "CTRL+N", ShortcutType.Window )]
	public void New()
	{
		PresetRegistry.Invalidate();

		var menu = new Menu( this );
		var targets = PresetRegistry.All
			.OrderBy( x => x.Category )
			.ThenBy( x => x.Name )
			.ToArray();

		if ( targets.Length == 0 )
		{
			Log.Warning( "No preset targets are registered." );
			return;
		}

		foreach ( var target in targets )
		{
			var modes = target.Modes
				.OrderBy( x => x.Key )
				.ToArray();

			if ( modes.Length <= 1 )
			{
				var mode = modes.FirstOrDefault();

				if ( mode is null )
					continue;

				var capturedTarget = target;
				var capturedMode = mode;

				menu.AddOption(
					target.Name,
					target.Icon,
					() => CreateNewPreset( capturedTarget, capturedMode )
				);

				continue;
			}

			var targetMenu = menu.AddMenu( target.Name, target.Icon );

			foreach ( var mode in modes )
			{
				var capturedTarget = target;
				var capturedMode = mode;

				targetMenu.AddOption(
					mode.Key.ToTitleCase(),
					target.Icon,
					() => CreateNewPreset( capturedTarget, capturedMode )
				);
			}
		}

		menu.OpenAtCursor();
	}

	[Shortcut( "editor.open", "CTRL+O", ShortcutType.Window )]
	public void Open()
	{
		var filename = EditorUtility.OpenFileDialog( "Open Component Preset", "preset", Project.Current.GetAssetsPath() );

		if ( string.IsNullOrWhiteSpace( filename ) )
			return;

		var asset = AssetSystem.FindByPath( filename ) ?? AssetSystem.RegisterFile( filename );

		if ( asset is null )
		{
			Log.Warning( $"Unable to open preset '{filename}'" );
			return;
		}

		OpenAsset( asset );
	}

	[Shortcut( "editor.save", "CTRL+S", ShortcutType.Window )]
	public void Save()
	{
		if ( _currentPreset?.Asset is null || _currentPreset.Resource is null )
			return;

		if ( !PresetService.Save( _currentPreset ) )
		{
			Log.Warning( $"Failed to save preset '{_currentPreset.Asset.Path}'" );
			return;
		}

		_presetView?.Reload();
		UpdateWindowTitle();
	}

	[Shortcut( "editor.save-as", "CTRL+SHIFT+S", ShortcutType.Window )]
	public void SaveAs()
	{
		if ( _currentPreset is null )
			return;

		var presetDirectory = Path.Combine( Project.Current.GetAssetsPath(), "presets" );
		Directory.CreateDirectory( presetDirectory );

		var suggestedName = MakeSafeFilename( _currentPreset.Name );
		var filename = EditorUtility.SaveFileDialog( "Save Preset As", "preset", Path.Combine( presetDirectory, $"{suggestedName}.preset" ) );

		if ( string.IsNullOrWhiteSpace( filename ) )
			return;

		var asset = PresetService.SaveAs( _currentPreset, filename );

		if ( asset is null )
		{
			Log.Warning( $"Failed to save preset as '{filename}'" );
			return;
		}

		_presetView?.Reload();
		OpenAsset( asset );
	}

	private void CreateNewPreset( PresetTargetInfo target, PresetModeInfo mode )
	{
		if ( target is null || mode is null )
			return;

		var temporary = PresetService.Create( target, mode.Key, $"New {target.Name}" );

		if ( temporary is null )
		{
			Log.Warning( $"Unable to create preset '{target.Key}:{mode.Key}'" );
			return;
		}

		var defaultFilename = MakeSafeFilename(
			mode.Key == PresetModes.Default
				? target.Key
				: $"{target.Key}_{mode.Key}"
		);

		var presetDirectory = Path.Combine( Project.Current.GetAssetsPath(), "presets" );
		Directory.CreateDirectory( presetDirectory );

		var defaultPath = Path.Combine( presetDirectory, $"{defaultFilename}.preset" );
		var filename = EditorUtility.SaveFileDialog(
			$"Create {target.Name} Preset",
			"preset",
			defaultPath
		);

		if ( string.IsNullOrWhiteSpace( filename ) )
			return;

		var asset = PresetService.SaveAs( temporary, filename );

		if ( asset is null )
		{
			Log.Warning( $"Unable to create preset '{filename}'" );
			return;
		}

		_presetView?.Reload();
		OpenAsset( asset );
	}

	private static string MakeSafeFilename( string value )
	{
		if ( string.IsNullOrWhiteSpace( value ) )
			return "new_preset";

		var invalid = Path.GetInvalidFileNameChars();
		var chars = value
			.Trim()
			.ToLowerInvariant()
			.Select( c => invalid.Contains( c ) ? '_' : c )
			.ToArray();

		return new string( chars ).Replace( ' ', '_' );
	}
}
