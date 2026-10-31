namespace Core.Editor;

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

public sealed partial class ResourceEditor
{
	private JsonObject _inspectedValues;
	private static readonly Action<Resource, string> _setPreviewPath = typeof( Resource )
		.GetProperty( nameof( Resource.ResourcePath ) ).GetSetMethod( true ).CreateDelegate<Action<Resource, string>>();

	private bool _jsonMode
	{
		get => (_inspectorView == ResourceInspectorViewType.Code ? _previousInspectorView : _inspectorView) == ResourceInspectorViewType.JSON;
		set
		{
			var view = value ? ResourceInspectorViewType.JSON : ResourceInspectorViewType.Form;
			if ( _inspectorView == ResourceInspectorViewType.Code )
				_previousInspectorView = view;
			else
				_inspectorView = view;
		}
	}

	[Property, EnumOptions( ResourceInspectorViewType.Code, Exclude = true )]
	public ResourceInspectorViewType InspectorView
	{
		get => _inspectorView;
		set
		{
			if ( value == ResourceInspectorViewType.Code )
			{
				if ( _inspectorView != ResourceInspectorViewType.Code )
					ShowGeneratedSource();
			}
			else
				SetJsonMode( value == ResourceInspectorViewType.JSON );
		}
	}

	/// <summary>
	/// Validates JSON edits and retains the current resource in it's pending entry.
	/// </summary>
	private bool CaptureCurrent()
	{
		if ( !ApplyJson() )
			return false;

		if ( _current is not null && _resource is not null )
		{
			_current.Resource = _resource;
			_current.Dirty = _dirty;
		}

		return true;
	}

	private void OpenEntry( Entry entry )
	{
		if ( !AllowsEntry( entry ) )
		{
			ShowError( "This resource is excluded by a Resource Editor project policy." );

			return;
		}

		if ( entry == _current && _resource is not null )
			return;

		if ( !CaptureCurrent() )
			return;

		try
		{
			// We're editing a private copy, so unsaved changes do not change the existing resource!
			var type = entry.Type;

			if ( entry.Resource is null )
			{
				var loaded = entry.Asset.LoadResource<GameResource>();

				if ( loaded is null || !loaded.IsValid || loaded.IsError )
					throw new InvalidOperationException( "Resource could not be loaded." );

				entry.Original = File.ReadAllText( entry.Path );
				var editable = type.Create();
				editable.Deserialize( loaded.Serialize() );
				entry.SavedValues = editable.Serialize();
				entry.Resource = editable;
			}

			Disconnect();
			_activeSchemaDraft = null;
			_editingType = false;
			_schemaResource = null;
			_inspectorView = _jsonMode ? ResourceInspectorViewType.JSON : ResourceInspectorViewType.Form;
			_current = entry;
			CurrentAsset = entry.Asset;
			_type = type;
			_resource = entry.Resource;
			_dirty = entry.Dirty;
			BuildForm();
			_jsonChanged = false;
			_jsonError.Hide();
			RefreshMode();
			UpdateTitle();

			if ( _tree?.IsValid == true )
				_tree.Update();
		}
		catch ( Exception e )
		{
			ClearSelection();
			_current = entry;
			CurrentAsset = entry.Asset;
			_type = entry.Type;
			UpdateTitle();
			ShowError( $"Unable to open '{entry.Path}': {e.Message}" );
		}
	}

	private void Changed( SerializedProperty property )
	{
		if ( _resource is null || _current is null )
			return;

		var values = _resource.Serialize();
		ResourceValues.AcceptEmptyCollections( _current.SavedValues, _inspectedValues, values );
		var sameValues = JsonNode.DeepEquals( _inspectedValues, values );
		if ( sameValues && !_dirty && !_current.Dirty )
			return;

		var dirty = HasValueChanges( values );
		if ( sameValues && _dirty == dirty && _current.Dirty == dirty )
			return;

		_inspectedValues = values;
		_dirty = dirty;
		_current.Dirty = _dirty;

		RefreshLinkedTrees();
		UpdateTitle();
	}

	/// <summary>
	/// Compares editable values with the saved resource. 
	/// New drafts remain pending until saved.
	/// </summary>
	private bool HasValueChanges( JsonNode values )
	{
		_current.SavedValues ??= _current.Asset?.LoadResource<GameResource>()?.Serialize();
		return _current.Asset is null || !ResourceValues.Equal( _current.SavedValues, values );
	}

	private void JsonChanged()
	{
		if ( !_jsonMode || _resource is null )
			return;

		var firstChange = !_jsonChanged;
		var wasDirty = _dirty;
	
		_jsonChanged = true;

		try
		{
			var values = JsonNode.Parse( _json.PlainText );
			_dirty = HasValueChanges( values );
		}
		catch ( JsonException )
		{
			_dirty = true;
		}

		_current.Dirty = _dirty;
		
		if ( firstChange || wasDirty != _dirty )
			RefreshLinkedTrees();
		
		UpdateTitle();
	}

	private void UpdateTitle()
	{
		WindowTitle = $"{_activeSchemaDraft?.Schema.DisplayName ?? _current?.Name ?? "Resources"}{(HasPending ? " *" : "")} - Resource Editor";
		_heading.Text = _activeSchemaDraft?.Schema.DisplayName ?? _current?.Name ?? "Select a resource";
		_heading.ToolTip = _current?.Path;
		_metadata.RefreshData();

		var schemaPending = _schemaEdits.Any( x => x.Dirty && (_activeSchemaDraft == x || x.Schema.FullName == _resource?.GetType().FullName) );

		var pendingSelected = schemaPending || (_resource is not null && _current is not null && (_dirty || _current.Dirty || _current.Removed || _current.Asset is null));
		_applyButton.Text = !_editingType && _activeSchemaDraft?.Original is not null && !_activeSchemaDraft.Dirty ? "Create resource..." : "Apply changes";
		_applyButton.Enabled = !_applyingSchemas && ((_activeSchemaDraft is not null && (!_editingType || _activeSchemaDraft.Dirty)) || (pendingSelected && AllowsEntry( _current )));
		_discardButton.Enabled = !_applyingSchemas && (pendingSelected || _activeSchemaDraft is not null);
		_viewControl.Enabled = _activeSchemaDraft is null && _resource is not null;
		_sourceButton.Visible = _activeSchemaDraft is not null || ResourceCodegen.GetSourcePath( _resource?.GetType() ) is not null;
	
		if ( _editTypeButton?.IsValid == true )
			_editTypeButton.Visible = _resource is not null && _schemaEdits.Any( edit => edit.Schema.FullName == _resource.GetType().FullName );
	
		_thumbnail.Update();

		if ( _tree?.IsValid == true )
			_tree.Update();

		var added = _entries.Count( x => x.Asset is null && !x.Removed );

		var removed = _entries.Count( x => x.Removed );

		var modified = _entries.Count( x => x.Asset is not null && !x.Removed && (x.Dirty || (x == _current && _dirty)) );

		var schemas = _schemaEdits.Count( x => x.Dirty );

		if ( _pendingLabel is not null )
			_pendingLabel.Text = HasPending ? $"Pending: {added} new, {modified} edited, {removed} removed" + (schemas > 0 ? $", {schemas} type schemas" : "") : "No pending changes";
	}

	private void ClearSelection()
	{
		Disconnect();
		_activeSchemaDraft = null;
		_editingType = false;
		_schemaResource = null;
		_inspectorView = _jsonMode ? ResourceInspectorViewType.JSON : ResourceInspectorViewType.Form;
		_current = null;
		CurrentAsset = null;
		_resource = null;
		_inspectedValues = null;
		_dirty = false;
		_jsonChanged = false;
		_properties.Canvas = null;
		_featureHeader.Layout.Clear( true );
		_jsonError.Hide();
		_jsonMode = false;
		_json.PlainText = "";
		RefreshMode();
	}

	/// <summary>
	/// Builds native property controls and, for managed classes, pending property additions.
	/// </summary>
	private void BuildForm()
	{
		Disconnect();
		if ( _resource.ResourcePath is null )
			_setPreviewPath( _resource, _current.Asset?.RelativePath ?? $"resource-editor/{Guid.NewGuid():N}.{_type.Extension}" );

		_inspectedValues = _resource.Serialize();
		_serialized = _resource.GetSerialized();
		_serialized.OnPropertyChanged += Changed;

		var canvas = new Widget( _properties )
		{
			Layout = Layout.Column()
		};
		canvas.Layout.Margin = 12;
		canvas.SetSizeMode( SizeMode.Flexible, SizeMode.CanShrink );

		var sheet = new ControlSheet
		{
			IncludePropertyNames = true
		};
		sheet.AddObject( _serialized );
		canvas.Layout.Add( sheet );

		var managed = ManagedEdit( _resource.GetType() );

		if ( managed is not null )
			canvas.Layout.Add( new SchemaEditor( this, managed, canvas ) );

		_properties.Canvas = canvas;
		BuildFeatureHeader( canvas );
	}

	/// <summary>
	/// Keeps native feature tabs above the scrolling properties, retaining their selection and add controls.
	/// </summary>
	private void BuildFeatureHeader( Widget canvas )
	{
		_featureHeader.Layout.Clear( true );
		var features = canvas.Children.OfType<FeatureTabWidget>().FirstOrDefault();
		if ( features is null )
			return;

		var row = new InspectorSection( _featureHeader );
		row.Layout.Margin = new Sandbox.UI.Margin( 12, 4 );

		foreach ( var tab in features.Children.OfType<FeatureTabOption>().ToArray() )
		{	
			row.Layout.Add( tab );
		}

		foreach ( var add in features.Children.OfType<IconButton>().ToArray() )
		{
			row.Layout.Add( add );
		}

		row.Layout.AddStretchCell();
		_featureHeader.Layout.Add( row );
		_featureHeader.Layout.AddSeparator( 1, Theme.WindowBackground );
		features.Layout.Margin = 0;
	}

	/// <summary>
	/// Applies valid JSON to the editable instance. 
	/// Invalid text remains visible to change.
	/// </summary>
	private bool ApplyJson()
	{
		if ( !_jsonMode || !_jsonChanged )
			return true;

		if ( !AllowsEntry( _current ) )
		{
			ShowError( "This resource is excluded by a Resource Editor project policy." );
			return false;
		}

		try
		{
			var data = JsonNode.Parse( _json.PlainText ) as JsonObject ?? throw new InvalidOperationException( "The resource must be a JSON object." );

			var editable = _type.Create();
			editable.Deserialize( data );
			_resource = editable;
			_current.Resource = editable;
			_dirty = HasValueChanges( editable.Serialize() );
			_current.Dirty = _dirty;
			BuildForm();
			_jsonChanged = false;
			_jsonError.Hide();
			RefreshLinkedTrees();
			UpdateTitle();

			return true;
		}
		catch ( Exception e )
		{
			_jsonError.Text = $"Invalid resource JSON: {e.Message}";
			_jsonError.Show();

			return false;
		}
	}

	private void SetJsonMode( bool json )
	{
		if ( _current is not null && !AllowsEntry( _current ) )
			return;

		if ( json == _jsonMode && _inspectorView != ResourceInspectorViewType.Code )
			return;

		if ( json != _jsonMode && !ApplyJson() )
			return;

		var changed = json != _jsonMode;
		_inspectorView = _jsonMode ? ResourceInspectorViewType.JSON : ResourceInspectorViewType.Form;
		_jsonMode = json;
		RefreshMode( changed );
	}

	/// <summary>
	/// Shows Form, JSON or source content and updates the inspector controls.
	/// </summary>
	private void RefreshMode( bool refreshJson = true )
	{
		_featureHeader.Visible = _inspectorView == ResourceInspectorViewType.Form && _featureHeader.Children.Any();
		if ( _inspectorView == ResourceInspectorViewType.Code )
		{
			_generatedSource.Html = "<pre style=\"font-family: Consolas; font-size: 10pt; white-space: pre;\">" + System.Net.WebUtility.HtmlEncode( _sourceText.Replace( "\t", "    " ) ) + "</pre>";
			_generatedSource.Show();
			_json.Hide();
			_properties.Hide();
		}
		else if ( _jsonMode )
		{
			if ( refreshJson )
			{
				_jsonMode = false;
				_json.PlainText = _resource?.Serialize().ToJsonString( new JsonSerializerOptions( JsonSerializerOptions.Default ) { WriteIndented = true } ) ?? "";
				_jsonMode = true;
			}

			_generatedSource.Hide();
			_json.Show();
			_properties.Hide();
		}
		else
		{
			_generatedSource.Hide();
			_json.Hide();
			_properties.Show();
		}

		_sourceButton.Background = _inspectorView == ResourceInspectorViewType.Code ? Theme.Blue.WithAlpha( 0.35f ) : Color.Transparent;
		_sourceButton.ToolTip = _inspectorView == ResourceInspectorViewType.Code ? "Return to Form / JSON" : "View resource class source";
		_viewControl.Update();
	}

	private void Disconnect()
	{
		_serialized?.OnPropertyChanged -= Changed;
		_serialized = null;
	}
}
