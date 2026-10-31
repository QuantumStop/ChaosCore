namespace Core.Editor;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Nodes;

public sealed partial class ResourceEditor
{
	[Shortcut( "editor.new", "CTRL+N", ShortcutType.Window )]
	public void New() => OpenAddWindow( _types, true );

	private void OpenAddWindow( IEnumerable<ResourceEditorType> types, bool allowNewType = false )
	{
		if ( !CaptureCurrent() )
			return;

		if ( _addWindow?.IsValid == true )
			_addWindow.Close();

		_addWindow = new ResourceAddWindow( this, types.ToArray(), allowNewType );
		_addWindow.Show();
	}

	private void ShowCreateMenu( IEnumerable<ResourceEditorType> types )
	{
		var menu = new Menu( this );

		foreach ( var type in types )
		{
			menu.AddOption( "Add " + type.Name, type.Icon, () => CreateResource( type, type.Create() ) );
			var edit = ManagedEdit( type.ResourceType );
			if ( edit is not null )
				menu.AddOption( "Edit resource type...", "edit", () => EditResourceType( edit ) );
		}
		
		menu.OpenAtCursor();
	}

	public void SaveAs()
	{
		if ( _resource is null || !ApplyJson() )
			return;

		CreateResource( _type, _resource );
	}

	/// <summary>
	/// Asks for a resource path and stages the new instance without writing it.
	/// </summary>
	private void CreateResource( ResourceEditorType type, GameResource resource )
	{
		if ( !CaptureCurrent() )
			return;

		var filename = EditorUtility.SaveFileDialog( $"Stage new {type.Name} - file created only on Save", type.Extension, Path.Combine( GetCreationDirectory( type.Extension ), $"new_resource.{type.Extension}" ) );

		if ( string.IsNullOrWhiteSpace( filename ) )
			return;

		// Creation/Duplication doesn't overwrite another resource.
		filename = Path.GetFullPath( filename );

		var root = Path.GetFullPath( Project.Current.GetAssetsPath() ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ) + Path.DirectorySeparatorChar;

		if ( !filename.StartsWith( root, StringComparison.OrdinalIgnoreCase ) || !string.Equals( Path.GetExtension( filename ), "." + type.Extension, StringComparison.OrdinalIgnoreCase ) )
		{
			ShowError( $"Choose a .{type.Extension} file inside this project's Assets folder." );
			return;
		}

		if ( File.Exists( filename ) || _entries.Any( x => string.Equals( x.Path, filename, StringComparison.OrdinalIgnoreCase ) ) )
		{
			Dialog.AskConfirm( () => CreateResource( type, resource ), 
			"Choose an unused filename for the new resource.", 
			"Resource already exists", 
			"Choose another", "Cancel" );
			return;
		}

		StageResource( type, resource, filename );
	}

	/// <summary>
	/// Adds an unsaved resource to the outliner and opens it for editing.
	/// </summary>
	private void StageResource( ResourceEditorType type, GameResource resource, string filename )
	{
		if ( !ResourceAccess.Allows( resource.GetType(), filename ) )
		{
			ShowError( "This resource is excluded by a Resource Editor project policy." );
			return;
		}

		var copy = type.Create();
		copy.Deserialize( resource.Serialize() );

		var entry = new Entry
		{
			Path = filename,
			Type = type,
			Resource = copy,
			Dirty = true
		};

		_entries.Add( entry );
		RememberCreationDirectory( type.Extension, filename );
		Reload();
		OpenEntry( entry );
	}

	private void Revert()
	{
		if ( !HasPending )
			return;

		Dialog.AskConfirm( DiscardAll, "Discard ALL pending edits, additions, duplicates and removals? No files will be changed.", 
		"Discard pending changes", "Discard all", "Cancel" );
	}

	/// <summary>
	/// Restores the selected resource or drops it's unsaved draft and pending schema additions.
	/// </summary>
	private void DiscardSelected()
	{
		if ( _activeSchemaDraft is not null )
		{
			var draft = _activeSchemaDraft;
			var resource = _schemaResource;
			Dialog.AskConfirm( () =>
			{
				_schemaEdits.Remove( draft );
				ClearSelection();
				if ( resource is not null )
					OpenEntry( resource );
				UpdateTitle();
			}, "Discard this resource type's pending schema?", "Discard type changes", "Discard", "Cancel" );

			return;
		}

		var entry = _current;

		if ( entry is null )
			return;

		Dialog.AskConfirm( () => DiscardEntry( entry ), $"Discard pending changes to '{entry.Name}'? Other resources keep their pending changes.", 
		"Discard resource changes", "Discard", "Cancel" );
	}

	private void DiscardEntry( Entry entry )
	{
		_schemaEdits.RemoveAll( x => x.Schema.FullName == entry.Resource?.GetType().FullName );

		var asset = entry.Asset;

		if ( _current == entry )
			ClearSelection();

		_entries.Remove( entry );
		Reload();

		if ( asset is not null )
			AssetOpen( asset );

		UpdateTitle();
	}

	/// <summary>
	/// Discards pending resource edits, removals and class additions.
	/// </summary>
	private void DiscardAll()
	{
		_schemaEdits.Clear();
		ClearSelection();
		_entries.Clear();
		_jsonError.Hide();
		Reload();
		UpdateTitle();
	}

	/// <summary>
	/// Stages or cancels removal. The asset is only removed when changes are saved.
	/// </summary>
	private void ToggleRemoval( Entry entry )
	{
		if ( !AllowsEntry( entry ) )
		{
			ShowError( "This resource is excluded by a Resource Editor project policy." );
			return;
		}

		// Removal is reversible until we Save, but new drafts are also retained for us to undo.
		if ( !entry.Removed && entry.Asset is not null && entry.Original is null )
		{
			try
			{
				entry.Original = File.ReadAllText( entry.Path );
			}
			catch ( Exception e )
			{
				ShowError( e.Message );
				return;
			}
		}

		entry.Removed = !entry.Removed;
		UpdateTitle();
		Reload();
	}

	private static string CreationDirectoryKey( string extension ) => "ResourceEditor." + Project.Current.ConfigFilePath + ".LastDirectory." + extension;

	/// <summary>
	/// Chooses a recent directory for this resource type, falling back to the project Assets folder.
	/// </summary>
	private string GetCreationDirectory( string extension )
	{
		var project = Path.GetDirectoryName( Project.Current.ConfigFilePath );
		var assets = Path.GetFullPath( Project.Current.GetAssetsPath() ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
		var remembered = EditorCookie.GetString( CreationDirectoryKey( extension ), "" );
		var candidates = new[]
		{
			_current?.Type.Extension == extension ? Path.GetDirectoryName( _current.Path ) : null,
			string.IsNullOrWhiteSpace( remembered ) ? null : Path.Combine( project, remembered ),
			_entries.Where( x => x.Type.Extension == extension && x.Asset is not null ).Select( x => Path.GetDirectoryName( x.Path ) ).FirstOrDefault(),
			assets
		};

		foreach ( var candidate in candidates )
		{
			if ( string.IsNullOrWhiteSpace( candidate ) )
				continue;

			var path = Path.GetFullPath( candidate );
			if ( Directory.Exists( path ) && (path.Equals( assets, StringComparison.OrdinalIgnoreCase ) || path.StartsWith( assets + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase )) )
				return path;
		}

		return assets;
	}

	private static void RememberCreationDirectory( string extension, string filename )
	{
		var project = Path.GetDirectoryName( Project.Current.ConfigFilePath );
		EditorCookie.SetString( CreationDirectoryKey( extension ), Path.GetRelativePath( project, Path.GetDirectoryName( filename ) ) );
	}

	/// <summary>
	/// Runs an action after pending changes are saved or discarded through the close dialog.
	/// </summary>
	private void WithSavedChanges( Action action )
	{
		if ( !HasPending )
		{
			action();

			return;
		}

		if ( _closeDialog?.IsValid == true )
		{
			_closeDialog.Show();
			_closeDialog.Window.Raise();
			_closeDialog.Window.Focus();
			return;
		}

		_closeDialog = new PendingChangesDialog( this, action );
		_closeDialog.Show();
	}

	/// <summary>
	/// Saves all pending resource edits and class additions.
	/// </summary>
	[Shortcut( "editor.save", "CTRL+S", ShortcutType.Window )]
	public void Save() => SavePending();

	private void SavePending( Action after = null, Entry selected = null )
	{
		if ( _applyingSchemas )
			return;

		if ( !CaptureCurrent() )
			return;

		if ( _schemaEdits.Any( x => x.Dirty && (selected is null || x.Schema.FullName == selected.Resource?.GetType().FullName) ) )
		{
			SaveSchemasThenPending( after, selected );

			return;
		}

		var removed = _entries.Where( x => x.Removed && x.Asset is not null && (selected is null || x == selected) ).ToArray();

		if ( removed.Length > 0 )
		{
			Dialog.AskConfirm( () =>
			{
				if ( SaveEntries( selected ) )
					after?.Invoke();
			}, "Save pending edits and send these resources to the Recycle Bin?\n" + string.Join( "\n", removed.Select( x => x.Path ) ),
			"Confirm resource removal", "Apply and remove", "Cancel" );

			return;
		}

		if ( SaveEntries( selected ) )
			after?.Invoke();
	}

	/// <summary>
	/// Validates the selected batch before writing resource data or applying staged removals.
	/// </summary>
	private bool SaveEntries( Entry selected )
	{
		if ( !CaptureCurrent() )
			return false;

		var pending = _entries.Where( x => (selected is null || x == selected) && (x.Dirty || x.Removed || x.Asset is null) ).ToArray();

		try
		{
			// Validate the entire batch before writing anything.
			foreach ( var entry in pending )
			{
				if ( !AllowsEntry( entry ) )
					throw new InvalidOperationException( $"'{entry.Name}' is excluded by a Resource Editor project policy. Discard its pending changes." );

				if ( !entry.Removed && entry.Asset is null && File.Exists( entry.Path ) )
					throw new IOException( $"A file already exists at '{entry.Path}' with the same name. Choose another name." );

				if ( entry.Asset is not null && entry.Original is not null && (!File.Exists( entry.Path ) || File.ReadAllText( entry.Path ) != entry.Original) )
					throw new IOException( $"'{entry.Name}' changed outside Resource Editor. Discard pending changes and reopen before saving." );

				if ( entry.Removed && entry.Asset is not null && entry.Asset.GetDependants( false ).Any() )
					throw new InvalidOperationException( $"Cannot remove '{entry.Name}': other assets reference it. Remove and save those references first." );

				if ( entry.Removed && entry.Asset is not null && _entries.Any( x => !x.Removed && x.Resource is not null
				&& ReferencesPath( x.Resource.Serialize(), entry.Asset.RelativePath ) ) )
					throw new InvalidOperationException( $"Cannot remove '{entry.Name}': a resource in this editor still references it." );
			}

			foreach ( var entry in pending.Where( x => !x.Removed ) )
			{
				entry.Asset ??= AssetSystem.CreateResource( entry.Type.Extension, entry.Path );

				if ( entry.Asset is null || !entry.Asset.SaveToDisk( entry.Resource ) )
					throw new IOException( $"Failed to save '{entry.Path}'." );

				entry.Original = File.ReadAllText( entry.Path );
				entry.SavedValues = entry.Resource.Serialize();
				entry.Dirty = false;
				entry.Asset.Compile( false );
			}

			foreach ( var entry in pending.Where( x => x.Removed ) )
			{
				entry.Asset?.Delete();

				if ( entry.Asset is not null && File.Exists( entry.Path ) )
					throw new IOException( $"Removal of '{entry.Path}' did not complete." );

				_entries.Remove( entry );

				if ( _current == entry )
					ClearSelection();
			}
		}
		catch ( Exception e )
		{
			ShowError( e.Message + " Pending changes remain available. Review before retrying Save." );

			return false;
		}

		CurrentAsset = _current?.Asset;

		if ( selected is null || selected == _current )
			_dirty = false;

		_jsonError.Hide();
		UpdateTitle();
		Reload();

		return true;
	}

	private static bool ReferencesPath( JsonNode node, string path )
	{
		if ( node is JsonObject obj )
			return obj.Any( x => ReferencesPath( x.Value, path ) );

		if ( node is JsonArray array )
			return array.Any( x => ReferencesPath( x, path ) );

		return node is JsonValue value && value.TryGetValue<string>( out var text ) && string.Equals( text, path, StringComparison.OrdinalIgnoreCase );
	}
}
