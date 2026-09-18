namespace Core.Editor;

public partial class ComponentPresetEditor
{
	public void AssetOpen( Asset asset )
	{
		if ( asset is null )
			return;

		OpenAsset( asset );

		Show();
		Focus();
	}

	public void SelectPreset( Asset asset )
	{
		if ( asset is null || asset == _asset )
			return;

		OpenAsset( asset );
	}

	public void SelectMember( string memberName ) { }

	private void OpenAsset( Asset asset )
	{
		DisconnectSerializedObjects();

		var loaded = PresetService.Load( asset );

		if ( loaded is null )
		{
			Log.Warning( $"Unable to load preset '{asset.Path}'" );

			_asset = null;
			_currentPreset = null;

			_properties?.Clear();
			UpdateWindowTitle();
			return;
		}

		_asset = asset;
		_currentPreset = loaded;

		BuildSerializedObjects();
		UpdateWindowTitle();
	}

	private void BuildSerializedObjects()
	{
		if ( _currentPreset?.Resource is null )
		{
			_properties?.Clear();
			return;
		}

		_resourceSerialized = _currentPreset.Resource.GetSerialized();
		_resourceSerialized?.OnPropertyChanged += OnResourcePropertyChanged;

		_properties?.SetPreset( _currentPreset );
	}

	private void DisconnectSerializedObjects()
	{
		_resourceSerialized?.OnPropertyChanged -= OnResourcePropertyChanged;
		_resourceSerialized = null;
	}

	private void OnResourcePropertyChanged( SerializedProperty property )
	{
		UpdateWindowTitle();
		_presetView?.UpdatePreset( _currentPreset );
	}

	private void UpdateWindowTitle()
	{
		WindowTitle = _currentPreset is null
			? "Component Preset Editor"
			: $"{_currentPreset.Name} - Component Preset Editor";
	}
}
