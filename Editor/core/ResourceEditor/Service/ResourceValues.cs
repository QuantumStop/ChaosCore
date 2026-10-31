namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Sandbox.Resources;

/// <summary>
/// Compares authored values using native generator settings, ignoring their compiled caches.
/// </summary>
internal static class ResourceValues
{
	private static readonly Dictionary<string, TypeDescription> Generators = new();

	internal static void Invalidate() => Generators.Clear();

	internal static bool Equal( JsonNode left, JsonNode right ) => JsonNode.DeepEquals( left, right )
		|| JsonNode.DeepEquals( Normalize( left?.DeepClone() ), Normalize( right?.DeepClone() ) );

	/// <summary>
	/// Accepts empty collections created by native form controls, without accepting edits to populated lists.
	/// </summary>
	internal static void AcceptEmptyCollections( JsonNode saved, JsonNode before, JsonNode after )
	{
		if ( saved is JsonObject savedObject && before is JsonObject beforeObject && after is JsonObject afterObject )
		{
			foreach ( var property in beforeObject )
			{
				if ( !savedObject.ContainsKey( property.Key ) || !afterObject.ContainsKey( property.Key ) )
					continue;
				var value = afterObject[property.Key];
				if ( property.Value is null && savedObject[property.Key] is null && value is JsonArray { Count: 0 } )
				{
					savedObject[property.Key] = new JsonArray();
				}
				else
					AcceptEmptyCollections( savedObject[property.Key], property.Value, value );
			}
		}
		else if ( saved is JsonArray savedArray && before is JsonArray beforeArray && after is JsonArray afterArray
			&& savedArray.Count == beforeArray.Count && beforeArray.Count == afterArray.Count )
		{
			for ( var i = 0; i < beforeArray.Count; i++ )
				AcceptEmptyCollections( savedArray[i], beforeArray[i], afterArray[i] );
		}
	}

	private static JsonNode Normalize( JsonNode node )
	{
		if ( node is JsonObject obj )
		{
			if ( obj["$compiler"] is JsonValue && obj["$source"] is JsonValue source
				&& source.TryGetValue<string>( out var name ) && name is not null && obj["data"] is JsonObject data )
			{
				if ( !Generators.TryGetValue( name, out var type ) )
				{
					type = EditorTypeLibrary.GetType( name );
					Generators[name] = type;
				}

				if ( type is not null && !type.TargetType.IsAbstract && typeof( ResourceGenerator ).IsAssignableFrom( type.TargetType ) )
				{
					try
					{
						var generator = type.Create<ResourceGenerator>();
						generator.Deserialize( data );
						obj["data"] = JsonNode.Parse( Json.Serialize( generator ) );
						obj.Remove( "compiled" );
					}
					catch ( Exception )
					{
						// Keep invalid generator data intact so it still compares as an edit.
					}
				}
			}

			foreach ( var property in obj )
			{	
				Normalize( property.Value );
			}
		}
		else if ( node is JsonArray array )
		{
			foreach ( var item in array )
			{	
				Normalize( item );
			}
		}
		return node;
	}
}
