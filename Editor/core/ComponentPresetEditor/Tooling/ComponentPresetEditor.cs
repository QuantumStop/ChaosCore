namespace Core.Editor;

using System;

[EditorForAssetType( "preset" )]
[EditorApp( "Component Preset Editor", "manage_accounts", "Edit component presets" )]
public partial class ComponentPresetEditor : DockWindow, IAssetEditor
{
	public bool CanOpenMultipleAssets => false;
	public Asset CurrentAsset => _asset;

	private Asset _asset;
	private PresetAsset _currentPreset;
	private SerializedObject _resourceSerialized;

	private PresetPropertiesWidget _properties;
	private ComponentPresetsView _presetView;
	private ToolBar _toolBar;

	public ComponentPresetEditor()
	{
		DeleteOnClose = true;
		WindowTitle = "Component Preset Editor";
		Size = new Vector2( 650, 300 );

		SetWindowIcon( "tune" );

		StateCookie = "ComponentPresetEditor";

		BuildDefaultLayout();
		RebuildUI();
	}

	protected override void OnClosed()
	{
		DisconnectSerializedObjects();
		base.OnClosed();
	}
}
