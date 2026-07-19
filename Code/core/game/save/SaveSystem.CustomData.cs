#if IGNIS || STANDALONE
namespace Core;

using System.Text.Json.Nodes;

public sealed partial class SaveSystem
{
	private JsonArray CollectCustomComponentData()
	{
		var customData = new JsonArray();
		foreach ( var component in Scene.GetAllComponents<BaseCustomSerialize>() )
			customData.Add( component.CustomSerialize() );

		return customData;
	}

	private void RestoreCustomComponentData( JsonArray customData )
	{
		if ( customData is null )
			return;

		foreach ( var node in customData )
		{
			if ( node is not JsonObject componentData )
				continue;

			var guid = componentData["SerializedGuid"]?.ToString();
			if ( string.IsNullOrWhiteSpace( guid ) )
				continue;

			var component = Scene.Components
				.GetAll<BaseCustomSerialize>()
				.FirstOrDefault( x => x.SerializedGuid == guid || x.Id.ToString() == guid );

			component?.CustomDeserialize( componentData );
		}
	}
}
#endif
