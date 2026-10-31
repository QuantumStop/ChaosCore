namespace Core.Editor;

public sealed partial class ResourceEditor
{
	protected override void BuildDefaultLayout()
	{
		// ResetLayout preserves the dock
		var properties = _inspectorPanel?.IsValid == true ? DockManager.FindDockWidget( _inspectorPanel ) : null;

		if ( properties?.IsValid != true )
			properties = DockManager.SetCentralWidget( BuildInspector() );

		DockManager.RegisterDock( new()
		{
			Title = "Outliner",
			Icon = "view_list",
			Area = DockArea.Left,
			CreateAction = BuildOutliner
		} );

		var browser = DockManager.OpenDock( "Outliner", DockArea.Left, properties );
		ApplyDockMinimumWidths();
		DockManager.SetSplitterProportions( browser, 0.28f, 0.72f );

		if ( _resource is not null )
			BuildForm();
		else if ( _activeSchemaDraft is not null )
			ShowSchemaDraft();

		RefreshMode();
		UpdateTitle();
	}

	/// <summary>
	/// Builds the outliner with a growing tree and a separate action footer.
	/// </summary>
	private Widget BuildOutliner()
	{
		var panel = new Widget( this )
		{
			MinimumWidth = 280,
			Layout = Layout.Column()
		};
		panel.Layout.Margin = 0;
		panel.Layout.Spacing = 0;

		var filter = new Widget( panel )
		{
			// Match the inspector's ~80px header.
			FixedHeight = 50,
			Layout = Layout.Column()
		};
		filter.Layout.Margin = 8;
		_search = new LineEdit( filter )
		{
			PlaceholderText = "Filter resources by name or path"
		};
		_search.TextChanged += _ => BuildBrowser();
		filter.Layout.Add( _search );
		panel.Layout.Add( filter );
		panel.Layout.AddSeparator( 1, Theme.WindowBackground );

		var body = new Widget( panel )
		{
			Layout = Layout.Column()
		};
		body.Layout.Margin = new Sandbox.UI.Margin( 0, 4 );
		panel.Layout.Add( body, 1 );
		_tree = new ResourceTreeView( body );
		body.Layout.Add( _tree, 1 );

		panel.Layout.AddSeparator( 1, Theme.WindowBackground );
		var footer = new InspectorSection( panel, footer: true );
		var add = new Button.Primary( "Add", "add", footer );
		add.Clicked += New;
		footer.Layout.Add( add, 1 );
		panel.Layout.Add( footer );

		Reload();
		return panel;
	}

	private Widget BuildInspector()
	{
		var panel = new Widget( this )
		{
			MinimumWidth = 420,
			Layout = Layout.Column()
		};
		panel.Layout.Margin = 0;
		panel.Layout.Spacing = 0;
		_inspectorPanel = panel;

		var header = new InspectorSection( panel );
		_thumbnail = new ResourcePreview( this, header );
		header.Layout.Add( _thumbnail );

		var identity = new Widget( header )
		{
			Layout = Layout.Column()
		};
		identity.Layout.Spacing = 2;
		header.Layout.Add( identity, 1 );

		var titleRow = identity.Layout.AddRow();
		titleRow.Spacing = 8;
		_heading = new Label( "Select a resource", identity );
		_heading.SetStyles( "font-size: 18px; font-weight: bold;" );
		_heading.WordWrap = true;
		titleRow.Add( _heading, 1 );

		_viewSerialized = this.GetSerialized();
		_viewControl = ControlWidget.Create( _viewSerialized.GetProperty( nameof( InspectorView ) ) );
		_viewControl.PaintBackground = false;
		_viewControl.FixedWidth = 140;
		_viewControl.MinimumHeight = Theme.RowHeight + 2;
		_viewControl.VerticalSizeMode = SizeMode.CanShrink;
		titleRow.Add( _viewControl );

		_sourceButton = new IconButton( "code", ShowGeneratedSource )
		{
			FixedSize = Theme.RowHeight,
			ToolTip = "View resource class source"
		};
		titleRow.Add( _sourceButton );
		_editTypeButton = new IconButton( "edit", () =>
		{
			var edit = _resource is null ? null : ManagedEdit( _resource.GetType() );
			if ( edit is not null )
				EditResourceType( edit );
		} )
		{
			FixedSize = Theme.RowHeight,
			ToolTip = "Edit resource type"
		};
		titleRow.Add( _editTypeButton );
		_metadata = new ResourceMetadata( this, identity );
		identity.Layout.Add( _metadata );
		panel.Layout.Add( header );
		panel.Layout.AddSeparator( 1, Theme.WindowBackground );
		_featureHeader = new Widget( panel )
		{
			Layout = Layout.Column()
		};
		_featureHeader.Layout.Margin = 0;
		_featureHeader.Layout.Spacing = 0;
		_featureHeader.Hide();
		panel.Layout.Add( _featureHeader );

		_jsonError = new Label( "", panel )
		{
			WordWrap = true
		};
		_jsonError.Hide();
		panel.Layout.Add( _jsonError );
		_json = new TextEdit( panel );
		_json.TextChanged += _ => JsonChanged();
		_json.Hide();
		panel.Layout.Add( _json, 1 );

		_generatedSource = new TextEdit( panel )
		{
			ReadOnly = true
		};
		_generatedSource.SetStyles( "font-family: Consolas; font-size: 12px;" );
		_generatedSource.TabSize = 32;
		_generatedSource.Hide();
		panel.Layout.Add( _generatedSource, 1 );
		_properties = new ScrollArea( panel );
		panel.Layout.Add( _properties, 1 );

		panel.Layout.AddSeparator( 1, Theme.WindowBackground );
		var footer = new InspectorSection( panel, footer: true );
		_discardButton = new Button( "Discard", footer )
		{
			ToolTip = "Discard pending changes to this resource only."
		};
		_discardButton.Clicked += DiscardSelected;

		_applyButton = new Button.Primary( "Apply changes", footer )
		{
			ToolTip = "Save pending changes to this resource only."
		};
		_applyButton.Clicked += ApplySelected;
		_pendingLabel = new Label( "", footer )
		{
			WordWrap = true
		};
		_pendingLabel.SetStyles( "color: #999;" );
		footer.Layout.Add( _pendingLabel, 1 );
		footer.Layout.Add( _discardButton );
		footer.Layout.Add( _applyButton );
		panel.Layout.Add( footer );

		return panel;
	}

	private void PopulateViewMenu( Menu view )
	{
		view.Clear();
		view.AddOption( "Restore To Default", "settings_backup_restore", RestoreDefaultView );
		view.AddSeparator();

		foreach ( var dock in DockManager.DockTypes )
		{
			var option = view.AddOption( dock.Title, dock.Icon );
			option.Checkable = true;
			option.Checked = DockManager.IsDockOpen( dock.Title );
			option.Toggled += enabled =>
			{
				DockManager.SetDockState( dock.Title, enabled );
				ApplyDockMinimumWidths();
			};
		}
	}

	/// <summary>
	/// Removes dock content insets and applies minimum widths to native dock areas.
	/// </summary>
	private void ApplyDockMinimumWidths()
	{
		var inspector = _inspectorPanel?.IsValid == true ? DockManager.FindDockWidget( _inspectorPanel ) : null;
		var outliner = DockManager.FindDockWidget( "Outliner" );
		Apply( inspector, 420 );
		Apply( outliner, outliner?.Parent == inspector?.Parent ? 420 : 280 );

		static void Apply( DockWidget dock, int width )
		{
			if ( dock?.IsValid != true || dock.Parent?.IsValid != true )
				return;

			dock.ContentMargins = 0;
			// Restrict the selector to the dock area so child controls retain their own sizes.
			dock.Parent.SetProperty( "resourceEditorPanel", true );
			dock.Parent.SetStyles( $"*[resourceEditorPanel=\"true\"] {{ min-width: {width}px; }}" );
		}
	}

	private void RestoreDefaultView()
	{
		var draft = _jsonMode ? _json.PlainText : null;
		var changed = _jsonChanged;
		var dirty = _dirty;

		ResetLayout();

		if ( draft is not null )
			_json.PlainText = draft;

		_jsonChanged = changed;
		_dirty = dirty;
		UpdateTitle();
	}
}
