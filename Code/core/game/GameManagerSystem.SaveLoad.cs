namespace Core;

using System;
using System.IO;
using System.Text.Json.Nodes;
public abstract partial class GameManagerSystem : GameObjectSystem
{
	[ConCmd( "restart" )]
	public static void RestartLevel()
	{
		BaseGUIManager.Local?.PrepareForSceneLoad();
		BaseGUIManager.Local?.SetLoadingOverlayScene( Game.ActiveScene.Source.ResourcePath );
		Game.ActiveScene.Load( Game.ActiveScene.Source );   // restart current map as if it was loaded again from scratch
	}

	[ConCmd( "reload" )]
	public static void ReloadLevel()   // load whatever latest save is
	{
		if ( Rules is SingleplayerRules sp && sp.CanSaveLoad )
		{
			if ( !string.IsNullOrEmpty( _lastSaveName ) )
				LoadGame( _lastSaveName );
			else
			{
				Log.Warning( "No save has been loaded or created this session. Restarting regularly." );
				RestartLevel();
			}
		}
		else Log.Warning( "Level reloading is not allowed!" );
	}

	[ConCmd( "scene" )] public static void CmdScene( string mapname, string parameter1 = "" ) => ChangeLevel( mapname, parameter1 );
	[ConCmd( "map" )]
	public static async void ChangeLevel( string mapname, string parameter1 = "" )
	{
		if ( !Rules.AllowMapChange ) return;

		if ( parameter1 == "transition" )
		{
			BaseGUIManager.Local?.SetLoadingOverlayScene( "scenes/" + mapname + ".scene" );
			if ( BaseGUIManager.Local is not null )
				await BaseGUIManager.Local.EnterLoadingOverlayAsync();

			Current.EnterLevelTransition( "scenes/" + mapname + ".scene" );
			return;
		}

		var map = GetScenePathless( mapname );

		if ( !map.IsValid() )
		{
			Log.Warning( $"Did not find the map {map}!" );
			return;
		}

		// Prepare the loading screen before we actually load
#if IGNIS || STANDALONE
		SaveSystem.RememberPrimarySceneSource( map );
#endif
		BaseGUIManager.Local?.SetLoadingOverlayScene( map );
		if ( BaseGUIManager.Local is not null )
			await BaseGUIManager.Local.EnterLoadingOverlayAsync();

		BaseGUIManager.Local?.PrepareForSceneLoad();
		var loadOptions = new SceneLoadOptions
		{
			ShowLoadingScreen = true
#if IGNIS
			,
			MinimumLoadingScreenSeconds = 3.0f
#endif
		};
		loadOptions.SetScene( map );
		Game.ActiveScene.Load( loadOptions );
#if IGNIS || STANDALONE
		Game.ActiveScene.GetSystem<SaveSystem>()?.SetPrimarySceneSource( map );
#endif
	}

	/// <summary>
	/// Get the SceneFile by any means necessary without requiring precise path
	/// </summary>
	/// <param name="mapname">Scene filename</param>
	/// <returns>The required SceneFile</returns>
	private static SceneFile GetScenePathless( string mapname ) => ResourceLibrary.Get<SceneFile>( mapname )
			?? ResourceLibrary.GetAll<SceneFile>().FirstOrDefault( x => string.Equals( x.ResourceName, mapname, StringComparison.OrdinalIgnoreCase ) );

#if IGNIS || STANDALONE
	[ConCmd( "save" )]
	public static void SaveGame( string savename = "" ) => TrySaveGame( savename, GetExplicitSaveTitle( savename ) );
#endif
	public static bool SaveGameFromMenu( string savename = "" ) => TrySaveGame( savename, null );

	// TODO: We need to do this better, this sucks, but it's best I can provide atm.
	private static void HandleSaveLoadInput()
	{
		if ( Input.Pressed( "quick_save" ) )
			QuickSave();

		if ( Input.Pressed( "quick_save" ) )
			QuickLoad();
	}

	private static bool TrySaveGame( string savename, string title )
	{
#if IGNIS || STANDALONE
		// We don't want to save in main menu
		if ( IsMenuScene() )
			return false;

		if ( Rules is SingleplayerRules sp && sp.CanSaveLoad )
		{
			if ( TryGetSaveSystem( out var saveSystem ) )
			{
				var path = string.IsNullOrWhiteSpace( savename )
					? GetNextManualSavePath()
					: SaveSystem.NormalizeSavePath( savename );

				SetSaveMetadata( saveSystem, "MANUAL SAVE", title );

				if ( saveSystem.Save( path ) )
				{
					_lastSaveName = path;
					return true;
				}
			}
		}
		else Log.Warning( "Saves are not allowed!" );
#endif
		return false;
	}

	[ConCmd( "quicksave" )]
	public static void QuickSave( int slot = 1 )
	{
#if IGNIS || STANDALONE
		if ( IsMenuScene() )
		{
			Log.Warning( "Quick saving is not allowed in menu scenes!" );
			return;
		}

		if ( Rules is not SingleplayerRules sp || !sp.CanSaveLoad )
		{
			Log.Warning( "Quick saving is not allowed!" );
			return;
		}

		if ( !TryGetSaveSystem( out var saveSystem ) )
			return;

		slot = Math.Clamp( slot, 1, 3 );
		var path = SaveSystem.NormalizeSavePath( $"{GetSaveFilePrefix()}-quick-{slot:00}" );

		SetSaveMetadata( saveSystem, "QUICK SAVE" );

		if ( saveSystem.Save( path ) )
			_lastSaveName = path;
#endif
	}

	[ConCmd( "quickload" )]
	public static void QuickLoad( int slot = 1 )
	{
		slot = Math.Clamp( slot, 1, 3 );
		LoadGame( $"{GetSaveFilePrefix()}-quick-{slot:00}" );
	}

	private static string _lastSaveName;

	[ConCmd( "load" )]
	public static async void LoadGame( string savename )
	{
		if ( CanLoadSave() )
		{
#if IGNIS
			if ( TryGetSaveSystem( out var saveSystem ) )
			{
				var path = SaveSystem.NormalizeSavePath( savename );
				_lastSaveName = path;
				if ( BaseGUIManager.Local is not null )
					await BaseGUIManager.Local.EnterLoadingOverlayAsync();

				BaseGUIManager.Local?.PrepareForSceneLoad();
				var loaded = await saveSystem.Load( path );

				if ( !loaded )
					BaseGUIManager.Local?.CancelLoadingOverlay();
			}
#endif
		}
		else Log.Warning( "Loading is not allowed!" );
	}

#if IGNIS || STANDALONE
	public static IReadOnlyList<SaveFileEntry> GetSaveFiles() => SaveSystem.GetSaveFiles();

	public static bool CanUseSaveSystem() => CanLoadSave() && TryGetSaveSystem( out _ );

	public static bool CanSaveGame() => !IsMenuScene() && Rules is SingleplayerRules sp && sp.CanSaveLoad && TryGetSaveSystem( out _ );

	public static bool DeleteSave( string savename ) => TryGetSaveSystem( out var saveSystem ) && saveSystem.Delete( savename );
#endif
	private static bool IsMenuScene() => Current?.SceneType == SceneType.Menu;
#if IGNIS || STANDALONE
	private static bool TryGetSaveSystem( out SaveSystem saveSystem )
	{
		saveSystem = Game.ActiveScene?.GetSystem<SaveSystem>();
		if ( saveSystem is not null )
			return true;

		Log.Warning( "SaveSystem is not present in the active scene. Add the SaveSystem game object system to scenes that support saving." );
		return false;
	}

	public static string GetNextManualSavePath()
	{
		var prefix = GetSaveFilePrefix();
		var index = 1;

		while ( FileSystem.Data.FileExists( $"{SaveSystem.SavesPath}/{prefix}-{index:0000}.sav" ) )
			index++;

		return SaveSystem.NormalizeSavePath( $"{prefix}-{index:0000}" );
	}
#endif
	private static string GetSaveFilePrefix()
	{
		var ident = Game.Ident?.Replace( "local.", "", StringComparison.OrdinalIgnoreCase ) ?? "save";
		var safe = new string( [.. ident
			.ToLowerInvariant()
			.Select( c => char.IsLetterOrDigit( c ) ? c : '-' )] );

		safe = safe.Trim( '-' );
		return string.IsNullOrWhiteSpace( safe ) ? "save" : safe;
	}
#if IGNIS || STANDALONE
	private static void SetSaveMetadata( SaveSystem saveSystem, string saveType, string title = null )
	{
		var sceneSource = Game.ActiveScene?.Source?.ResourcePath;
		var sceneTitle = Current.GetCurrentSaveDisplayTitle();

		saveSystem.SetMetadata( "Title", string.IsNullOrWhiteSpace( title ) ? sceneTitle : title );
		saveSystem.SetMetadata( "SceneTitle", sceneTitle );
		saveSystem.SetMetadata( "SaveType", saveType );
		saveSystem.SetMetadata( "SceneSource", sceneSource ?? "" );
	}

	private static string GetExplicitSaveTitle( string savename )
	{
		if ( string.IsNullOrWhiteSpace( savename ) )
			return null;

		var path = SaveSystem.NormalizeSavePath( savename );
		return Path.GetFileNameWithoutExtension( path );
	}
#endif
	protected virtual string GetCurrentSaveDisplayTitle()
	{
		var sceneSource = Game.ActiveScene?.Source;
		return GetSceneDisplayName( sceneSource );
	}

	protected static string GetSceneDisplayName( GameResource sceneSource )
	{
		if ( sceneSource.IsValid() )
			return Path.GetFileNameWithoutExtension( sceneSource.ResourceName ?? sceneSource.ResourcePath ) ?? "Unknown Scene";

		return "Unknown Scene";
	}

	private static bool CanLoadSave()
	{
		if ( Current?.SceneType == SceneType.Menu )
			return true;

		return Rules is SingleplayerRules sp && sp.CanSaveLoad;
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
			Game.ActiveScene.Components.GetAll<BaseCustomSerialize>().First( component => component.SerializedGuid == guid.ToString() ).CustomDeserialize( componentData.AsObject() );
		}
	}

	public void EnterLevelTransition( string targetmap )
	{
		if ( !Rules.CanTransition ) { Log.Warning( "Transitioning is not allowed!" ); return; }
		BaseGUIManager.Local?.SetLoadingOverlayScene( targetmap );

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

		BaseGUIManager.Local?.PrepareForSceneLoad();
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
