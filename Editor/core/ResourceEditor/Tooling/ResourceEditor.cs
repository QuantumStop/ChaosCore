namespace Core.Editor;

using System;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// A registered data resource type exposed by Resource Editor.
/// </summary>
internal sealed record ResourceEditorType( string Name, string Extension, string Icon, 
	Func<GameResource> Create, Type ResourceType, AssetType AssetType );

/// <summary>
/// Inspector content modes. 
/// </summary>
public enum ResourceInspectorViewType
{
	Form,
	JSON,
	Code
}

/// <summary>
/// Browses and edits project resources. Data changes remain pending until Apply or Save.
/// </summary>
public sealed partial class ResourceEditor : DockWindow, IAssetEditor, AssetSystem.IEventListener
{
	public bool CanOpenMultipleAssets => false;

	public Asset CurrentAsset { get; private set; }

	private ResourceEditorType _type;
	private GameResource _resource;
	private SerializedObject _serialized;
	private bool _dirty;
	private TextEdit _json;
	private TextEdit _generatedSource;
	private ResourceInspectorViewType _inspectorView;
	private ResourceInspectorViewType _previousInspectorView;
	private string _sourceText = "";
	private bool _jsonChanged;
	private Label _jsonError;
	private ControlWidget _viewControl;
	private IconButton _sourceButton;
	private IconButton _editTypeButton;
	private SerializedObject _viewSerialized;

	private LineEdit _search;
	private ResourceTreeView _tree;
	private ScrollArea _properties;
	private Widget _featureHeader;
	private Label _heading;
	private ResourceMetadata _metadata;
	private Widget _thumbnail;
	private Button _applyButton;
	private Button _discardButton;
	private Label _pendingLabel;
	private Widget _inspectorPanel;

	private readonly List<Entry> _entries = [];
	private Entry _current;

	private bool HasPending => _dirty || _entries.Any( x => x.Dirty || x.Removed || x.Asset is null ) || _schemaEdits.Any( x => x.Dirty );

	private ResourceEditor()
	{
		ResourceCatalog.Invalidate();
		DeleteOnClose = true;
		WindowTitle = "Resource Editor";
		Size = new Vector2( 1100, 750 );
		StateCookie = "ChaosCore.ResourceEditor.Layout";
		SetWindowIcon( "dataset" );
		DockManager.OnLayoutLoaded += ApplyDockMinimumWidths;
		try
		{
			BuildDefaultLayout();
		}
		catch
		{
			Destroy();
			throw;
		}

		var file = MenuBar.AddMenu( "File" );
		file.AddOption( "New resource", "common/new.png", New, "editor.new" );
		file.AddOption( "Save all pending changes", "common/save.png", Save, "editor.save" );
		file.AddOption( "Revert changes", "undo", Revert );
		file.AddOption( "Options...", "settings", OpenOptions );

		var view = MenuBar.AddMenu( "View" );
		view.AboutToShow += () => PopulateViewMenu( view );

		var toolbar = new ToolBar( this, "ResourceEditor.Toolbar" );
		AddToolBar( toolbar, ToolbarPosition.Top );
		toolbar.AddOption( "New", "common/new.png", New );
		toolbar.AddOption( "Save all", "common/save.png", Save );
		toolbar.AddOption( "Refresh list", "refresh", Reload );
		toolbar.AddOption( "Options", "settings", OpenOptions );

		Reload();
	}

	private void ShowError( string message )
	{
		_jsonError.Text = message;
		_jsonError.Show();
	}

	protected override bool OnClose()
	{
		if ( !HasPending )
			return base.OnClose();

		WithSavedChanges( Close );

		return false;
	}

	protected override void OnClosed()
	{
		CloseChildWindows();
		Disconnect();
		base.OnClosed();
	}

	/// <summary>
	/// Reuses the live editor if one is around, otherwise creates a new instance.
	/// </summary>
	public static ResourceEditor Open()
	{
		var editor = All.OfType<ResourceEditor>().FirstOrDefault( window => window.IsValid ) ?? new ResourceEditor();
		if ( editor.IsMinimized )
			editor.MakeWindowed();

		if ( !editor.Visible )
			editor.Show();
		editor.Raise();
		editor.Focus();
		return editor;
	}

	/// <summary>
	/// Opens a supported asset in the existing editor, creating the window when needed.
	/// </summary>
	[EditorForAssetType( "npc" )]
	[EditorForAssetType( "ai" )]
	[EditorForAssetType( "aigoal" )]
	public static void OpenAsset( Asset asset ) => Open().AssetOpen( asset );

	// EditorApp creates a Widget for each activation. This entry point delegates to
	// the singleton without constructing another editor window.
	// This way we make sure we always only have one resource editor and I don't like 
	// doing it like this, but there's also no proper way to do this.
	/// <summary>
	/// Native Tools entry point that opens or focuses the single Resource Editor window.
	/// </summary>
	[EditorApp( "Resource Editor", "format_shapes", "Create and manage data resources" )]
	public sealed class Tool : Widget
	{
		public Tool() : base( null ) { }

		public override void Show()
		{
			Open();
			Destroy();
		}
	}

	private OptionsWindow _optionsWindow;
	private ResourceAddWindow _addWindow;

	private void OpenOptions()
	{
		if ( _optionsWindow?.IsValid == true )
		{
			_optionsWindow.Show();
			_optionsWindow.Focus();
			return;
		}

		_optionsWindow = new OptionsWindow( this );
		_optionsWindow.Show();
	}

	/// <summary>
	/// Closes all related dialogs when the tool shuts down.
	/// </summary>
	private void CloseChildWindows()
	{
		if ( _closeDialog?.IsValid == true )
			_closeDialog.Close();

		if ( _addWindow?.IsValid == true )
			_addWindow.Close();

		if ( _optionsWindow?.IsValid == true )
			_optionsWindow.Close();
	}

	public override void OnDestroyed()
	{
		CloseChildWindows();
		base.OnDestroyed();
	}
}
