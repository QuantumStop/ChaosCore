using Core;

using System;
using System.Text.Json.Nodes;

/// The GameObjectSystem to automatically create everything is made per-game, check chaoscore for example

[Title( "Game Manager" )]
public class game_manager : BaseEntity, Component.ExecuteInEditor
{
	public static game_manager GameManager;

	public GameObject Player { get; set; }
	[Property, ReadOnly] Transform LastEditorCameraPosition { get; set; }
	[Property, ReadOnly, Range( 0f, 2f )] public float TimeScaleSlider { get; set; } = 1f;

	/// <summary>
	/// The thing you should set
	/// </summary>
	[Property, ReadOnly] public bool UsePlayerStart { get; set; }

	/// <summary>
	/// UsePlayerStart but with standalone check on top, set the above and check for the below
	/// </summary>
	/// <returns>Spawn with </returns>
	public bool UsePlayerStartRule => !Application.IsStandalone && UsePlayerStart;


	/// <summary>
	/// Use this if you dont want for player to spawn (menu scene, debug scene, etc)
	/// </summary>
	[Property] public bool DontSpawnPlayer { get; set; } = false;

	private float PreviousTimeScaleSlider { get; set; } = 1f;

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( Gizmo.CameraTransform.Position.LengthSquared > 0 )
			LastEditorCameraPosition = Gizmo.CameraTransform;
	}

	[ConCmd( "restart" )]
	public static void RestartLevel()   // restart current map as if it was loaded again from scratch
	{
		Game.ActiveScene.Load( Game.ActiveScene.Source );
	}

	[ConCmd( "reload" )]
	public static void ReloadLevel()   // load whatever latest save is
	{
		if ( LastSaveName != null )
			LoadGame( LastSaveName );
	}

	[ConVar( "host_timescale" ), Description( "Affects the scale of time, making things faster or slower." )] public static float TimeScale { get; set; } = 1f;
	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( PreviousTimeScaleSlider != TimeScaleSlider )
		{
			TimeScale = TimeScaleSlider;
			PreviousTimeScaleSlider = TimeScaleSlider;
		}
		else
		{
			TimeScaleSlider = TimeScale;
			PreviousTimeScaleSlider = TimeScaleSlider;
		}
		if ( Scene.IsEditor )
			Scene.TimeScale = 1f;
		else
			Scene.TimeScale = TimeScale;

		DrawAllDebugGizmos();
		ToggleXGUIDebug();
	}

	[ConCmd( "scene" )] public static void CmdScene( string mapname, string parameter1 = "" ) { ChangeLevel( mapname, parameter1 ); }
	[ConCmd( "map" )]
	public static void ChangeLevel( string mapname, string parameter1 = "" )
	{
		if ( parameter1 == "transition" )
		{
			GameManager.EnterLevelTransition( "scenes/" + mapname + ".scene" );
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
		FileSystem.Data.CreateDirectory( "saves" );
		GameManager.SerializeGameState( "saves/" + savename );
		LastSaveName = savename;
	}

	private static string LastSaveName;

	[ConCmd( "load" )]
	public static void LoadGame( string savename )
	{
		DeserializeGameState( "saves/" + savename );
	}


	[ConCmd( "npc_create" )]
	public static void NpcCreate( string npcname = "" )
	{
		if ( ResourceLibrary.Get<NpcDefinition>( "scripts/" + npcname + ".npc" ) == null )
		{
			foreach ( var npcdef in ResourceLibrary.GetAll<NpcDefinition>() )
				Log.Info( npcdef.ResourceName );
			return;
		}
		var tr = GameManager.Scene.Trace.Ray( new Ray( BasePlayer.Local.GetEyePos(), BasePlayer.Local.GetEyeAngles().Forward ), 200f ).Run();
		var npc = GameManager.Scene.CreateObject().Components.Create<BaseNpc>( false );
		npc.NpcDef = ResourceLibrary.Get<NpcDefinition>( "scripts/" + npcname + ".npc" );
		npc.RerollWeaponSlotsOnEnabled = true;
		npc.GameObject.WorldPosition = tr.EndPosition;
		npc.GameObject.WorldRotation = BasePlayer.Local.Controller.WorldRotation;
		npc.GameObject.Name = npcname;
		npc.Enabled = true;
	}


	[ConCmd( "ent_text" )]
	public static void EntText( string cmd )
	{
		if ( GameManager?.EntTextEntities == null )
			return;

		// Clear all on a specific command
		if ( cmd.Equals( "!ClearAll", StringComparison.OrdinalIgnoreCase ) )
		{
			foreach ( var ent in GameManager.EntTextEntities )
			{
				if ( GameManager.textRenderersCache.TryGetValue( ent, out var renderer ) )
				{
					var debugGO = renderer.GameObject;
					if ( debugGO?.IsValid() == true )
						debugGO.Destroy();
					GameManager.textRenderersCache.Remove( ent );
				}
			}

			GameManager.EntTextEntities.Clear();
			Log.Info( "[ent_text] Cleared all tracked entities." );
			return;
		}

		// Player initialized trace to get the entity we're looking at
		if ( cmd.Equals( "!picker", StringComparison.OrdinalIgnoreCase ) )
		{
			var ray = BasePlayer.Local?.Controller?.AimRay;
			if ( ray == null )
			{
				Log.Warning( "[ent_text] Could not retrieve AimRay." );
				return;
			}

			const float maxDistance = 2048.0f;

			var trace = Game.ActiveScene.Trace.Ray( ray.Value.Position, ray.Value.Position + ray.Value.Forward * maxDistance )
				.WithAnyTags( "solid" )
				.UseHitboxes( true )
				.UseRenderMeshes( true )
				.UsePhysicsWorld( true )
				.WithoutTags( "player" )
				.Run();

			// Sphere trace fallback
			if ( !trace.Hit || trace.GameObject == null )
			{
				trace = Game.ActiveScene.Trace.Sphere( 6.0f, ray.Value.Position, ray.Value.Position + ray.Value.Forward * maxDistance )
					.WithAnyTags( "solid" )
					.UseHitboxes( false )
					.UsePhysicsWorld( true )
					.WithoutTags( "player" )
					.Run();
			}

			if ( !trace.Hit || trace.GameObject == null )
			{
				Log.Warning( "[ent_text] No object hit by AimRay." );
				return;
			}

			var hitEntity = trace.GameObject.Components.Get<BaseEntity>( true );
			if ( hitEntity == null )
			{
				Log.Warning( $"[ent_text] Hit object [{trace.GameObject}] has no BaseEntity component." );
				return;
			}

			cmd = hitEntity.TargetName ?? hitEntity.GameObject?.Name ?? hitEntity.GetType().Name;
		}

		var allEntities = Game.ActiveScene?.GetAllComponents<BaseEntity>();
		if ( allEntities == null )
		{
			Log.Warning( "[ent_text] No entities found — ActiveScene is null or empty." );
			return;
		}

		List<BaseEntity> matches = allEntities.Where( ent =>
			ent != null && (
				string.Equals( ent.TargetName, cmd, StringComparison.OrdinalIgnoreCase ) ||
				string.Equals( ent.GameObject?.Name, cmd, StringComparison.OrdinalIgnoreCase ) ||
				string.Equals( ent.GetType().Name, cmd, StringComparison.OrdinalIgnoreCase )
			)
		).ToList();

		if ( matches.Count == 0 )
		{
			Log.Warning( $"[ent_text] No entities found matching: \"{cmd}\"" );
			return;
		}

		int added = 0; int removed = 0;

		foreach ( var ent in matches )
		{
			if ( GameManager.EntTextEntities.Contains( ent ) )
			{
				GameManager.EntTextEntities.Remove( ent );

				if ( GameManager.textRenderersCache.TryGetValue( ent, out var existingRenderer ) )
				{
					var debugGO = existingRenderer.GameObject;
					if ( debugGO?.IsValid() == true )
						debugGO.Destroy();

					GameManager.textRenderersCache.Remove( ent );
				}

				removed++;
				Log.Info( $"[ent_text] Removed: {ent.TargetName ?? "null"} ({ent.GetType().Name})" );
			}
			else
			{
				GameManager.EntTextEntities.Add( ent );

				// Create child GameObject for TextRenderer
				GameObject debugGO = new()
				{
					Name = "ent_text_debug_" + ent.TargetName ?? ent.GetType().Name,
					Flags = GameObjectFlags.Hidden
				};

				debugGO.SetParent( ent.GameObject );
				debugGO.WorldPosition = ent.GameObject.WorldPosition;
				debugGO.WorldRotation = ent.GameObject.WorldRotation;

				TextRenderer textRenderer = debugGO.AddComponent<TextRenderer>();

				textRenderer.Scale = 0.05f;
				textRenderer.Color = Color.White;
				textRenderer.FontFamily = "Courier New";
				textRenderer.FontWeight = 100;
				//	textRenderer.TextAlignment = TextRenderer.TextAlign.Left;

				TextRendering.Scope scope = textRenderer.TextScope;

				scope.FontSize = 15;
				scope.OutlineUnder.Enabled = true;
				scope.OutlineUnder.Size = 4;
				scope.OutlineUnder.Color = Color.Black;

				textRenderer.TextScope = scope;

				textRenderer.RenderOptions.Game = false;
				textRenderer.RenderOptions.AfterUI = true;

				GameManager.textRenderersCache[ent] = textRenderer;

				added++;
				Log.Info( $"[ent_text] Added: {ent.TargetName ?? "null"} ({ent.GetType().Name})" );
			}
		}

		Log.Info( $"[ent_text] Added {added}, Removed {removed}. Now tracking {GameManager.EntTextEntities.Count} entities." );
	}

	protected override void OnStart() { GameManager = this; }   // instance is not set in editor otherwise

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Scene.IsEditor )
			return;

		//		spawn all our managers
		Components.GetOrCreate<NpcSoundManager>();

		Scene.CreateObject().Components.GetOrCreate<DebrisManager>();

		//		reset timescale
		TimeScaleSlider = 1f;
		PreviousTimeScaleSlider = 1f;
		TimeScale = 1f;

		//		check if this is a level transition
		ExitLevelTransition();

		if ( !DontSpawnPlayer )
		{

			//	if we have a player in the world use that
			if ( Scene.Components.GetAll<BasePlayer>().Any() )
			{
				Player = Scene.Components.GetAll<BasePlayer>().First().GameObject;
				return;
			}

			// if we have info_player_starts pick the active one or the one closest to the camera
			if ( Scene.Components.GetAll<info_player_start>().Any() && UsePlayerStartRule )
			{
				info_player_start closest = Scene.Components.GetAll<info_player_start>().First();

				foreach ( var playerstart in Scene.Components.GetAll<info_player_start>() )
				{
					if ( playerstart.Primary )
					{
						closest = playerstart;
						break;
					}
					if ( closest == null || playerstart.WorldPosition.DistanceSquared( LastEditorCameraPosition.Position ) < closest.WorldPosition.DistanceSquared( LastEditorCameraPosition.Position ) )
						closest = playerstart;
				}
				if ( closest is not null )
				{
					Player = Scene.GetPrefab( "prefabs/player.prefab" ).Clone();

					Player.WorldPosition = closest.WorldPosition;
					Player.WorldRotation = closest.WorldRotation;

					if ( Player.Components.TryGet<BasePlayer>( out var playerComponent ) )
					{
						playerComponent.Controller.EyeAngles = closest.WorldRotation;
						playerComponent.Controller.Controller.Velocity = Vector3.Zero;
						playerComponent.Controller.Controller.BaseVelocity = Vector3.Zero;
					}
					closest.OnPlayerSpawned?.DynamicInvoke( Player );
					closest.OnPlayerSpawnedInternal();

					if ( closest.RemoveOnLevelLoad )
					{
						if ( closest.Components.Count == 1 )
							closest.GameObject.Destroy();
						else
							closest.Destroy();
					}

					return;
				}
			}
			else
			{
				//	otherwise spawn player at editor camera position
				SceneTraceResult tr = Scene.Trace.Ray( LastEditorCameraPosition.Position, LastEditorCameraPosition.Position - Vector3.Up * 64f ).Run();

				Player = Scene.GetPrefab( "prefabs/player.prefab" ).Clone();

				// if this is standalone there is no editor camera, so spawn at 0 0 0 (sucks but better than spawning in a random spot)
				Player.WorldPosition = !Application.IsStandalone ? LastEditorCameraPosition.Position - Vector3.Up * 64f * tr.Fraction : Vector3.Zero;
				Player.WorldRotation = !Application.IsStandalone ? LastEditorCameraPosition.Rotation.Angles().WithPitch( 0 ).WithRoll( 0 ).ToRotation() : Angles.Zero;

				if ( Player.Components.TryGet<BasePlayer>( out var playerComponent2 ) )
				{
					playerComponent2.Controller.EyeAngles = !Application.IsStandalone ? LastEditorCameraPosition.Rotation : Angles.Zero;
					playerComponent2.Controller.Controller.Velocity = Vector3.Zero;
					playerComponent2.Controller.Controller.BaseVelocity = Vector3.Zero;
				}
			}


			foreach ( var item in Components.GetAll<BaseItem>() )
				item.PickUp = true;
		}
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

	/// <summary>
	/// Internal function to collect all types of gizmo draws and put it OnUpdate()
	/// </summary>
	private void DrawAllDebugGizmos()
	{
#if STANDALONE

		UpdateEntityText();
#endif
	}

	/// <summary>
	/// Internal function to collect all types of visual debug, such as console and xgui master debug.
	/// </summary>
	private static void ToggleXGUIDebug()
	{
		if ( Input.Pressed( "Console_Toggle" ) || Input.Pressed( "console" ) )
		{
			//Log.Info( "Processed" );
			XGUI_DebugMaster_Manager.Local.ToggleConsole();
		}

		if ( Input.Pressed( "MasterDebug_Toggle" ) )
		{
			XGUI_DebugMaster_Manager.Local.ToggleMasterDebug();
		}
	}


	[Property, ReadOnly] private List<BaseEntity> EntTextEntities { get; set; } = new();
	private Dictionary<BaseEntity, TextRenderer> textRenderersCache = [];//  Caching TeDictionaryxtRenderer components for each entity
	private record EntTextParams( BaseEntity Entity, string DisplayText, Color Color, float Size = 32f );

#if STANDALONE

	/// <summary>
	/// Draws EntText debug in-game for all selected entities.
	/// </summary>
	private void UpdateEntityText()
	{
		// Skip if there are no entities to display or the game isn't running
		if ( GameManager?.EntTextEntities == null || GameManager.EntTextEntities.Count == 0 || !Game.IsPlaying )
			return;

		// Ensure TextRenderers are up-to-date in the cache
		foreach ( var ent in GameManager.EntTextEntities )
		{
			if ( !textRenderersCache.ContainsKey( ent ) )
			{
				var textRenderer = ent.GameObject?.GetComponent<TextRenderer>();
				if ( textRenderer != null )
					textRenderersCache[ent] = textRenderer;
			}
		}

		foreach ( var ent in GameManager.EntTextEntities.ToList() )
		{
			// First things first lets super safe guard it from objects become destroyed/picked up/killed
			if ( ent == null || ent.GameObject?.IsValid() != true )
			{
				GameManager.EntTextEntities.Remove( ent );
				textRenderersCache.Remove( ent );
				continue;
			}

			if ( !textRenderersCache.TryGetValue( ent, out var textRenderer ) || textRenderer == null || textRenderer.IsValid != true )
				continue;


			var go = ent.GameObject;
			if ( go == null || !go.IsValid() )
				continue;

			var pos = ent.GameObject.WorldPosition;
			var camPos = Scene.Camera?.WorldPosition ?? Vector3.Zero;

			float distance = Vector3.DistanceBetween( camPos, pos );
			if ( distance > 3000.0f )
				continue; // Skip very far

			// Build the display text using a StringBuilder
			var sb = new System.Text.StringBuilder();

			sb.AppendLine( $"Name: {ent?.TargetName ?? ent?.GetType().Name} ({ent?.GetType().Name})" );
			sb.AppendLine( $"Position: {ent?.GameObject?.WorldPosition.x:000.0}, {ent?.GameObject?.WorldPosition.y:000.0}, {ent?.GameObject?.WorldPosition.z:000.0}" );

			var grouped = ent?.GetDebugProperties().GroupBy( e => e.Group );

			foreach ( var group in grouped )
			{
				if ( !string.IsNullOrWhiteSpace( group?.Key ) )
					sb.AppendLine( $"\n[{group?.Key}]" );

				foreach ( var entry in group )
					sb.AppendLine( $"{entry?.Label}: {entry?.Value}" );
			}

			string newText = sb?.ToString();
			if ( textRenderer.Text != newText )
				textRenderer.Text = newText;

			Vector3 toCamera = camPos - pos;
			toCamera.z = 0;

			Rotation rot = Rotation.LookAt( -toCamera, Vector3.Up );
			textRenderer.WorldRotation = rot;

			textRenderer.Scale = Math.Clamp( distance / 800.0f, 0.005f, 0.5f );
		}
	}

#endif

}
