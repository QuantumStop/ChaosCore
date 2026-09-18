namespace Core.Editor;

using System;
using System.Text.Json.Nodes;

public static partial class PresetService
{
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

		try
		{
			value = Json.FromNode<T>( node );

			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool SetValue( GenericPresetResource preset, string propertyName, object value, Type valueType )
	{
		if ( preset is null || string.IsNullOrWhiteSpace( propertyName ) || valueType is null )
			return false;

		var payload = ParsePayload( preset ) ?? [];

		payload[propertyName] = Json.ToNode( value, valueType );

		preset.PayloadJson = payload.ToJsonString();

		return true;
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
		catch
		{
			return null;
		}
	}
}
