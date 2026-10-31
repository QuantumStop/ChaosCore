namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public sealed partial class ResourceEditor
{
	/// <summary>
	/// Pending class metadata and properties, with the original source for existing classes.
	/// </summary>
	private sealed class SchemaEdit
	{
		public ResourceSchema Schema;
		public ResourceCodegen.ManagedSchema Original;
		public bool Dirty;
	}

	private readonly List<SchemaEdit> _schemaEdits = [];
	private SchemaEdit _activeSchemaDraft;
	private Entry _schemaResource;
	private bool _editingType;
	private bool _applyingSchemas;

	/// <summary>
	/// Toggles the source view, previewing pending additions without writing any files.
	/// </summary>
	private void ShowGeneratedSource()
	{
		if ( _inspectorView == ResourceInspectorViewType.Code )
		{
			_inspectorView = _previousInspectorView;
			RefreshMode( false );

			return;
		}

		var edit = _activeSchemaDraft ?? _schemaEdits.FirstOrDefault( x => x.Schema.FullName == _resource?.GetType().FullName );

		try
		{
			if ( edit is not null && (edit.Original is null || edit.Dirty) )
				_sourceText = ResourceCodegen.Preview( edit.Schema, edit.Original );
			else
			{
				var path = edit?.Original?.SourcePath ?? ResourceCodegen.GetSourcePath( _resource?.GetType() );
				if ( path is null )
					return;

				_sourceText = File.ReadAllText( path );
			}
			_previousInspectorView = _inspectorView;
			_inspectorView = ResourceInspectorViewType.Code;
			RefreshMode( false );
		}
		catch ( Exception e )
		{
			ShowError( e.Message );
		}
	}

	/// <summary>
	/// Returns pending edits for a managed class or loads it's current source definition.
	/// </summary>
	private SchemaEdit ManagedEdit( Type type )
	{
		var existing = _schemaEdits.FirstOrDefault( x => x.Schema.FullName == type.FullName );

		if ( existing?.Dirty == true )
			return existing;

		var original = ResourceCodegen.LoadManaged( type );

		if ( original is null )
		{
			if ( existing is not null )
				_schemaEdits.Remove( existing );

			return null;
		}

		if ( existing is not null )
			_schemaEdits.Remove( existing );

		var edit = new SchemaEdit
		{
			Original = original,
			Schema = original.Schema with { Properties = original.Schema.Properties.Select( property => property with { } ).ToList() }
		};
		_schemaEdits.Add( edit );

		return edit;
	}

	/// <summary>
	/// Opens the managed class definition while retaining the selected resource's pending values.
	/// </summary>
	private void EditResourceType( SchemaEdit edit )
	{
		if ( !CaptureCurrent() )
			return;
		var resource = _current;
		ClearSelection();
		_schemaResource = resource;
		_editingType = true;
		_activeSchemaDraft = edit;
		ShowSchemaDraft();
	}

	/// <summary>
	/// Opens a new resource class draft in the main inspector.
	/// </summary>
	private void BeginNewType()
	{
		if ( !CaptureCurrent() )
			return;

		var edit = _schemaEdits.FirstOrDefault( x => x.Original is null );

		if ( edit is null )
		{
			var name = new string( [.. (Project.Current.Config.Title ?? Project.Current.Config.Ident).Where( char.IsLetterOrDigit )] );

			var schema = new ResourceSchema
			{
				Namespace = string.IsNullOrEmpty( name ) || !char.IsLetter( name[0] ) ? "Game.Data" : char.ToUpperInvariant( name[0] ) + name[1..] + ".Data"
			};

			var index = 1;

			while ( AssetType.All.Any( x => x.FileExtension == schema.Extension ) )
			{
				index++;
				schema.Extension = "data" + index;
				schema.ClassName = "NewDataResource" + index;
			}

			edit = new SchemaEdit
			{
				Schema = schema,
				Dirty = true
			};
			_schemaEdits.Add( edit );
		}

		ClearSelection();
		_activeSchemaDraft = edit;
		ShowSchemaDraft();
		Show();
		Focus();
	}

	private void ShowSchemaDraft()
	{
		var canvas = new SchemaEditor( this, _activeSchemaDraft, _properties );
		canvas.Layout.Margin = 12;
		_properties.Canvas = canvas;
		RefreshMode();
		UpdateTitle();
	}

	/// <summary>
	/// Saves the selected resource or generates it's new class before creating an instance.
	/// </summary>
	private async void ApplySelected()
	{
		if ( _applyingSchemas )
			return;

		if ( _activeSchemaDraft is not null )
		{
			var edit = _activeSchemaDraft;

			if ( edit.Dirty && !await ApplySchemaEdits( new[] { edit } ) )
				return;
			if ( _editingType )
			{
				var resource = _schemaResource;
				if ( resource is not null )
					OpenEntry( resource );
				else
					ShowSchemaDraft();
				return;
			}

			var type = _types.FirstOrDefault( x => x.ResourceType.FullName == edit.Schema.FullName );

			if ( type is not null )
				CreateResource( type, type.Create() );

			return;
		}

		if ( _current is not null )
			SavePending( selected: _current );
	}

	private async void SaveSchemasThenPending( Action after, Entry selected )
	{
		var edits = _schemaEdits.Where( x => x.Dirty && (selected is null || x.Schema.FullName == selected.Resource?.GetType().FullName) ).ToArray();

		if ( await ApplySchemaEdits( edits ) )
			SavePending( after, selected );
	}

	/// <summary>
	/// Writes pending class changes, waits for compilation and restores staged resource values after hotload.
	/// </summary>
	private async Task<bool> ApplySchemaEdits( IEnumerable<SchemaEdit> edits )
	{
		if ( _applyingSchemas || !CaptureCurrent() )
			return false;

		_applyingSchemas = true;
		UpdateTitle();

		try
		{
			foreach ( var edit in edits.Where( x => x.Dirty ).ToArray() )
			{
				var values = _entries.Where( x => x.Resource?.GetType().FullName == edit.Schema.FullName ).ToDictionary( x => x, x => x.Resource.Serialize() );
				var revision = ResourceCatalog.Revision;
				var waitForHotload = true;

				if ( edit.Original is null )
				{
					var exportDirectory = ResourceEditorOptions.Load().GetExportDirectory();
					Directory.CreateDirectory( exportDirectory );

					var path = EditorUtility.SaveFileDialog( "Save resource type", "cs", Path.Combine( exportDirectory, edit.Schema.ClassName + ".cs" ) );

					if ( string.IsNullOrWhiteSpace( path ) )
						return false;

					ResourceCodegen.WriteNew( edit.Schema, path );
				}
				else
					waitForHotload = ResourceCodegen.UpdateManaged( edit.Original, edit.Schema );

				ResourceEditorType registered = null;

				for ( var i = 0; i < 200 && IsValid; i++ )
				{
					if ( waitForHotload && ResourceCatalog.Revision == revision )
					{
						await Task.Delay( 100 );
						continue;
					}
					registered = _types.FirstOrDefault( x => x.ResourceType.FullName == edit.Schema.FullName
						&& SchemaCompiled( edit, x.ResourceType ) );

					if ( registered is not null )
						break;

					await Task.Delay( 100 );
				}

				if ( registered is null )
					throw new InvalidOperationException( "The resource type has not compiled. Check the editor Console; your schema draft is retained." );

				edit.Original = ResourceCodegen.LoadManaged( registered.ResourceType ) ?? throw new InvalidOperationException( "The managed resource class could not be reloaded." );
				edit.Schema = edit.Original.Schema with
				{
					Properties = edit.Original.Schema.Properties.Select( property => property with { } ).ToList()
				};
				edit.Dirty = false;

				foreach ( var pair in values )
				{
					pair.Key.Type = registered;
					pair.Key.Resource = registered.Create();
					pair.Key.Resource.Deserialize( pair.Value );
					pair.Key.Dirty = pair.Key.Asset is null || !ResourceValues.Equal( pair.Key.SavedValues, pair.Key.Resource.Serialize() );

					if ( _current == pair.Key )
					{
						_resource = pair.Key.Resource;
						_type = registered;
						_dirty = pair.Key.Dirty;
					}
				}

				Reload();

				if ( _activeSchemaDraft is not null )
					ShowSchemaDraft();
				else if ( _resource is not null )
					BuildForm();
			}

			return true;
		}
		catch ( Exception e )
		{
			ShowError( e.Message );

			return false;
		}
		finally
		{
			_applyingSchemas = false;

			if ( IsValid )
				UpdateTitle();
		}
	}

	/// <summary>
	/// Waits for changed metadata, removed members and property types as well as new names.
	/// </summary>
	private static bool SchemaCompiled( SchemaEdit edit, Type type )
	{
		var compiled = ResourceCodegen.LoadManaged( type );
	
		if ( compiled is null || compiled.Schema.DisplayName != edit.Schema.DisplayName || compiled.Schema.Category != edit.Schema.Category
			|| compiled.Schema.Icon != edit.Schema.Icon || compiled.Schema.Properties.Count != edit.Schema.Properties.Count )
			return false;

		var expectedCount = (edit.Original?.PropertyCount ?? 0) + edit.Schema.Properties.Count - (edit.Original?.Schema.Properties.Count ?? 0);
	
		if ( compiled.PropertyCount != expectedCount )
			return false;
	
		foreach ( var property in edit.Schema.Properties )
		{
			var current = compiled.Schema.Properties.FirstOrDefault( p => p.Name == property.Name );
		
			if ( current is null || current.TypeName != property.TypeName || current.List != property.List || current.Nullable != property.Nullable
				|| current.Category != property.Category || current.Description != property.Description )
				return false;
			
			if ( !string.IsNullOrWhiteSpace( property.DefaultJson ) && !ResourceValues.Equal( System.Text.Json.Nodes.JsonNode.Parse( property.DefaultJson ),
				System.Text.Json.Nodes.JsonNode.Parse( current.DefaultJson ) ) )
				return false;
		}

		return true;
	}
}
