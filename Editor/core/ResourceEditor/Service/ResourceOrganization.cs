namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A resolved per project configured group. 
/// Unassigned types appear in Ungrouped.
/// </summary>
internal sealed record ResourceGroup( string Key, string Name, string Icon, params Type[] ResourceTypes );

/// <summary>
/// Resolves configured groups and their resource trees.
/// </summary>
internal static class ResourceOrganizationRegistry
{
	internal const string FallbackKey = "resource-editor.ungrouped";
	internal static readonly ResourceGroup Fallback = new( FallbackKey, "Ungrouped", "folder" );

	/// <summary>
	/// Places engine and "Core" types under Core declaration, other types use their project's title.
	/// </summary>
	internal static string Owner( Type type )
	{
		var ns = type.Namespace ?? "";

		var assembly = type.Assembly.GetName().Name ?? "";

		if ( ns == "Core" || ns.StartsWith( "Core.", StringComparison.Ordinal ) || ns == "Sandbox" || ns.StartsWith( "Sandbox.", StringComparison.Ordinal )
		|| assembly == "Sandbox" || assembly.StartsWith( "Sandbox.", StringComparison.Ordinal ) )
			return "Core";

		return Project.Current?.Config?.Title ?? "Game";
	}

	/// <summary>
	/// Loads group definitions from project options and resolves their resource types.
	/// </summary>
	internal static ResourceGroup[] Groups() => [.. ResourceEditorOptions.Load().Groups
		.Select( group => new ResourceGroup( group.Key, group.Name, group.Icon,
			[.. group.ResourceTypes.Select( ResourceCodegen.ResolveType ).Where( type => type is not null )] ) )];

	/// <summary>
	/// Builds owner and group sections. 
	/// Types may appear in several groups, while unassigned types are under "Ungrouped".
	/// </summary>
	internal static ResourceTypeGroup[] BuildGroups( ResourceEditorType[] types )
	{
		var definitions = Groups();
		var buckets = new Dictionary<(string Owner, string Group), ResourceTypeGroup>();

		foreach ( var type in types )
		{
			var owner = Owner( type.ResourceType );
			var assigned = false;
			
			foreach ( var definition in definitions )
			{
				if ( !Contains( definition, type.ResourceType ) )
					continue;

				Add( owner, definition, type );
				assigned = true;
			}
		
			if ( !assigned )
				Add( owner, Fallback, type );
		}

		var result = buckets.Values.ToArray();

		Array.Sort( result, ( left, right ) =>
		{
			var order = (left.Owner == "Core" ? 0 : 1).CompareTo( right.Owner == "Core" ? 0 : 1 );
			
			if ( order == 0 )
				order = string.Compare( left.Owner, right.Owner, StringComparison.CurrentCulture );
			
			if ( order == 0 )
				order = (left.Definition.Key == FallbackKey ? 1 : 0).CompareTo( right.Definition.Key == FallbackKey ? 1 : 0 );
			
			return order != 0 ? order : string.Compare( left.Definition.Name, right.Definition.Name, StringComparison.CurrentCulture );
		} );

		return result;

		void Add( string owner, ResourceGroup definition, ResourceEditorType type )
		{
			var key = (owner, definition.Key);
			if ( !buckets.TryGetValue( key, out var bucket ) )
				buckets[key] = bucket = new ResourceTypeGroup( owner, definition, new List<ResourceEditorType>() );

			bucket.Types.Add( type );
		}
	}

	private static bool Contains( ResourceGroup group, Type type )
	{
		foreach ( var member in group.ResourceTypes )
		{
			if ( member.IsAssignableFrom( type ) )
				return true;
		}

		return false;
	}
}

internal sealed record ResourceTypeGroup( string Owner, ResourceGroup Definition, List<ResourceEditorType> Types );
