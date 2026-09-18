namespace Core;

using System;
using System.Collections.Generic;
using System.Linq;

public static class PresetModes
{
	public const string Default = "default";

	public static string Normalize( string mode )
	{
		return string.IsNullOrWhiteSpace( mode )
			? Default
			: mode.Trim();
	}
}

public sealed class PresetModeInfo
{
	public string Key { get; init; }

	public bool IncludeAll { get; init; }

	public override string ToString()
	{
		return Key;
	}
}

public sealed class PresetTargetInfo
{
	public string Key { get; init; }

	public string Name { get; internal set; }

	public string Icon { get; internal set; }

	public string Category { get; internal set; }

	public TypeDescription TargetType { get; init; }

	public bool IncludeInherited { get; init; }

	internal Dictionary<string, PresetModeInfo> ModeMap { get; init; } = new( StringComparer.OrdinalIgnoreCase );

	public IEnumerable<PresetModeInfo> Modes => ModeMap.Values;

	public Type RuntimeType => TargetType?.TargetType;

	public PresetModeInfo FindMode( string mode )
	{
		mode = PresetModes.Normalize( mode );

		return ModeMap.TryGetValue( mode, out var info ) ? info : null;
	}

	public bool SupportsMode( string mode )
	{
		return FindMode( mode ) is not null;
	}

	public bool CanTarget( object target )
	{
		if ( target is null || RuntimeType is null )
			return false;

		return RuntimeType.IsAssignableFrom( target.GetType() );
	}

	public override string ToString()
	{
		return $"{Name ?? Key} ({RuntimeType?.Name ?? "unknown"})";
	}
}

public static class PresetRegistry
{
	private static List<PresetTargetInfo> _all;

	private static Dictionary<string, List<PresetTargetInfo>> _byKey;

	public static IReadOnlyList<PresetTargetInfo> All
	{
		get
		{
			EnsureBuilt();

			return _all;
		}
	}

	public static IReadOnlyList<PresetTargetInfo> FindAll( string key )
	{
		EnsureBuilt();

		if ( string.IsNullOrWhiteSpace( key ) )
			return [];

		return _byKey.TryGetValue( key, out var targets ) ? targets : Array.Empty<PresetTargetInfo>();
	}

	public static PresetTargetInfo Find( string key )
	{
		var definitions = FindAll( key );
		return definitions.Count > 0 ? definitions[0] : null;
	}

	public static PresetTargetInfo Find( string key, Type targetType )
	{
		if ( targetType is null )
			return Find( key );

		return FindAll( key ).FirstOrDefault( x =>
			x.RuntimeType is not null &&
			x.RuntimeType.IsAssignableFrom( targetType )
		);
	}

	public static IEnumerable<PresetTargetInfo> FindForTarget( Type targetType )
	{
		EnsureBuilt();

		if ( targetType is null )
			yield break;

		foreach ( var info in _all )
		{
			if ( info.RuntimeType is null )
				continue;

			if ( info.RuntimeType.IsAssignableFrom( targetType ) )
				yield return info;
		}
	}

	public static IEnumerable<PresetTargetInfo> FindForTarget( object target )
	{
		if ( target is null )
			return [];

		return FindForTarget( target.GetType() );
	}

	public static PresetTargetInfo FindForTarget( object target, string key )
	{
		if ( target is null )
			return null;

		return Find( key, target.GetType() );
	}

	public static PresetModeInfo FindMode( string key, Type targetType, string mode )
	{
		return Find( key, targetType )?.FindMode( mode );
	}

	private static void EnsureBuilt()
	{
		if ( _all is not null && _byKey is not null && _all.Count > 0 )
			return;

		Rebuild();
	}

	private static void Rebuild()
	{
		_all = [];

		_byKey = new( StringComparer.OrdinalIgnoreCase );

		foreach ( var entry in Game.TypeLibrary.GetTypesWithAttribute<PresetTargetAttribute>() )
		{
			var attribute = entry.Attribute;

			if ( attribute is null )
				continue;

			if ( string.IsNullOrWhiteSpace( attribute.Key ) )
				continue;

			var key = attribute.Key.Trim();

			var info = new PresetTargetInfo
			{
				Key = key,
				Name = !string.IsNullOrWhiteSpace( attribute.Name ) ? attribute.Name : entry.Type.Title ?? key,
				Icon = !string.IsNullOrWhiteSpace( attribute.Icon ) ? attribute.Icon : entry.Type.Icon ?? "tune",
				Category = !string.IsNullOrWhiteSpace( attribute.Category ) ? attribute.Category : "General",
				TargetType = entry.Type,
				IncludeInherited = attribute.IncludeInherited
			};

			info.ModeMap[PresetModes.Default] = new PresetModeInfo
			{
				Key = PresetModes.Default,
				IncludeAll = attribute.IncludeAll
			};

			foreach ( var modeName in attribute.Modes ?? [] )
			{
				if ( string.IsNullOrWhiteSpace( modeName ) )
					continue;

				var mode = PresetModes.Normalize( modeName );

				if ( string.Equals( mode, PresetModes.Default, StringComparison.OrdinalIgnoreCase ) )
					continue;

				info.ModeMap[mode] = new PresetModeInfo
				{
					Key = mode,
					IncludeAll = false
				};
			}

			_all.Add( info );

			if ( !_byKey.TryGetValue( key, out var targets ) )
			{
				targets = new();
				_byKey[key] = targets;
			}

			targets.Add( info );
		}

		_all = [.. _all
			.OrderBy( x => x.Category )
			.ThenBy( x => x.Name )
			.ThenBy( x => x.RuntimeType?.Name )];
	}

	public static void Invalidate()
	{
		_all = null;
		_byKey = null;
	}

}
