namespace Core.Editor;

public partial class ComponentPresetEditor
{
	protected override void BuildDefaultLayout()
	{
		DockManager.RegisterDock( new()
		{
			Title = "Properties",
			Icon = "tune",
			Area = DockArea.Left,
			CreateAction = () =>
			{
				_properties = new PresetPropertiesWidget( this );
				return _properties;
			}
		} );

		DockManager.RegisterDock( new()
		{
			Title = "Component Presets View",
			Icon = "view_list",
			Area = DockArea.Right,
			CreateAction = () =>
			{
				_presetView = new ComponentPresetsView( this );
				return _presetView;
			}
		} );

		var properties = DockManager.OpenDock( "Properties", DockArea.Left );
		var presets = DockManager.OpenDock( "Component Presets View", DockArea.Right, properties );

		DockManager.SetSplitterProportions( presets, 0.62f, 0.38f );
	}

	private void RebuildUI()
	{
		MenuBar.Clear();

		BuildFileMenu();
		BuildViewMenu();
		BuildToolBar();
	}

	private void BuildFileMenu()
	{
		var file = MenuBar.AddMenu( "File" );

		file.AddOption( "New", "common/new.png", New, "editor.new" ).StatusTip = "Create a new preset";

		file.AddOption( "Open", "common/open.png", Open, "editor.open" ).StatusTip = "Open a preset";

		file.AddOption( "Save", "common/save.png", Save, "editor.save" ).StatusTip = "Save the current preset";

		file.AddOption( "Save As...", "common/save.png", SaveAs, "editor.save-as" ).StatusTip = "Save the current preset as a new asset";

		file.AddSeparator();

		file.AddOption( new Option( "Exit" )
		{
			Triggered = Close
		} );
	}

	private void BuildViewMenu()
	{
		var view = MenuBar.AddMenu( "View" );
		view.AboutToShow += () => PopulateViewMenu( view );
	}

	private void PopulateViewMenu( Menu view )
	{
		view.Clear();

		view.AddOption( "Restore To Default", "settings_backup_restore", ResetLayout );
		view.AddSeparator();

		foreach ( var dock in DockManager.DockTypes )
		{
			var option = view.AddOption( dock.Title, dock.Icon );

			option.Checkable = true;
			option.Checked = DockManager.IsDockOpen( dock.Title );
			option.Toggled += enabled => DockManager.SetDockState( dock.Title, enabled );
		}
	}

	private void BuildToolBar()
	{
		_toolBar?.Destroy();

		_toolBar = new ToolBar( this, "ComponentPresetEditor.Toolbar" );
		AddToolBar( _toolBar, ToolbarPosition.Top );

		_toolBar.AddOption( "New", "common/new.png", New ).StatusTip = "Create a new preset";

		_toolBar.AddOption( "Open", "common/open.png", Open ).StatusTip = "Open a preset";

		// Keep save directly available on the left-side toolbar.
		_toolBar.AddOption( "Save", "common/save.png", Save ).StatusTip = "Save the current preset";
	}
}
