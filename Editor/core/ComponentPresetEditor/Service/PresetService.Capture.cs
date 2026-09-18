namespace Core.Editor;

using System;
using System.Text.Json.Nodes;

public static partial class PresetService
{
	public static PresetAsset Capture( string presetKey, object target, string name, string mode = null )
	{
		if ( target is null )
			return null;

		var targetInfo = PresetRegistry.Find( presetKey, target.GetType() );

		if ( targetInfo is null )
		{
			Log.Warning( $"Target '{target.GetType().Name}' does not support preset '{presetKey}'." );

			return null;
		}

		mode = PresetModes.Normalize( mode );

		var modeInfo = targetInfo.FindMode( mode );

		if ( modeInfo is null )
		{
			Log.Warning( $"Preset target '{presetKey}' does not support mode '{mode}'." );

			return null;
		}

		JsonObject payload = CapturePayload( targetInfo, modeInfo, target );

		if ( payload is null )
			return null;

		var resource = new GenericPresetResource
		{
			PresetType = targetInfo.Key,
			PresetMode = modeInfo.Key,
			PresetName = string.IsNullOrWhiteSpace( name ) ? targetInfo.Name : name,
			PresetIcon = targetInfo.Icon,
			PresetDescription = null,
			PayloadJson = payload.ToJsonString()
		};

		return new PresetAsset
		{
			Resource = resource,
			Target = targetInfo
		};
	}

	public static bool CaptureInto( GenericPresetResource resource, object target )
	{
		if ( resource is null || target is null )
			return false;

		var targetInfo = PresetRegistry.Find( resource.PresetType, target.GetType() );

		if ( targetInfo is null )
			return false;

		var mode = targetInfo.FindMode( resource.PresetMode );

		if ( mode is null )
			return false;

		JsonObject payload = CapturePayload( targetInfo, mode, target );

		if ( payload is null )
			return false;

		resource.PayloadJson = payload.ToJsonString();

		return true;
	}

	public static object CreateEditableTarget( GenericPresetResource resource, out GameObject temporaryGameObject )
	{
		temporaryGameObject = null;

		if ( resource is null )
			return null;

		var targetInfo = PresetRegistry.Find( resource.PresetType );

		if ( targetInfo?.RuntimeType is null )
			return null;

		try
		{
			object target;

			// Small hack: We're just essentially creating a temporary GO,
			// so we can more easily attach stuff and do all kinds of stuff with it.
			// TODO: Let's not use hacks... maybe??

			if ( typeof( Component ).IsAssignableFrom( targetInfo.RuntimeType ) )
			{
				temporaryGameObject = new GameObject( true, $"Preset Editor - {resource.PresetName}" );
				target = temporaryGameObject.Components.Create( targetInfo.TargetType );
			}
			else
				target = Activator.CreateInstance( targetInfo.RuntimeType );

			if ( target is null )
			{
				temporaryGameObject?.Destroy();
				temporaryGameObject = null;

				return null;
			}

			// Populate our editable target from the preset now.
			Apply( resource, target );

			return target;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Unable to construct preset editor target " +
				$"for '{resource.PresetType}': {e.Message}"
			);

			temporaryGameObject?.Destroy();
			temporaryGameObject = null;

			return null;
		}
	}

	/// <summary>
	/// <para>Creates an empty unsaved preset.</para>
	///
	/// Primarily used by ComponentPresetEditor when creating an
	/// asset without capturing a live component.
	/// </summary>
	public static PresetAsset Create( PresetTargetInfo targetInfo, string mode, string name = null )
	{
		if ( targetInfo is null )
			return null;

		mode = PresetModes.Normalize( mode );

		var modeInfo = targetInfo.FindMode( mode );

		if ( modeInfo is null )
			return null;

		JsonObject payload = null;
		GameObject temporaryObject = null;

		try
		{
			if ( typeof( Component ).IsAssignableFrom( targetInfo.RuntimeType ) )
			{
				temporaryObject = new GameObject( true, $"Preset Defaults - {targetInfo.Name}" );

				Component component = temporaryObject.Components.Create( targetInfo.TargetType );

				if ( component is not null )
					payload = CapturePayload( targetInfo, modeInfo, component );

			}
			else
			{
				// Plain runtime target fallback.
				object instance = Activator.CreateInstance( targetInfo.RuntimeType );

				if ( instance is not null )
					payload = CapturePayload( targetInfo, modeInfo, instance );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed creating default preset payload for " +
				$"'{targetInfo.Key}': {e.Message}" );
		}
		finally
		{
			temporaryObject?.Destroy();
		}

		payload ??= [];

		var resource = new GenericPresetResource
		{
			PresetType = targetInfo.Key,
			PresetMode = modeInfo.Key,
			PresetName = string.IsNullOrWhiteSpace( name ) ? $"New {targetInfo.Name}" : name,
			PresetIcon = targetInfo.Icon,
			PayloadJson = payload.ToJsonString()
		};

		return new PresetAsset
		{
			Resource = resource,
			Target = targetInfo
		};
	}

	private static JsonObject CapturePayload( PresetTargetInfo targetInfo, PresetModeInfo mode, object target )
	{
		JsonObject payload = [];

		foreach ( var property in GetPresetProperties( targetInfo, mode ) )
		{
			try
			{
				var value = property.GetValue( target );

				payload[property.Name] = Json.ToNode( value, property.PropertyType );
			}
			catch ( Exception e )
			{
				Log.Warning( $"Preset '{targetInfo.Key}' failed to capture " +
					$"'{property.Name}' ({property.PropertyType.Name}): {e.Message}"
				);
			}
		}

		return payload;
	}
}
