namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Linq;

public static partial class PresetService
{
	public static IEnumerable<PresetAsset> All()
	{
		foreach ( var asset in AssetSystem.All )
		{
			if ( !IsPresetAsset( asset ) )
				continue;

			PresetAsset preset;

			try
			{
				preset = Load( asset );
			}
			catch ( Exception e )
			{
				Log.Warning( $"Failed to load preset '{asset.RelativePath}': {e.Message}" );

				continue;
			}

			if ( preset is not null )
				yield return preset;
		}
	}

	public static IEnumerable<PresetAsset> FindFor( object target, string presetKey = null, string mode = null )
	{
		foreach ( var preset in All() )
		{
			if ( preset?.Resource is null )
				continue;

			if ( !string.IsNullOrWhiteSpace( presetKey ) )
			{
				if ( !string.Equals( preset.Resource.PresetType, presetKey, StringComparison.OrdinalIgnoreCase ) )
					continue;
			}

			if ( !string.IsNullOrWhiteSpace( mode ) )
			{
				if ( !string.Equals( PresetModes.Normalize( preset.Resource.PresetMode ), PresetModes.Normalize( mode ), StringComparison.OrdinalIgnoreCase ) )
					continue;
			}

			if ( target is not null && !CanApply( preset, target ) )
				continue;

			yield return preset;
		}
	}

	public static PresetAsset Load( Asset asset )
	{
		if ( asset is null )
			return null;

		if ( !IsPresetAsset( asset ) )
			return null;

		var resource = asset.LoadResource<GenericPresetResource>();

		if ( resource is null )
			return null;

		if ( string.IsNullOrWhiteSpace( resource.PresetType ) )
		{
			Log.Warning( $"Preset '{asset.Path}' has no PresetType." );

			return null;
		}

		resource.PresetMode = PresetModes.Normalize( resource.PresetMode );

		var definitions = PresetRegistry.FindAll( resource.PresetType );

		if ( definitions.Count == 0 )
		{
			Log.Warning(
				$"Unknown preset target '{resource.PresetType}' in '{asset.Path}'"
			);

			return null;
		}

		// The same preset type can be used by more than one target type,
		// so use any definition that supports the preset's selected mode.
		var targetInfo = definitions.FirstOrDefault( x => x.SupportsMode( resource.PresetMode ) );

		if ( targetInfo is null )
		{
			Log.Warning(
				$"Preset '{asset.Path}' uses unknown mode " +
				$"'{resource.PresetMode}' for '{resource.PresetType}'."
			);

			return null;
		}

		// Validate the payload while loading the preset so malformed JSON
		// is rejected here instead of causing errors later in the inspector.
		if ( !string.IsNullOrWhiteSpace( resource.PayloadJson ) )
		{
			try
			{
				var payload = Json.ParseToJsonObject( resource.PayloadJson );

				if ( payload is null )
				{
					Log.Warning( $"Preset '{asset.Path}' payload is not a JSON object." );

					return null;
				}
			}
			catch ( Exception e )
			{
				Log.Warning( $"Failed to parse preset payload '{asset.Path}': {e.Message}" );

				return null;
			}
		}

		resource.SourceAssetPath = asset.Path;

		if ( string.IsNullOrWhiteSpace( resource.PresetIcon ) )
			resource.PresetIcon = targetInfo.Icon;

		return new PresetAsset
		{
			Asset = asset,
			Resource = resource,
			Target = targetInfo
		};
	}

	public static bool CanApply( PresetAsset preset, object target ) => CanApply( preset?.Resource, target );

	public static bool CanApply( GenericPresetResource preset, object target )
	{
		if ( preset is null || target is null )
			return false;

		var targetInfo = PresetRegistry.Find( preset.PresetType, target.GetType() );

		if ( targetInfo is null )
			return false;

		return targetInfo.SupportsMode( preset.PresetMode );
	}
}
