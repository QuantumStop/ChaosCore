using Core;
public class GameInit : GameObjectSystem<GameInit>
{
	public GameInit( Scene scene ) : base( scene )
	{
		Listen( Stage.SceneLoaded, 5000, delegate
		{
			if ( !Application.IsStandalone ) CreateEditorStuff( false ); // we dont need to create anything in standalone because everything should already have it and if it doesnt we fucked up
			foreach ( var entity in Scene.GetAll<BaseEntity>() ) { entity.OnStartOnceInternal(); }
		}, "CreateEngineShit" );
	}

	GameObject Manager { get; set; }
	GameObject Prefab { get; set; }
	GameObject Exposure { get; set; }
	GameObject Video { get; set; }
	GameObject Env { get; set; }

	public void CreateEditorStuff( bool IsRefresh, CameraExposure.ExposureMode exposureMode = CameraExposure.ExposureMode.Manual, float ISO = 100, float Shutter = 100, float Aperture = 16, float ND = 1 )
	{
		if ( !IsRefresh && !Scene.GetAll<game_manager>().Any() )
		{
			Manager = Scene.CreateObject();
			Manager.WorldPosition = new Vector3( 4096, 4096, 4096 );
			Manager.Name = "Game Manager";
			Manager.Components.Create<game_manager>();
		}
		else { Manager = Scene.GetAll<game_manager>().FirstOrDefault().GameObject; } // assign the existing manager object to a variable so the rest can still regenerate

		if ( !Game.IsPlaying ) // dont do this if the game is running, apparently EditorOnly still picks it up
		{
			if ( !IsRefresh && !Scene.GetAll<Tonemapper>().Any() )
			{
				Prefab = Scene.GetPrefab( "prefabs/editor/editor_camera.prefab" ).Clone();
				Prefab.Flags = GameObjectFlags.EditorOnly | GameObjectFlags.Hidden; // hide the motherfucker its very annoying to see the lines etc
				Prefab.SetParent( Manager );
				Prefab.LocalPosition = new Vector3( 0, 0, 0 );
				Prefab.Name = "Editor Camera";
			}
		}

		if ( !IsRefresh && !Scene.GetAll<CameraExposure>().Any() )
		{
			Exposure = Scene.CreateObject();
			Exposure.SetParent( Manager );
			Exposure.LocalPosition = new Vector3( 16, 0, 0 );
			Exposure.Name = "Exposure Manager";
			var cam = Exposure.Components.GetOrCreate<CameraExposure>();
			cam.Mode = exposureMode; cam.Aperture = Aperture; cam.ISO = ISO; cam.Shutter = Shutter; cam.SunND = ND;
		}

		if ( !IsRefresh && !Scene.GetAll<videoplayer_manager>().Any() )
		{
			Video = Scene.CreateObject();
			Video.SetParent( Manager );
			Video.LocalPosition = new Vector3( -16, 0, 0 );
			Video.Components.Create<videoplayer_manager>();
			Video.Name = "Video Manager";
		}

		if ( !IsRefresh && !Scene.GetAll<EnvironmentManager>().Any() )
		{
			Env = Scene.CreateObject();
			Env.SetParent( Manager );
			Env.LocalPosition = new Vector3( -32, 0, 0 );
			Env.Components.Create<EnvironmentManager>();
			Env.Name = "Environment Manager";
		}
	}
}
