namespace Core;
using System;
using System.Text.Json.Nodes;
public partial class GameManager
{
	[ConCmd( "restart", ConVarFlags.Replicated )]
	public static void RestartLevel()   // restart current map as if it was loaded again from scratch
	{
		Game.ActiveScene.Load( Game.ActiveScene.Source );
	}

	[ConCmd( "reload", ConVarFlags.Replicated )]
	public static void ReloadLevel()   // load whatever latest save is
	{
		if ( Rules is SingleplayerRules sp && sp.CanSaveLoad )
		{
			if ( LastSaveName != null )
				LoadGame( LastSaveName );
		}
		else Log.Warning( "Level reloading is not allowed!" );
	}

	[ConCmd( "scene" )] public static void CmdScene( string mapname, string parameter1 = "" ) { ChangeLevel( mapname, parameter1 ); }
	[ConCmd( "map" )]
	public static void ChangeLevel( string mapname, string parameter1 = "" )
	{
		if ( parameter1 == "transition" )
		{
			Instance.EnterLevelTransition( "scenes/" + mapname + ".scene" );
			return;
		}

		Game.ActiveScene.Load( GetScenePathless( mapname ) );
	}

	/// <summary>
	/// Get the SceneFile by any means necessary without requiring precise path
	/// </summary>
	/// <param name="mapname">Scene filename</param>
	/// <returns>The required SceneFile</returns>
	private static SceneFile GetScenePathless( string mapname )
	{
		return ResourceLibrary.Get<SceneFile>( mapname ) ?? ResourceLibrary.GetAll<SceneFile>().FirstOrDefault( ( SceneFile x ) => string.Equals( x.ResourceName, mapname, StringComparison.OrdinalIgnoreCase ) );
	}

	[ConCmd( "save" )]
	public static void SaveGame( string savename )
	{
		if ( Rules is SingleplayerRules sp && sp.CanSaveLoad )
		{
			FileSystem.Data.CreateDirectory( "saves" );
			Instance.SerializeGameState( "saves/" + savename );
			LastSaveName = savename;
		}
		else Log.Warning( "Saves are not allowed!" );
	}

	private static string LastSaveName;

	[ConCmd( "load" )]
	public static void LoadGame( string savename )
	{
		if ( Rules is SingleplayerRules sp && sp.CanSaveLoad )
			DeserializeGameState( "saves/" + savename );
		else Log.Warning( "Loading is not allowed!" );
	}

	public void SerializeGameState( string filename )
	{
		Log.Info( "saving game to " + filename + ".save" );

		//		get any custom json data from individual components
		JsonArray customdata = [];

		foreach ( var component in Scene.GetAllComponents<BaseCustomSerialize>() )
			customdata.Add( component.CustomSerialize() );

		JsonObject data = new()
		{
			{"Type", "game_save"},
			{"SceneObject", Scene.Serialize()},
			{"CustomComponentData", customdata}
		};

		FileSystem.Data.WriteJson( filename + ".save", data );
	}
	public static void DeserializeGameState( string filename )
	{
		//		make sure the file exists

		if ( !FileSystem.Data.FileExists( filename + ".save" ) )
		{
			Log.Info( "save file " + filename + ".save does not exist" );
			return;
		}

		Log.Info( "loading game from " + filename + ".save" );
		JsonObject data = FileSystem.Data.ReadJson<JsonObject>( filename + ".save" );

		//		validate
		data.TryGetPropertyValue( "Type", out JsonNode read );

		if ( read.ToString() != "game_save" )
			return;

		//		load
		data.TryGetPropertyValue( "SceneObject", out read );
		Game.ActiveScene.Deserialize( read.AsObject() );

		//		apply custom data
		data.TryGetPropertyValue( "CustomComponentData", out read );
		var objects = read.AsArray();

		foreach ( var componentData in objects )
		{
			componentData.AsObject().TryGetPropertyValue( "SerializedGuid", out JsonNode guid );
			//			find the object its talking about and load on it
			Game.ActiveScene.Components.GetAll<BaseCustomSerialize>().Where( component => component.SerializedGuid == guid.ToString() ).First().CustomDeserialize( componentData.AsObject() );
		}
	}

	public void EnterLevelTransition( string targetmap )
	{
		if ( !Rules.CanTransition ) { Log.Warning( "Transitioning is not allowed!" ); return; }

		//		save all the stuff we want to keep into a temporary json file, to be loaded by the new maps game_manager
		JsonArray holdovers = [];
		foreach ( var gameobject in Scene.Children )
		{
			if ( !gameobject.Tags.Has( "allow_to_transition" ) )
				continue;

			holdovers.Add( gameobject.Serialize() );
		}

		//		and also get any custom json data from individual components
		JsonArray customdata = [];
		foreach ( var component in Scene.GetAllComponents<BaseCustomSerialize>() )
		{
			if ( !component.Tags.Has( "allow_to_transition" ) )
				continue;

			customdata.Add( component.CustomSerialize() );
		}

		JsonObject data = new()
		{
			{"Type", "temp__level_transition"},
			{"PreviousMap", "scenes/"+Game.ActiveScene.Source.ResourcePath+".scene"}, // this will be null on the initial scene in editor, but fine afterwards
			{"TargetMap", targetmap},
			{"GameObjects", holdovers},
			{"CustomComponentData", customdata}
		};

		FileSystem.Data.WriteJson( "temp__level_transition.save", data );

		//	switch scene without the loading screen
		var loadOptions = new SceneLoadOptions();
		loadOptions.ShowLoadingScreen = false;
		loadOptions.SetScene( targetmap );

		Game.ActiveScene.Load( loadOptions );
	}

	public void ExitLevelTransition()
	{
		if ( !Rules.CanTransition ) { Log.Warning( "Un-transitioning is not allowed either!" ); return; }

		//		see if we have a valid file to load from
		if ( !FileSystem.Data.FileExists( "temp__level_transition.save" ) )
			return;

		JsonObject data = FileSystem.Data.ReadJson<JsonObject>( "temp__level_transition.save" );
		FileSystem.Data.OpenWrite( "debug__last_level_transition.save" ).Write( FileSystem.Data.ReadAllBytes( "temp__level_transition.save" ) );
		FileSystem.Data.DeleteFile( "temp__level_transition.save" );

		//		validate
		data.TryGetPropertyValue( "Type", out JsonNode read );
		if ( read.ToString() != "temp__level_transition" )
			return;

		//		TODO: check if TargetMap is the same as this map
		//		load
		data.TryGetPropertyValue( "PreviousMap", out read );
		Log.Info( "Loading objects from scene " + read.ToString() );
		data.TryGetPropertyValue( "GameObjects", out read );

		var objects = read.AsArray();
		foreach ( var gameobject in objects ) Scene.CreateObject().Deserialize( gameobject.AsObject() );

		//		apply custom data
		data.TryGetPropertyValue( "CustomComponentData", out read );
		objects = read.AsArray();

		foreach ( var componentData in objects )
		{
			componentData.AsObject().TryGetPropertyValue( "__guid", out JsonNode guid );
			var component = (BaseCustomSerialize)Scene.Directory.FindComponentByGuid( Guid.Parse( guid.ToString() ) );
			component.CustomDeserialize( componentData.AsObject() );
		}
	}
}
