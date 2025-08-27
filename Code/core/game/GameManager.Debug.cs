namespace Core;
using System;

public partial class GameManager
{
	// maybe move to its own misc type thing
	[ConCmd( "npc_create" )]
	public static void NpcCreate( string npcname = "" )
	{
		if ( ResourceLibrary.Get<NpcDefinition>( "scripts/" + npcname + ".npc" ) == null )
		{
			foreach ( var npcdef in ResourceLibrary.GetAll<NpcDefinition>() )
				Log.Info( npcdef.ResourceName );
			return;
		}
		var tr = Instance.Scene.Trace.Ray( new Ray( BasePlayer.Local.GetEyePos(), BasePlayer.Local.GetEyeAngles().Forward ), 200f ).Run();
		var npc = Instance.Scene.CreateObject().Components.Create<BaseNpc>( false );
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
		if ( Instance?.EntTextEntities == null )
			return;

		// Clear all on a specific command
		if ( cmd.Equals( "!ClearAll", StringComparison.OrdinalIgnoreCase ) )
		{
			foreach ( var ent in Instance.EntTextEntities )
			{
				if ( Instance.textRenderersCache.TryGetValue( ent, out var renderer ) )
				{
					var debugGO = renderer.GameObject;
					if ( debugGO?.IsValid() == true )
						debugGO.Destroy();
					Instance.textRenderersCache.Remove( ent );
				}
			}

			Instance.EntTextEntities.Clear();
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
			if ( Instance.EntTextEntities.Contains( ent ) )
			{
				Instance.EntTextEntities.Remove( ent );

				if ( Instance.textRenderersCache.TryGetValue( ent, out var existingRenderer ) )
				{
					var debugGO = existingRenderer.GameObject;
					if ( debugGO?.IsValid() == true )
						debugGO.Destroy();

					Instance.textRenderersCache.Remove( ent );
				}

				removed++;
				Log.Info( $"[ent_text] Removed: {ent.TargetName ?? "null"} ({ent.GetType().Name})" );
			}
			else
			{
				Instance.EntTextEntities.Add( ent );

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

				Instance.textRenderersCache[ent] = textRenderer;

				added++;
				Log.Info( $"[ent_text] Added: {ent.TargetName ?? "null"} ({ent.GetType().Name})" );
			}
		}

		Log.Info( $"[ent_text] Added {added}, Removed {removed}. Now tracking {Instance.EntTextEntities.Count} entities." );
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
		if ( Instance?.EntTextEntities == null || Instance.EntTextEntities.Count == 0 || !Game.IsPlaying )
			return;

		// Ensure TextRenderers are up-to-date in the cache
		foreach ( var ent in Instance.EntTextEntities )
		{
			if ( !textRenderersCache.ContainsKey( ent ) )
			{
				var textRenderer = ent.GameObject?.GetComponent<TextRenderer>();
				if ( textRenderer != null )
					textRenderersCache[ent] = textRenderer;
			}
		}

		foreach ( var ent in Instance.EntTextEntities.ToList() )
		{
			// First things first lets super safe guard it from objects become destroyed/picked up/killed
			if ( ent == null || ent.GameObject?.IsValid() != true )
			{
				Instance.EntTextEntities.Remove( ent );
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
