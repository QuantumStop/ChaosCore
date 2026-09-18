namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static partial class PresetService
{
	public static bool Apply( PresetAsset preset, object target ) => Apply( preset?.Resource, target, out _ );

	public static bool Apply( GenericPresetResource preset, object target ) => Apply( preset, target, out _ );

	public static bool Apply( PresetAsset preset, object target, out IReadOnlyList<string> changedProperties ) => Apply( preset?.Resource, target, out changedProperties );

	public static bool Apply( GenericPresetResource preset, object target, out IReadOnlyList<string> changedProperties )
	{
		var changed = new List<string>();

		changedProperties = changed;

		if ( !CanApply( preset, target ) )
			return false;

		var targetInfo = PresetRegistry.Find( preset.PresetType, target.GetType() );

		if ( targetInfo is null )
			return false;

		var mode = targetInfo.FindMode( preset.PresetMode );

		if ( mode is null )
			return false;

		var payload = ParsePayload( preset );

		if ( payload is null )
			return false;

		foreach ( var property in GetPresetProperties( targetInfo, mode ) )
		{
			if ( !payload.TryGetPropertyValue( property.Name, out var node ) )
				continue;

			try
			{
				var value = node is null ? null : Json.FromNode( node, property.PropertyType );

				property.SetValue( target, value );

				changed.Add( property.Name );
			}
			catch ( Exception e )
			{
				Log.Warning(
					$"Failed applying preset property " +
					$"'{property.Name}' to '{target.GetType().Name}': {e.Message}"
				);
			}
		}

		return changed.Count > 0;
	}

	public static IReadOnlyList<string> GetAffectedProperties( GenericPresetResource preset, object target )
	{
		if ( !CanApply( preset, target ) )
			return [];

		var targetInfo = PresetRegistry.Find( preset.PresetType, target.GetType() );

		var mode = targetInfo?.FindMode( preset.PresetMode );

		if ( targetInfo is null || mode is null )
			return [];

		var payload = ParsePayload( preset );

		if ( payload is null )
			return [];

		return [.. GetPresetProperties( targetInfo, mode )
			.Where( x => payload.ContainsKey( x.Name ) )
			.Select( x => x.Name )];
	}

	public static IEnumerable<PropertyInfo> GetPresetProperties( PresetTargetInfo target, PresetModeInfo mode )
	{
		if ( target?.RuntimeType is null || mode is null )
			yield break;

		var flags = BindingFlags.Instance | BindingFlags.Public;
		
		if ( !target.IncludeInherited )
			flags |= BindingFlags.DeclaredOnly;

		var properties = target.RuntimeType.GetProperties( flags );

		foreach ( var property in properties )
		{
			if ( !property.CanRead || !property.CanWrite )
				continue;

			if ( property.GetIndexParameters().Length > 0 )
				continue;

			if ( property.GetCustomAttribute<PropertyAttribute>( true ) is null )
				continue;

			// If we have an ignore attribute on it, it doesn't participate
			if ( property.GetCustomAttribute<PresetIgnoreAttribute>( true ) is not null )
				continue;

			// Preset resource entry itself shouldn't participate either
			if ( property.GetCustomAttribute<PresetSelectorAttribute>( true ) is not null )
				continue;

			// In include all case we accept all properties
			if ( mode.IncludeAll )
			{
				yield return property;
				continue;
			}

			var presetProperties = property.GetCustomAttributes<PresetPropertyAttribute>( true );

			if ( presetProperties.Any( x => x.IncludesMode( mode.Key ) ) )
				yield return property;
		}
	}
}
