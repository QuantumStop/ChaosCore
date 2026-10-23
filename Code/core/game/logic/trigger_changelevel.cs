namespace Core;

/// <summary>A trigger that changes level to a defined scene.</summary>
[Icon( "multiple_stop" )]
public class trigger_changelevel : BaseTrigger
{
	[Title( "Next scene" )][Property] public SceneFile NextScene { get; set; }

	[Property] public bool DestroyPlayer { get; set; }


	[ShowIf( nameof( IsDebug ), true )][Feature( "Debug" ), Property, ReadOnly] public SceneFile CurrentScene;

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		DebugOverlay.ScreenText( new Vector2( 250, 25 ), "current scene:" + Game.ActiveScene.Source.ToString() );
		DebugOverlay.ScreenText( new Vector2( 250, 50 ), NextScene?.ToString() );
	}

	protected override void OnTriggerIn()
	{
		base.OnTriggerIn();

		var item = TrackedItems.Keys.FirstOrDefault( i => i.Tags.Has( "player" ) && IsEnabled );

		if ( item.IsValid() )
		{
			if ( DestroyPlayer )
			{
				item.Flags = ~GameObjectFlags.DontDestroyOnLoad;
			}
			else
			{
				item.Flags = GameObjectFlags.DontDestroyOnLoad;
			}

			ChangeScene();
		}

	}



	void ChangeScene()
	{
		if ( NextScene is null )
		{
			Log.Warning( this + " No Scene selected!" );
			return;
		}

		if ( NextScene == Game.ActiveScene.Source )
		{
			Log.Info( this + "Next scene is the same as current one, trigger will not execute" );
			return;
		}

		GameManagerSystem.Current.EnterLevelTransition( NextScene.ResourcePath );

		//var load = new SceneLoadOptions();
		//
		//// S&Box loading screen, if we don't have it we'll just have frame stop. 
		//// In most cases results in instant load, and without a blackscreen transition.
		//// TODO: Add an additive scene that can have a panel/3d models for loading visuals (if needed)
		//
		//load.ShowLoadingScreen = false;  
		//
		//load.SetScene( nextScene );
		//
		////Scene.Load( load );
		//
		//Game.ChangeScene(load);
	}

}
