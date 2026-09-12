namespace Core;

using System;
using System.Text.Json.Nodes;

public abstract partial class GameManagerSystem
{
	private void ResolveSceneType()
	{
		if ( Scene.Source is not SceneFile sceneFile )
			return;

		if ( TryGetAuthoredSceneType( sceneFile, out var sceneType ) )
		{
			SceneType = sceneType;
			return;
		}

		var tags = sceneFile.GetMetadata( "Tags", string.Empty );
		SceneType = HasSceneTag( tags, "menu_scene" )
			? SceneType.Menu
			: HasSceneTag( tags, "debug_scene" )
				? SceneType.Debug
				: SceneType.Game;
	}

	private bool TryGetAuthoredSceneType( SceneFile sceneFile, out SceneType sceneType )
	{
		sceneType = default;

		if ( sceneFile.SceneProperties?["GameObjectSystems"] is not JsonObject systems )
			return false;

		var systemName = GetType().FullName;
		if ( string.IsNullOrEmpty( systemName ) || systems[systemName] is not JsonObject properties )
			return false;

		if ( properties["SceneType"] is not JsonValue value )
			return false;

		if ( value.TryGetValue<string>( out var name )
			&& Enum.TryParse( name, true, out sceneType )
			&& Enum.IsDefined( typeof(SceneType), sceneType ) )
			return true;

		if ( !value.TryGetValue<int>( out var number ) || !Enum.IsDefined( typeof(SceneType), number ) )
			return false;

		sceneType = (SceneType)number;
		return true;
	}

	private static bool HasSceneTag( string tags, string expected )
	{
		if ( string.IsNullOrWhiteSpace( tags ) )
			return false;

		foreach ( var tag in tags.Split( [',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries ) )
		{
			if ( string.Equals( tag, expected, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false;
	}
}
