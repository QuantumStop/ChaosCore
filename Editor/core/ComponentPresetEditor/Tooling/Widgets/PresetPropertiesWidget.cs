namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Linq;

public sealed class PresetPropertiesWidget : Widget
{
	private readonly ComponentPresetEditor _editor;
	private readonly ScrollArea _scroll;

	private GameObject _temporaryGameObject;
	private object _temporaryTarget;
	private SerializedObject _targetSerialized;
	private GenericPresetResource _resource;

	private PresetPreviewWidget _preview;

	public PresetPropertiesWidget( ComponentPresetEditor editor ) : base( null )
	{
		_editor = editor;

		Layout = Layout.Column();

		_scroll = new ScrollArea( this );
		Layout.Add( _scroll, 1 );

		Clear();
	}

	public void SetPreset( PresetAsset preset )
	{
		Clear();

		if ( preset?.Resource is null )
			return;

		_resource = preset.Resource;

		var container = new Widget( _scroll )
		{
			Layout = Layout.Column()
		};

		container.SetSizeMode( SizeMode.Flexible, SizeMode.CanShrink );
		container.Layout.Margin = 0;
		container.Layout.Spacing = 0;

		// Preset metadata

		var metadataSection = new PresetCollapsibleSection( "Preset", "tune", Theme.Blue, true, container )
		{
			StateCookieName = "ComponentPresetEditor.Section.Preset"
		};

		metadataSection.RestoreState();

		SerializedObject resourceSerialized = _resource.GetSerialized();

		var metadataSheet = new ControlSheet
		{
			IncludePropertyNames = true
		};

		metadataSheet.AddObject( resourceSerialized, property => property.Name switch
			{
				nameof( GenericPresetResource.PresetType ) => false,
				nameof( GenericPresetResource.PresetMode ) => false,
				nameof( GenericPresetResource.PayloadJson ) => false,
				nameof( GenericPresetResource.SourceAssetPath ) => false,
				_ => true
			}
		);

		if ( PresetService.GetPreviewProvider( preset ) is not null )
		{
			var metadataBody = new Widget( metadataSection.Content )
			{
				Layout = Layout.Row()
			};

			metadataBody.Layout.Margin = 0;
			metadataBody.Layout.Spacing = 8;

			_preview = new PresetPreviewWidget( preset, metadataBody );

			metadataBody.Layout.Add( _preview );
			metadataBody.Layout.Add( metadataSheet, 1 );

			metadataSection.Content.Layout.Add( metadataBody );
		}
		else
		{
			metadataSection.Content.Layout.Add( metadataSheet );
		}

		container.Layout.Add( metadataSection );

		// Actual target payload

		_temporaryTarget = PresetService.CreateEditableTarget( _resource, out _temporaryGameObject );

		if ( _temporaryTarget is not null )
		{
			_targetSerialized = _temporaryTarget.GetSerialized();

			if ( _targetSerialized is not null )
			{
				_targetSerialized.OnPropertyChanged += OnTargetPropertyChanged;

				var targetInfo = PresetRegistry.Find( _resource.PresetType, _temporaryTarget.GetType() );

				var mode = targetInfo?.FindMode( _resource.PresetMode );

				var allowed = targetInfo is not null && mode is not null
					? PresetService
						.GetPresetProperties( targetInfo, mode )
						.Select( x => x.Name )
						.ToHashSet( StringComparer.OrdinalIgnoreCase )
					: new HashSet<string>( StringComparer.OrdinalIgnoreCase );

				var payloadSection = new PresetCollapsibleSection( preset.Target?.Name ?? _temporaryTarget.GetType().Name.ToTitleCase(),
					preset.Target?.Icon ?? "tune", Theme.Blue, true, container )
				{
					StateCookieName = $"ComponentPresetEditor.Section.{_resource.PresetType}"
				};

				payloadSection.RestoreState();

				var targetSheet = new ControlSheet
				{
					IncludePropertyNames = true
				};

				targetSheet.AddObject( _targetSerialized, property => allowed.Contains( property.Name ) );

				payloadSection.Content.Layout.Add( targetSheet );
				container.Layout.Add( payloadSection );
			}
		}

		_scroll.Canvas = container;
	}

	public void Clear()
	{
		_targetSerialized?.OnPropertyChanged -= OnTargetPropertyChanged;

		_targetSerialized = null;
		_temporaryTarget = null;

		_temporaryGameObject?.Destroy();
		_temporaryGameObject = null;

		_preview = null;
		_resource = null;

		_scroll.Canvas = null;
	}

	private void OnTargetPropertyChanged( SerializedProperty property )
	{
		if ( _resource is null || _temporaryTarget is null )
			return;

		if ( !PresetService.CaptureInto( _resource, _temporaryTarget ) )
			return;

		_preview?.Refresh();
	}
}
