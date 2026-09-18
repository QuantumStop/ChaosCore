namespace Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

/// <summary>
/// API for inspecting, validating, reading, and applying generic presets.
/// This is what should be interacted with to set presets, while a game is live.
/// </summary>
public static class PresetAPI
{
	/// <summary>
	/// Returns whether a preset can be applied to the supplied target.
	/// </summary>
	public static bool CanApply( GenericPresetResource preset, object target )
	{
		if ( preset is null || target is null )
			return false;

		var targetInfo = PresetRegistry.Find( preset.PresetType, target.GetType() );

		if ( targetInfo is null )
			return false;

		return targetInfo.SupportsMode( PresetModes.Normalize( preset.PresetMode ) );
	}

	/// <summary>
	/// Applies the preset to the target and, when possible, updates the target's
	/// PresetSelectorAttribute resource reference to the applied preset.
	/// </summary>
	public static bool TrySetAndApply( GenericPresetResource preset, Component target )
	{
		if ( preset is null || target is null )
			return false;

		if ( !TryApply( preset, target ) )
			return false;

		var type = Game.TypeLibrary.GetType( target.GetType() );

		if ( type is null )
			return false;

		var selector = type.Properties.FirstOrDefault(
			x =>
				x.PropertyType == typeof( GenericPresetResource ) &&
				x.CanWrite
		);

		selector?.SetValue( target, preset );

		return true;
	}

	/// <summary>
	/// Applies a preset to a component when compatible.
	/// TODO: This needs to be a private method *only* in the future!
	/// </summary>
	internal static bool TryApply( GenericPresetResource preset, Component target )
	{
		if ( target is null )
			return false;

		return TryApplyInternal( preset, target, out _ );
	}

	/// <summary>
	/// Returns the property names that would be affected by applying
	/// the preset to the supplied target.
	/// </summary>
	public static IReadOnlyList<string> GetAffectedProperties( GenericPresetResource preset, object target )
	{
		if ( preset is null || target is null )
			return [];

		var targetInfo = PresetRegistry.Find(
			preset.PresetType,
			target.GetType()
		);

		if ( targetInfo is null )
			return [];

		var mode = targetInfo.FindMode(
			PresetModes.Normalize( preset.PresetMode )
		);

		if ( mode is null )
			return [];

		var payload = ParsePayload( preset );

		if ( payload is null )
			return [];

		return [ .. GetPresetProperties( targetInfo, mode )
				.Where( x => payload.ContainsKey( x.Name ) )
				.Select( x => x.Name )
		];
	}

	/// <summary>
	/// Returns the properties participating in a preset target and mode.
	/// </summary>
	public static IEnumerable<PropertyDescription> GetPresetProperties( PresetTargetInfo target, PresetModeInfo mode )
	{
		if ( target?.TargetType is null || mode is null )
			yield break;

		foreach ( var property in GetCandidateProperties( target ) )
		{
			if ( !property.CanRead || !property.CanWrite )
				continue;

			if ( property.IsIndexer )
				continue;

			if ( property.PropertyType == typeof( GenericPresetResource ) )
				continue;

			if ( !HasAttribute<PropertyAttribute>( property ) )
				continue;

			if ( HasAttribute<PresetIgnoreAttribute>( property ) )
				continue;

			if ( mode.IncludeAll )
			{
				yield return property;
				continue;
			}

			var presetProperties = property.Attributes.OfType<PresetPropertyAttribute>();

			if ( presetProperties.Any( x => x.IncludesMode( mode.Key ) ) )
				yield return property;
		}
	}

	/// <summary>
	/// Attempts to read a typed value from a preset payload.
	/// </summary>
	public static bool TryGetValue<T>( GenericPresetResource preset, string propertyName, out T value )
	{
		value = default;

		if ( preset is null || string.IsNullOrWhiteSpace( propertyName ) )
			return false;

		var payload = ParsePayload( preset );

		if ( payload is null )
			return false;

		if ( !payload.TryGetPropertyValue( propertyName, out var node ) )
			return false;

		if ( node is null )
			return true;

		try
		{
			value = Json.FromNode<T>( node );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning(
				$"Failed reading preset '{preset.PresetType}' property " +
				$"'{propertyName}' as '{typeof( T ).Name}': {e.Message}"
			);

			return false;
		}
	}

	/// <summary>
	/// Writes a typed value into an in-memory preset payload.
	/// </summary>
	public static bool SetValue<T>( GenericPresetResource preset, string propertyName, T value )
	{
		if ( preset is null || string.IsNullOrWhiteSpace( propertyName ) )
			return false;

		var payload = ParsePayload( preset );

		if ( payload is null )
			return false;

		try
		{
			payload[propertyName] = Json.ToNode( value, typeof( T ) );

			preset.PayloadJson = payload.ToJsonString();

			return true;
		}
		catch ( Exception e )
		{
			Log.Warning(
				$"Failed writing preset '{preset.PresetType}' property " +
				$"'{propertyName}': {e.Message}"
			);

			return false;
		}
	}

	private static bool TryApplyInternal( GenericPresetResource preset, object target, out IReadOnlyList<string> changedProperties )
	{
		changedProperties = [];

		if ( preset is null || target is null )
			return false;

		var targetInfo = PresetRegistry.Find( preset.PresetType, target.GetType() );

		if ( targetInfo is null )
			return false;

		var mode = targetInfo.FindMode( PresetModes.Normalize( preset.PresetMode ) );

		if ( mode is null )
			return false;

		var payload = ParsePayload( preset );

		if ( payload is null )
			return false;

		var changed = new List<string>();

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
					$"Failed applying preset '{preset.PresetType}' property " +
					$"'{property.Name}' to '{target.GetType().Name}': {e.Message}"
				);
			}
		}

		changedProperties = changed;

		return changed.Count > 0;
	}

	private static IEnumerable<PropertyDescription> GetCandidateProperties( PresetTargetInfo target )
	{
		if ( target?.TargetType is null )
			return [];

		if ( target.IncludeInherited )
			return target.TargetType.Properties;

		return target.TargetType.DeclaredMembers.OfType<PropertyDescription>();
	}

	private static bool HasAttribute<T>( PropertyDescription property ) where T : Attribute
	{
		return property.Attributes.Any( x => x is T );
	}

	private static JsonObject ParsePayload( GenericPresetResource preset )
	{
		if ( preset is null )
			return null;

		if ( string.IsNullOrWhiteSpace( preset.PayloadJson ) )
			return [];

		try
		{
			return Json.ParseToJsonObject( preset.PayloadJson );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed parsing preset '{preset.PresetType}' payload: {e.Message}" );

			return null;
		}
	}
}
