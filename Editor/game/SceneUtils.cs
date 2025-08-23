using Facepunch.ActionGraphs;
public class SceneUtils
{
	static Scene GetScene()     // surprisingly Editor doesn't know about current scene by default, and we need an object reference
	{
		// can't spawn children corrently without this but it shits the play mode
		//	SceneEditorSession.Scope();
		return SceneEditorSession.Active.Scene;
	}

	[Menu( "Editor", "chaoscore/Utils/Refresh All Probes" ), Order( 100 )]
	static void RefreshAllProbes()
	{
		var scene = GetScene();

		if ( !scene.IsValid() )
			return;

		var allprobes = scene.GetAll<EnvmapProbe>();

		if ( allprobes.Any() )
		{
			foreach ( var probe in allprobes )
			{
				probe.Dirty = true;
			}
			Log.Info( "Probes refreshed!" );
		}
		else
		{
			Log.Info( "There are no probes why did you call me" );
		}
	}

	/*
		//	[Menu( "Editor", "HLchaoscore2K/Utils/Regenerate Game Manager" ), Order( 101 )]
		static void RecreateManager()
		{
			var scene = GetScene();

			if ( !scene.IsValid() )
				return;

			if ( scene.Scene.Components.TryGet<game_manager>( out var old, FindMode.EverythingInSelfAndChildren ) )
			{
				var camera = old.Components.Get<CameraExposure>( FindMode.EverythingInChildren );
				if ( old.Active )
				{
					CameraExposure.ExposureMode exposureMode = camera.Mode; float Aperture = camera.Aperture; float ISO = camera.ISO; float Shutter = camera.Shutter; float ND = camera.SunND;
					Log.Info( "Exposure settings copied..." );
					old.GameObject.Destroy();
					Log.Info( "Old Game Manager Cleared!" );

					GameInit.Get( scene ).CreateEditorStuff( true, exposureMode, ISO, Shutter, Aperture, ND );
				}
				else
				{
					Log.Warning( "Something was fucked up with the old manager >:(" );
				}
			}
			else
			{
				Log.Info( "Didn't find a game manager, shitting pant :(\n Still give you a new one though..." );
				// default values
				GameInit.Get( scene ).CreateEditorStuff( true );
			}

		}
	*/

	public static void TogglePlayerStart( bool which )
	{
		var scene = GetScene();

		if ( !scene.IsValid() )
			return;

		// doesnt work when not a soundevent, or rather works only for on/true
		SoundEvent sound = which ? ResourceLibrary.Get<SoundEvent>( "sound/ui/editor_player_start_on.sound" ) : ResourceLibrary.Get<SoundEvent>( "sound/ui/editor_player_start_off.sound" );
		Sound.Play( sound );

		var balls = scene.GetAllComponents<game_manager>().FirstOrDefault();

		if ( balls is null )
			return;

		// if there are no start entities, then the bool isnt changed, which may or may not be desired effect, which if its not, move this above the warning check
		balls.UsePlayerStart = which;

		string ball = which ? "enabled!" : "disabled!";
		Log.Info( "All info_player_start were " + ball );
	}

	public static bool GetPlayerStart()
	{
		var scene = GetScene();

		if ( !scene.IsValid() )
			return false;

		var balls = scene.GetAllComponents<game_manager>().FirstOrDefault();

		if ( balls is null )
			return false;

		return balls.UsePlayerStart;
	}
}
