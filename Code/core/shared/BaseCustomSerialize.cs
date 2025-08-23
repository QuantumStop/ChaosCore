using System;
using System.Text.Json.Nodes;
public class BaseCustomSerialize : Component
{
	[Property, Feature( "Debug" ), Order( 10000 ), ReadOnly] public string SerializedGuid { get; set; }
	protected override void OnEnabled()
	{
		base.OnEnabled();
		SerializedGuid = Id.ToString();
	}
	public virtual JsonObject CustomSerialize()
	{
		SerializedGuid = Id.ToString();
		JsonObject customdata = new JsonObject
		{
			{"__guid", Id},
			{"SerializedGuid", SerializedGuid}
		};

		return customdata;
	}
	public virtual void CustomDeserialize( JsonObject node ) { }

	public static explicit operator BaseCustomSerialize( JsonNode v )
	{
		throw new NotImplementedException();
	}
}
