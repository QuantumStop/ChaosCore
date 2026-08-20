namespace Core;

using System;
using AI;

public abstract partial class GameManagerSystem : GameObjectSystem
{
#if IGNIS
	[ConVar( "mat_fullbright" )]
	public static SceneCameraDebugMode Fullbright
	{
		get => field = Current.Scene.Camera.DebugMode;
		set
		{
			if ( value == field || !Current.Scene.IsValid() ) return;

			Current.Scene.Camera.DebugMode = value;
		}
	}

	private static readonly Model _entCameraPreviewModel = Model.Load( "models/editor/camera.vmdl" );

	public static bool IsGameEjected
	{
		get
		{
			if ( !Application.IsEditor )
				return false;

			// Tooling/resource reloads can temporarily invalidate editor/session references while playing.
			return (Game.ActiveScene?.Editor?.IsGameEjected ?? false)
				|| (Current?.Scene?.Editor?.IsGameEjected ?? false);
		}
	}

	[Property] public bool ShowEjectCamera { get; set; } = false;

	[ConVar( "debug_showejectcamera" )]
	public static bool DebugShowEjectCamera
	{
		get => Current?.ShowEjectCamera ?? false;
		set => Current?.ShowEjectCamera = value;
	}

	[ConCmd( "ent_text" )]
	public static void EntText( string cmd = "!picker" )
	{
		var scene = Game.ActiveScene;
		var system = DebugOverlaySystem.Current;

		if ( !scene.IsValid() || system is null || system.Scene != scene )
		{
			Log.Warning( "[ent_text] DebugOverlaySystem not available." );
			return;
		}

		//
		// Clear All
		//
		if ( cmd.Equals( "!clearall", StringComparison.OrdinalIgnoreCase ) )
		{
			DebugOverlaySystem.Current.ClearAllEntries();

			Log.Info( "[ent_text] Cleared all debug overlays." );
			return;
		}
		//
		// Picker Mode
		//
		if ( cmd.Equals( "!picker", StringComparison.OrdinalIgnoreCase ) )
		{
			var camera = scene.Camera;
			if ( IsGameEjected )
				camera = Application.Editor.Camera;
			if ( !camera.IsValid() )
			{
				Log.Warning( "[ent_text] No active scene camera." );
				return;
			}

			const float maxDistance = 2048f;

			var start = camera.WorldPosition;
			var end = start + camera.WorldRotation.Forward * maxDistance;

			var trace = scene.Trace.Ray( start, end )
				.WithAnyTags( "solid" )
				.UseHitboxes( true )
				.UsePhysicsWorld( true )
				.WithoutTags( "player" )
				.Run();

			if ( !trace.Hit || !trace.GameObject.IsValid() )
				trace = scene.Trace.Sphere( 6f, start, end )
					.WithAnyTags( "solid" )
					.UsePhysicsWorld( true )
					.WithoutTags( "player" )
					.Run();

			if ( !trace.Hit || !trace.GameObject.IsValid() )
			{
				Log.Warning( "[ent_text] Picker trace failed." );
				return;
			}

			Component hitComponent = trace.GameObject.Components.Get<Component>( true );

			if ( !hitComponent.IsValid() || !DebugExposeMetadata.Get( hitComponent.GetType() ).HasMembers )
				return;

			// This will toggle the debug for the hit component if it has DebugExpose entries.
			// If the debug is running it'll toggle to dispose it, if not it will enable it otherwise.
			bool createdEntry = hitComponent.DebugOverlay.ScreenTextOverlay( hitComponent );

			Log.Info(
				$"[ent_text] {(createdEntry ? "Added" : "Removed")} debug overlay on {hitComponent.GetType().Name}"
			);

			return;
		}

		//
		// Name / Type Match Mode
		//
		int changedCount = 0;

		// This should be just components, but for now I'm doing just BaseEntity
		var matches = scene.GetAllComponents<BaseEntity>()
		.Where( comp =>
		{
			if ( !comp.IsValid() )
				return false;

			var cmdLower = cmd.ToLowerInvariant();

			var typeName = comp.GetType().Name.ToLowerInvariant();
			var gameName = comp.GameObject?.Name?.ToLowerInvariant();

			return
				typeName.Contains( cmdLower ) ||
				(gameName is not null && gameName.Contains( cmdLower ));
		} );

		Log.Info(
			$"[ent_text] Search '{cmd}' found {matches.Count<Component>()} candidates."
		);

		foreach ( var comp in matches )
		{
			Log.Info( $"[ent_text] Candidate: Type:{comp.GetType().Name} | Name:{comp.GameObject?.Name}" );

			bool removed = DebugOverlaySystem.Current.RemoveWhere( so =>
				so is DebugTextSceneObject d &&
				d.component == comp
			) > 0;

			if ( !removed )
				comp.DebugOverlay.ScreenTextOverlay( comp );

			changedCount++;
		}

		Log.Info(
			$"[ent_text] Toggled overlay text debug for {changedCount} entities."
		);
	}
#endif
	/// <summary>
	/// Internal function to collect all types of gizmo draws or debug overlays and put it OnUpdate()
	/// </summary>
	private void DrawAllDebugGizmos()
	{
#if !STANDALONE && IGNIS
		DrawEjectCameraDebug();
		//	VoxelDebugQueue.DrawAll( Time.Delta );
#endif
		DebugConvars();
	}

#if IGNIS
	private void DrawEjectCameraDebug()
	{
		if ( Scene.IsLoading || !Application.IsEditor )
			return;

		if ( !ShowEjectCamera || IsGameEjected )
			return;

		if ( TryGetLatestEditorCameraTransform( out var editorCam ) )
			DrawCameraDebugPreview( editorCam, Color.Cyan, 25.0f );
	}
#endif
	private bool TryGetLatestEditorCameraTransform( out Transform transform )
	{
		transform = default;
		if ( !Application.IsEditor || !Scene.IsValid() )
			return false;

		var latestEditorCamera = Scene.GetAllObjects( true ).LastOrDefault( x => x?.Name == "editor_camera" );
		if ( latestEditorCamera?.IsValid() == true )
		{
			transform = latestEditorCamera.Transform.World;
			return true;
		}

		// Fallback cache from OnEditorUpdate in case lookup fails for a frame.
		if ( LastEditorCameraPosition.Position.LengthSquared > 0.001f )
		{
			transform = LastEditorCameraPosition;
			return true;
		}

		return false;
	}
#if IGNIS
	/// <summary>
	/// Draws a simple camera preview gizmo at the given transform with a forward line indicating direction.
	/// </summary> <param name="transform">The transform to draw the camera preview at.</param> 
	/// <param name="color">The color to use for the gizmo.</param> 
	/// <param name="forwardLineLength">The length of the forward direction line.</param>
	private static void DrawCameraDebugPreview( Transform transform, Color color, float forwardLineLength = 56.0f )
	{
		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.Color = color;

		if ( _entCameraPreviewModel.IsValid() )
			Gizmo.Draw.Model( _entCameraPreviewModel, transform );

		Gizmo.Draw.Color = color;
		Gizmo.Draw.LineThickness = 3f;
		Gizmo.Draw.Arrow( transform.Position + transform.Forward * 10.5f, transform.Position + transform.Rotation.Forward * forwardLineLength, 4f, 1f );
	}
#endif
	[ConVar( "cl_showpos", Help = "Show player position and rotation debug." )] public static bool ShowPos { get; set; } = false;
	[ConVar( "cl_show_worldtime", Help = "Show player position and rotation debug." )] public static bool ShowWorldTime { get; set; } = false;
	[ConVar( "cl_show_savesystem", Help = "Show save system debug." )] public static bool ShowSaveSystem { get; set; } = false;

	/// <summary>
	/// Spacing between blocks
	/// </summary>
	private const float _debugSpacing = 32f;

	/// <summary>
	/// Handles rendering of debug convars like cl_showpos
	/// </summary>
	private static void DebugConvars()
	{
		Vector2 pos = new( 32, 32 );

		if ( Application.IsStandalone )
		{
			StandaloneVersion( pos );
		}

		if ( ShowPos )
		{
			PlayerPos( ref pos );
			pos.y += _debugSpacing;
		}

#if IGNIS || STANDALONE
		if ( ShowSaveSystem )
		{
			DrawSaveSystem( ref pos );
			pos.y += _debugSpacing;
		}
#endif
		if ( ShowWorldTime )
		{
			WorldTime( ref pos );
			pos.y += _debugSpacing;
		}

		if ( BasePlayer.Local.IsValid() && BasePlayer.Local.Controller.IsValid() && BasePlayer.Local.Controller.IsNoclipping )
		{
			Noclip( ref pos );
			pos.y += _debugSpacing;
		}

		if ( BasePlayer.God || BasePlayer.Buddha )
		{
			GodBuddha( ref pos );
			pos.y += _debugSpacing;
		}

		if ( AIController.NoTarget )
		{
			Notarget( ref pos );
			pos.y += _debugSpacing;
		}

		if ( AIManager.AIDisable ) AIDisable( new( Screen.Width * 0.7f, Screen.Height * 0.8f ) );
	}

	private static void WorldTime( ref Vector2 pos )
	{
		var scope = InitDebugTextScope();

		var x = pos.x;
		var y = pos.y;

		DrawWorldTimeLine( ref y, "Time.Now", Time.Now, Color.White );
		DrawWorldTimeLine( ref y, "WorldTime.Now", Core.WorldTime.Now, Color.Green );
		DrawWorldTimeLine( ref y, "WorldTime.Delta", Core.WorldTime.Delta, Color.Orange );
		DrawWorldTimeLine( ref y, "WorldTime.Offset", Current.WorldTimeOffset, Color.Cyan );

		pos.y = y;

		void DrawWorldTimeLine( ref float lineY, string label, float value, Color color )
		{
			scope.Text = $"{label,-16} {value,10:0.000}";
			scope.TextColor = color;
			DebugOverlaySystem.Current.ScreenText( new Vector2( -x, lineY ), scope, TextFlag.Right, 0f );
			lineY += 14;
		}
	}
#if IGNIS || STANDALONE
	private static void DrawSaveSystem( ref Vector2 pos )
	{
		var saveSystem = SaveSystem.Current;
		var scope = InitDebugTextScope();

		var x = pos.x;
		var y = pos.y;
		const float labelXOffset = 85f;
		const float tightLabelXOffset = 55f;

		var headerStatus = GetSaveSystemHeaderStatus( saveSystem );
		DrawSaveSystemHeadLine( ref y, "SaveSystem", headerStatus.Text, headerStatus.Color );

		if ( saveSystem is not null )
			DrawSaveSystemValueLine( ref y, ShortenDebugPath( saveSystem.PrimarySceneSource ), Color.White );

		y += 20;

		if ( saveSystem is not null )
		{
			DrawSaveSystemLine( ref y, "Last Operation", saveSystem.LastOperation, Color.White );
			if ( !string.IsNullOrWhiteSpace( saveSystem.LastError ) )
				DrawSaveSystemLine( ref y, "Last Error", saveSystem.LastError, Color.Red );

			DrawSaveSystemLine( ref y, "Scene Sources", saveSystem.LoadedSceneSourceCount.ToString(), Color.White );
			DrawSaveSystemLine( ref y, "Loaded Save", saveSystem.HasLoadedSave ? ShortenDebugPath( saveSystem.LoadedSavePath ) : "none", saveSystem.HasLoadedSave ? Color.Green : Color.White );
			DrawSaveSystemLine( ref y, "Pending Roots", SaveSystem.HasPendingSavedRoots ? "yes" : "no", SaveSystem.HasPendingSavedRoots ? Color.Orange : Color.White );
			DrawSaveSystemLine( ref y, "Saved Roots", saveSystem.LastSavedRootCount.ToString(), Color.White );

			y += 20;

			DrawSaveSystemTightLine( ref y, "Runtime State", saveSystem.LastRuntimeObjectStateCount.ToString(), Color.White );
			DrawSaveSystemTightLine( ref y, "Custom Data", saveSystem.LastCustomDataCount.ToString(), Color.White );
			DrawSaveSystemTightLine( ref y, "Packages", saveSystem.LastRequiredPackageCount.ToString(), Color.White );
			DrawSaveSystemTightLine( ref y, "Sync Entries", saveSystem.LastSyncEntryCount.ToString(), Color.White );

			y += 14;

			DrawSaveSystemTightLine( ref y, "Patch Added", saveSystem.LastPatchAddedObjectCount.ToString(), Color.White );
			DrawSaveSystemTightLine( ref y, "Patch Removed", saveSystem.LastPatchRemovedObjectCount.ToString(), Color.White );
			DrawSaveSystemTightLine( ref y, "Patch Moved", saveSystem.LastPatchMovedObjectCount.ToString(), Color.White );
			DrawSaveSystemTightLine( ref y, "Patch Props", saveSystem.LastPatchPropertyOverrideCount.ToString(), Color.White );

			y += 14;

			DrawSaveSystemTightLine( ref y, "Prefab Snaps", saveSystem.LastPrefabSnapshotCount.ToString(), saveSystem.LastPrefabSnapshotCount > 0 ? Color.Cyan : Color.White );
			DrawSaveSystemTightLine( ref y, "Prefab Owners", $"{saveSystem.LastPrefabBaselineOwnerCount}/{saveSystem.LastPrefabCurrentOwnerCount}", Color.White );
			DrawSaveSystemTightLine( ref y, "Prefab Changed", saveSystem.LastPrefabChangedIdCount.ToString(), saveSystem.LastPrefabChangedIdCount > 0 ? Color.Cyan : Color.White );
			DrawSaveSystemTightLine( ref y, "Prefab Applied", saveSystem.LastPrefabAppliedSnapshotCount.ToString(), saveSystem.LastPrefabAppliedSnapshotCount > 0 ? Color.Green : Color.White );
			DrawSaveSystemTightLine( ref y, "Prefab Skipped", saveSystem.LastPrefabSkippedSnapshotCount.ToString(), saveSystem.LastPrefabSkippedSnapshotCount > 0 ? Color.Orange : Color.White );

		}

		pos.y = y;

		void DrawSaveSystemHeadLine( ref float lineY, string label, string value, Color color )
		{
			scope.Text = label;
			scope.TextColor = color;
			DebugOverlaySystem.Current.ScreenText( new Vector2( -(x + labelXOffset), lineY ), scope, TextFlag.Right, 0f );

			scope.Text = value;
			scope.TextColor = color;
			DebugOverlaySystem.Current.ScreenText( new Vector2( -x, lineY ), scope, TextFlag.Right, 0f );
			lineY += 14;
		}

		void DrawSaveSystemValueLine( ref float lineY, string value, Color color )
		{
			scope.Text = value;
			scope.TextColor = color;
			DebugOverlaySystem.Current.ScreenText( new Vector2( -x, lineY ), scope, TextFlag.Right, 0f );
			lineY += 14;
		}

		void DrawSaveSystemLine( ref float lineY, string label, string value, Color color )
		{
			scope.Text = label;
			scope.TextColor = color;
			DebugOverlaySystem.Current.ScreenText( new Vector2( -(x + labelXOffset * 1.20f), lineY ), scope, TextFlag.Right, 0f );

			scope.Text = value;
			scope.TextColor = color;
			DebugOverlaySystem.Current.ScreenText( new Vector2( -x, lineY ), scope, TextFlag.Right, 0f );
			lineY += 14;
		}

		void DrawSaveSystemTightLine( ref float lineY, string label, string value, Color color )
		{
			scope.Text = label;
			scope.TextColor = color;
			DebugOverlaySystem.Current.ScreenText( new Vector2( -(x + tightLabelXOffset), lineY ), scope, TextFlag.Right, 0f );

			scope.Text = value;
			scope.TextColor = color;
			DebugOverlaySystem.Current.ScreenText( new Vector2( -x, lineY ), scope, TextFlag.Right, 0f );
			lineY += 14;
		}

		static (string Text, Color Color) GetSaveSystemHeaderStatus( SaveSystem saveSystem )
		{
			if ( saveSystem is null )
				return ("negative", Color.Red);

			return saveSystem.LastResult switch
			{
				"fail" => ("negative", Color.Red),
				"running" => ("running", Color.Orange),
				_ => ("ok", Color.Green)
			};
		}
	}
#endif

	private static string ShortenDebugPath( string path )
	{
		if ( string.IsNullOrWhiteSpace( path ) )
			return "none";

		var normalized = path.Replace( '\\', '/' );
		var slash = normalized.LastIndexOf( '/' );

		return slash >= 0 && slash < normalized.Length - 1
			? normalized[(slash + 1)..]
			: normalized;
	}


	private static void PlayerPos( ref Vector2 pos )
	{
		if ( !BasePlayer.Local.IsValid() ) return;

		var scope = InitDebugTextScope();

		var x = pos.x;
		var y = pos.y;

		scope.Text = $"pos: {BasePlayer.Local.WorldPosition.x:0.00} {BasePlayer.Local.WorldPosition.y:0.00} {BasePlayer.Local.WorldPosition.z:0.00}";
		scope.TextColor = new Color32( 255, 145, 117 );
		DebugOverlaySystem.Current.ScreenText( new Vector2( x, y ), scope, TextFlag.Left, 0f );
		y += 14;

		scope.Text = $"ang: {BasePlayer.Local.GetEyeAngles().pitch:0.00} {BasePlayer.Local.GetEyeAngles().yaw:0.00} {BasePlayer.Local.GetEyeAngles().roll:0.00}";
		scope.TextColor = new Color32( 255, 153, 64 );
		DebugOverlaySystem.Current.ScreenText( new Vector2( x, y ), scope, TextFlag.Left, 0f );
		y += 14;

		scope.Text = $"vel: {BasePlayer.Local.Movement.Velocity.Length:0.00}";
		scope.TextColor = Color.White;
		DebugOverlaySystem.Current.ScreenText( new Vector2( x, y ), scope, TextFlag.Left, 0f );
		y += 14;

		scope.Text = $"vel2d: {BasePlayer.Local.Movement.Velocity.WithZ( 0 ).Length:0.00}";
		scope.TextColor = new Color32( 255, 133, 255 );
		DebugOverlaySystem.Current.ScreenText( new Vector2( x, y ), scope, TextFlag.Left, 0f );

		if ( BasePlayer.Local.Controller is PlayerController controller )
		{
			y += 14;
			y += 14;
			scope.Text = $"duckRatio: {controller.DuckRatio:0.00}";
			scope.TextColor = new Color32( 74, 217, 193 );
			DebugOverlaySystem.Current.ScreenText( new Vector2( x, y ), scope, TextFlag.Left, 0f );
			y += 14;

			scope.Text = $"duckSpeedScale: {controller.DuckSpeedScale:0.00}";
			scope.TextColor = new Color32( 74, 217, 193 );
			DebugOverlaySystem.Current.ScreenText( new Vector2( x, y ), scope, TextFlag.Left, 0f );
		}

		pos.y = y;
	}

	private static void Noclip( ref Vector2 pos )
	{
		if ( !BasePlayer.Local.IsValid() ) return;

		var scope = InitDebugTextScope();

		var x = pos.x;
		var y = pos.y;

		scope.Text = $"Noclip enabled";
		scope.TextColor = Color.Green;
		DebugOverlaySystem.Current.ScreenText( new Vector2( x, y ), scope, TextFlag.Left, 0f );

		pos.y = y;
	}

	private static void GodBuddha( ref Vector2 pos )
	{
		if ( !BasePlayer.Local.IsValid() ) return;

		var scope = InitDebugTextScope();

		var x = pos.x;
		var y = pos.y;

		if ( BasePlayer.God )
		{
			scope.Text = "God enabled";
			scope.TextColor = new Color32( 253, 203, 23 );
		}
		else if ( BasePlayer.Buddha )
		{
			scope.Text = "Buddha enabled";
			scope.TextColor = Color.Yellow;
		}

		DebugOverlaySystem.Current.ScreenText( new Vector2( x, y ), scope, TextFlag.Left, 0f );

		pos.y = y;
	}

	private static void Notarget( ref Vector2 pos )
	{
		if ( !BasePlayer.Local.IsValid() ) return;

		var scope = InitDebugTextScope();

		var x = pos.x;
		var y = pos.y;

		scope.Text = "Notarget enabled";
		scope.TextColor = new Color32( 224, 176, 255 );

		DebugOverlaySystem.Current.ScreenText( new Vector2( x, y ), scope, TextFlag.Left, 0f );

		pos.y = y;
	}

	private static void AIDisable( Vector2 pos )
	{
		if ( !BasePlayer.Local.IsValid() ) return;

		var scope = InitDebugTextScope();
		scope.FontSize *= 3;

		var x = pos.x;
		var y = pos.y;

		scope.Text = "AI Disabled...";
		scope.TextColor = Color.Red;

		DebugOverlaySystem.Current.ScreenText( new Vector2( x, y ), scope, TextFlag.Left, 0f );
	}

	private static void StandaloneVersion( Vector2 pos )
	{
		if ( string.IsNullOrEmpty( _projectName ) ) return;

		var scope = InitDebugTextScope();

		var x = pos.x;
		var y = pos.y;

		scope.Text = $"{_projectName} Development Build: {Standalone.BuildDate}";
		scope.TextColor = Color.White;
		DebugOverlaySystem.Current.ScreenText( new Vector2( -x, y ), scope, TextFlag.Right, 0f );
		y += 14;

		scope.Text = $"Build: {_buildNumber}";
		scope.TextColor = Color.White;
		DebugOverlaySystem.Current.ScreenText( new Vector2( -x, y ), scope, TextFlag.Right, 0f );
	}

	static private string _projectName { get; set; }
	static private int _buildNumber { get; set; }

	private static void PrepareStandaloneInfo()
	{
		if ( !Application.IsStandalone ) return;

		PrepareName();
		PrepareVersion();
	}

	private static void PrepareName()
	{
		if ( !string.IsNullOrWhiteSpace( _projectName ) ) return;

		var projectName = Game.Ident.Replace( "local.", "" );

		if ( !string.IsNullOrWhiteSpace( projectName ) ) _projectName = projectName;
		else _projectName = string.Empty;

		// _projectName = char.ToUpperInvariant( projectName[0] ) + projectName[1..];
	}

	private static void PrepareVersion()
	{
		if ( _buildNumber != 0 ) return;

		int d = Standalone.BuildDate.Day; int m = Standalone.BuildDate.Month; int y = Standalone.BuildDate.Year;
		int[] days = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 303, 334, 365]; // amount of days in a year in every month
		m -= 1; // TimeDate struct is 1-12, array is 0-11, so offset the number
		_buildNumber = 365 * (y - 2026) - 31 + days[m] + d; // original method had 1999, we are not in 1999
	}

	private static TextRendering.Scope InitDebugTextScope()
	{
		return new( "", Color.White, 12, "Roboto Mono" )
		{
			Outline = new TextRendering.Outline
			{
				Color = Color.Black,
				Enabled = true,
				Size = 3.25f
			},
			LineHeight = 0.85f
		};
	}

}
