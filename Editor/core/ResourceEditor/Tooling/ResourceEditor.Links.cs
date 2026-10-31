namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

public sealed partial class ResourceEditor
{
	private static string LinkKey( Asset asset )
	{
		var path = asset?.RelativePath?.Replace( '\\', '/' ) ?? "";

		return path.EndsWith( "_c", StringComparison.OrdinalIgnoreCase ) ? path[..^2] : path;
	}

	private static Asset LinkSource( Asset asset ) => AssetSystem.FindByPath( LinkKey( asset ) ) ?? asset;

	private static bool VisibleLink( Asset asset ) => asset?.AssetType?.IsGameResource == true && asset.AssetType.ResourceType is not null && !asset.AssetType.ResourceType.IsAbstract && typeof( GameResource ).IsAssignableFrom( asset.AssetType.ResourceType ) && HasEditableSource( asset );

	private static IEnumerable<string> JsonPaths( JsonNode node )
	{
		if ( node is JsonObject obj )
		{
			foreach ( var item in obj )
			{
				foreach ( var path in JsonPaths( item.Value ) )
				{
					yield return path;
				}
			}
		}
		else if ( node is JsonArray array )
		{
			foreach ( var item in array )
			{
				foreach ( var path in JsonPaths( item ) )
				{
					yield return path;
				}
			}
		}
		else if ( node is JsonValue value && value.TryGetValue<string>( out var text ) && !string.IsNullOrWhiteSpace( text ) )
			yield return text;
	}

	private bool HasLiveLinks( Entry entry ) => entry.Resource is not null && (entry.Dirty || entry.Asset is null || (entry == _current && _dirty));

	private readonly Dictionary<(Entry Entry, bool UsedBy), Asset[]> _linkCache = [];

	/// <summary>
	/// Caches visible references or "users" for an entry until resource edits invalidate them.
	/// </summary>
	private Asset[] GetLinks( Entry entry, bool usedBy )
	{
		if ( entry is null || entry.Removed )
			return [];

		var key = (entry, usedBy);
		if ( !_linkCache.TryGetValue( key, out var links ) )
			_linkCache[key] = links = ComputeLinks( entry, usedBy );

		return links;
	}

	/// <summary>
	/// Combines saved asset links with pending resource values, excluding resources this editor cannot show.
	/// </summary>
	private Asset[] ComputeLinks( Entry entry, bool usedBy )
	{
		if ( entry is null || entry.Removed )
			return [];

		var links = new Dictionary<string, Asset>( StringComparer.OrdinalIgnoreCase );
		void Add( Asset asset )
		{
			if ( asset is null )
				return;

			asset = LinkSource( asset );

			if ( VisibleLink( asset ) && !string.Equals( LinkKey( asset ), LinkKey( entry.Asset ), StringComparison.OrdinalIgnoreCase ) )
				links[LinkKey( asset )] = asset;
		}

		if ( !usedBy && HasLiveLinks( entry ) )
		{
			var resource = entry == _current ? _resource : entry.Resource;

			foreach ( var path in JsonPaths( resource.Serialize() ) )
			{
				Add( AssetSystem.FindByPath( path ) );
			}
		}
		else if ( entry.Asset is not null )
		{
			foreach ( var asset in usedBy ? entry.Asset.GetDependants( false ) : entry.Asset.GetReferences( false ) )
			{
				Add( asset );
			}
		}

		if ( usedBy && entry.Asset is not null )
		{
			foreach ( var candidate in _entries.Where( x => x.Asset is not null && (x.Removed || HasLiveLinks( x )) ) )
			{
				links.Remove( LinkKey( candidate.Asset ) );

				if ( !candidate.Removed && GetLinks( candidate, false ).Any( a => string.Equals( LinkKey( a ), LinkKey( entry.Asset ), StringComparison.OrdinalIgnoreCase ) ) )
					Add( candidate.Asset );
			}
		}

		return links.Values.OrderBy( x => x.Name ).ThenBy( x => x.RelativePath ).ToArray();
	}

	/// <summary>
	/// Navigates to a linked resource within this editor.
	/// </summary>
	private void OpenLinkedAsset( Asset asset )
	{
		asset = LinkSource( asset );

		if ( !VisibleLink( asset ) || !CaptureCurrent() )
			return;

		OpenResource( asset );
	}
}
