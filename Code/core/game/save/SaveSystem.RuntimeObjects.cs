#if IGNIS || STANDALONE
namespace Core;

using System;
using System.Text.Json.Nodes;

public sealed partial class SaveSystem
{
	private JsonArray CollectRuntimeObjectState()
	{
		var states = new JsonArray();

		foreach ( var gameObject in Scene.GetAllObjects( true ) )
		{
			if ( !ShouldSaveRuntimeObjectState( gameObject ) )
				continue;

			var state = new JsonObject
			{
				["Id"] = gameObject.Id.ToString(),
				["WorldTransform"] = Json.ToNode( gameObject.WorldTransform ),
				["Enabled"] = gameObject.Enabled
			};

			if ( gameObject.Components.Get<Rigidbody>() is { } rigidbody && rigidbody.IsValid() )
			{
				state["Velocity"] = Json.ToNode( rigidbody.Velocity );
				state["AngularVelocity"] = Json.ToNode( rigidbody.AngularVelocity );
				state["MotionEnabled"] = rigidbody.MotionEnabled;
			}

			states.Add( state );
		}

		return states;
	}

	private void RestoreRuntimeObjectState( JsonArray states )
	{
		if ( states is null )
			return;

		foreach ( var stateNode in states )
		{
			if ( stateNode is not JsonObject state )
				continue;

			if ( !Guid.TryParse( state["Id"]?.ToString(), out var id ) )
				continue;

			var gameObject = Scene.Directory.FindByGuid( id ) as GameObject;
			if ( !gameObject.IsValid() )
				continue;

			if ( state["WorldTransform"] is JsonNode transformNode )
				gameObject.WorldTransform = Json.FromNode<Transform>( transformNode );

			if ( state["Enabled"] is JsonNode enabledNode )
				gameObject.Enabled = enabledNode.GetValue<bool>();

			if ( gameObject.Components.Get<Rigidbody>() is not { } rigidbody || !rigidbody.IsValid() )
				continue;

			if ( state["MotionEnabled"] is JsonNode motionEnabledNode )
				rigidbody.MotionEnabled = motionEnabledNode.GetValue<bool>();

			rigidbody.Velocity = state["Velocity"] is JsonNode velocityNode
				? Json.FromNode<Vector3>( velocityNode )
				: Vector3.Zero;

			rigidbody.AngularVelocity = state["AngularVelocity"] is JsonNode angularVelocityNode
				? Json.FromNode<Vector3>( angularVelocityNode )
				: Vector3.Zero;
		}
	}

	private static bool ShouldSaveRuntimeObjectState( GameObject gameObject )
	{
		if ( !gameObject.IsValid() )
			return false;

		if ( HasSkippedAncestor( gameObject ) )
			return false;

		return gameObject.Components.Get<Rigidbody>().IsValid() ||
			   gameObject.Components.Get<GameProp>().IsValid() ||
			   gameObject.Components.Get<BaseItem>().IsValid();
	}

	private static bool HasSkippedAncestor( GameObject gameObject )
	{
		for ( var current = gameObject; current.IsValid(); current = current.Parent )
		{
			if ( current.Flags.Contains( GameObjectFlags.DontDestroyOnLoad ) ||
				 current.Flags.Contains( GameObjectFlags.NotSaved ) ||
				 current.Flags.Contains( GameObjectFlags.EditorOnly ) )
				return true;

			if ( current.Components.Get<BasePlayer>().IsValid() ||
				 //	 HasComponentNamedInSelf( current, "BaseGUIManager" ) ||
				 HasComponentNamedInSelf( current, "ScreenPanel" ) )
				return true;
		}

		return false;
	}

	private static bool HasComponentNamedInSelf( GameObject gameObject, string componentName )
	{
		foreach ( var component in gameObject.Components.GetAll() )
		{
			var type = component.GetType();
			if ( string.Equals( type.Name, componentName, StringComparison.Ordinal ) ||
				 string.Equals( type.FullName, componentName, StringComparison.Ordinal ) )
				return true;
		}

		return false;
	}
}
#endif
