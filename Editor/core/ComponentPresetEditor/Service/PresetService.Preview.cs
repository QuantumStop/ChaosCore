namespace Core.Editor;

using System;
using Sandbox;
using System.Threading.Tasks;

/// <summary>
/// Provides an optional visual preview for a preset target type.
/// </summary>
public interface IPresetPreviewProvider
{
	Task<Pixmap> RenderAsync( GenericPresetResource preset );

	bool BuildAssetPreview( GenericPresetResource preset, CameraComponent camera, 
		out GameObject primaryObject, out Vector3 sceneCenter, out Vector3 sceneSize );
}

public static partial class PresetService
{
	private static Dictionary<Type, IPresetPreviewProvider> _previewProviders;

	public static IPresetPreviewProvider GetPreviewProvider( GenericPresetResource preset )
	{
		if ( preset is null )
			return null;

		var target = PresetRegistry.Find( preset.PresetType );

		if ( target?.RuntimeType is null )
			return null;

		return GetPreviewProvider( target.RuntimeType );
	}

	public static IPresetPreviewProvider GetPreviewProvider( PresetAsset preset )
	{
		if ( preset?.Target?.RuntimeType is null )
			return null;

		return GetPreviewProvider( preset.Target.RuntimeType );
	}

	public static bool HasPreview( GenericPresetResource preset )
	{
		return GetPreviewProvider( preset ) is not null;
	}

	private static IPresetPreviewProvider GetPreviewProvider( Type targetType )
	{
		if ( targetType is null )
			return null;

		EnsurePreviewProviders();

		for ( var type = targetType; type is not null; type = type.BaseType )
		{
			if ( _previewProviders.TryGetValue( type, out var provider ) )
				return provider;
		}

		return null;
	}

	private static void EnsurePreviewProviders()
	{
		if ( _previewProviders is not null )
			return;

		_previewProviders = [];

		foreach ( var entry in EditorTypeLibrary.GetTypesWithAttribute<PresetPreviewForAttribute>() )
		{
			if ( entry.Attribute?.TargetType is null )
				continue;

			if ( !typeof( IPresetPreviewProvider ).IsAssignableFrom( entry.Type.TargetType ) )
				continue;

			if ( Activator.CreateInstance( entry.Type.TargetType ) is not IPresetPreviewProvider provider )
				continue;

			_previewProviders[entry.Attribute.TargetType] = provider;
		}
	}
}
