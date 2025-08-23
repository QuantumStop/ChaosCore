using System.Diagnostics;

namespace Core;

[Description( "A trigger that changes level to a defined scene." )]
[Icon( "multiple_stop" )]
public class trigger_changelevel : BaseTrigger
{
	[Title( "Next scene" )][Property] public SceneFile nextScene { get; set; }

	[Property] public bool destroyPlayer { get; set; }


	[HideIf( "isDebug", false )][Feature( "Debug" ), Property, ReadOnly] public SceneFile currentScene;

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		DebugOverlay.ScreenText( new Vector2( 250, 25 ), "current scene:" + Game.ActiveScene.Source.ToString() );
		DebugOverlay.ScreenText( new Vector2( 250, 50 ), nextScene?.ToString() );
	}

	protected override void OnTriggerIn()
	{

		base.OnTriggerIn();

		var item = trackedItems.Keys.FirstOrDefault( i => i.Tags.Has( "player" ) && isEnabled );

		if ( item is not null )
		{
			if ( destroyPlayer )
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
		if ( nextScene is null )
		{
			Log.Warning( this + " No Scene selected!" );
			return;
		}

		if ( nextScene == Game.ActiveScene.Source )
		{
			Log.Info( this + "Next scene is the same as current one, trigger will not execute" );
			return;
		}


		game_manager.GameManager.EnterLevelTransition( nextScene.ResourcePath );

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
