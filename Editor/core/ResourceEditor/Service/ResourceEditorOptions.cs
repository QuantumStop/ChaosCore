namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Resourc Editor options, configured per project. 
/// Export paths are relative to /Code directory.
/// </summary>
public sealed class ResourceEditorOptions
{
	[Property, Title( "Class export folder" ), Description( "Relative to the project's Code directory." )]
	public string ClassExportFolder { get; set; } = "Generated/Resources";

	/// <summary>
	/// Project defined groups used by the outliner and resource picker.
	/// </summary>
	public List<ResourceGroupOptions> Groups { get; set; } = [];

	private static string SettingsPath => Path.Combine( Path.GetDirectoryName( Project.Current.ConfigFilePath ), "ProjectSettings", "ResourceEditor.config" );

	/// <summary>
	/// Loads project options, using defaults when the settings file is absent or unreadable.
	/// </summary>
	public static ResourceEditorOptions Load()
	{
		try
		{
			if ( !File.Exists( SettingsPath ) )
				return new();

			return JsonSerializer.Deserialize<ResourceEditorOptions>( File.ReadAllText( SettingsPath ) ) ?? new();
		}
		catch ( Exception e )
		{
			Log.Warning( $"Resource Editor options could not be loaded: {e.Message}" );
			return new();
		}
	}

	/// <summary>
	/// Resolves the class export folder and rejects paths outside the project's Code directory.
	/// </summary>
	public string GetExportDirectory()
	{
		var code = Path.GetFullPath( Project.Current.GetCodePath() ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
		var relative = ClassExportFolder?.Trim();
		
		if ( string.IsNullOrWhiteSpace( relative ) || Path.IsPathRooted( relative ) )
			throw new InvalidOperationException( "Choose an export folder relative to Code." );

		var path = Path.GetFullPath( Path.Combine( code, relative ) );
		
		if ( !path.Equals( code, StringComparison.OrdinalIgnoreCase ) && !path.StartsWith( code + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase ) )
			throw new InvalidOperationException( "The class export folder must stay within Code." );

		return path;
	}

	/// <summary>
	/// Validates and writes project options. 
	/// Resource data is saved separately.
	/// </summary>
	public void Save()
	{
		GetExportDirectory();
		
		if ( Groups.Any( x => string.IsNullOrWhiteSpace( x.Name ) ) )
			throw new InvalidOperationException( "Give every group a name." );

		Directory.CreateDirectory( Path.GetDirectoryName( SettingsPath ) );
		File.WriteAllText( SettingsPath, JsonSerializer.Serialize( this, new JsonSerializerOptions { WriteIndented = true } ) );
	}
}

/// <summary>
/// A group configuration, authored in Resource Editor.
/// </summary>
public sealed class ResourceGroupOptions
{
	/// <summary>
	/// Stable group identity used to retain tree state when the group is renamed.
	/// </summary>
	public string Key { get; set; } = "project." + Guid.NewGuid().ToString( "N" );

	[Property] public string Name { get; set; } = "New group";

	[Property, IconName] public string Icon { get; set; } = "folder";

	/// <summary>
	/// Full resource type names assigned to this group. Types may belong to other groups too.
	/// </summary>
	public List<string> ResourceTypes { get; set; } = [];
}
