namespace SDK;

using System;
using Core;

[Title( "HL2K Game Manager" )]
public partial class SDKGameManager : GameManagerSystem, IPlayerEvents, Component.INetworkListener
{
	public SDKGameManager( Scene scene ) : base( scene ) => Listen( Stage.SceneLoaded, -1, HandleEditor, "CreateEngineShit" );

	protected override void DecideGameRules() => Rules = new SDKRules();

	protected override void OnStart()
	{
		InitScene();

		DecideGameRules();
		Rules?.GameStart();

		PreSpawn();
	}

	void Component.INetworkListener.OnActive( Connection channel )
	{
		SpawnNetworkPlayer( channel );
		PostSpawn();
	}

	private void SpawnNetworkPlayer( Connection channel )
	{
		if ( DontSpawnPlayer || Scene is null )
			return;

		var playerPrefab = GameObject.GetPrefab( "prefabs/player.prefab" );

		if ( playerPrefab is null )
		{
			Log.Error( "Could not find player prefab: prefabs/player.prefab" );
			return;
		}

		var spawnlist = Scene.GetAll<SpawnPoint>();

		// if we have info_player_starts pick the active one or the one closest to the camera
		if ( spawnlist.Any() && UsePlayerStartRule )
		{
			SpawnPoint closest = Random.Shared.FromList( [.. spawnlist], spawnlist.First() );

			if ( closest.IsValid() )
			{
				Player = playerPrefab.Clone();

				Player.WorldPosition = closest.WorldPosition;

				if ( Player.Components.TryGet<BasePlayer>( out var playerComponent ) )
				{
					playerComponent.Controller.EyeAngles = closest.WorldRotation;
					playerComponent.Controller.Controller.Velocity = Vector3.Zero;
					playerComponent.Controller.Controller.BaseVelocity = Vector3.Zero;
				}

				closest.GameObject.Destroy();

				return;
			}
		}
		else
		{
			//	otherwise spawn player at editor camera position
			SceneTraceResult tr = Scene.Trace.Ray( LastEditorCameraPosition.Position, LastEditorCameraPosition.Position - Vector3.Up * 64f ).Run();

			Player = playerPrefab.Clone();

			// if this is standalone there is no editor camera, so spawn at 0 0 0 (sucks but better than spawning in a random spot)
			Player.WorldPosition = !Application.IsStandalone ? LastEditorCameraPosition.Position - Vector3.Up * 64f * tr.Fraction : Vector3.Zero;

			if ( Player.Components.TryGet<BasePlayer>( out var playerComponent2 ) )
			{
				playerComponent2.Controller.EyeAngles = !Application.IsStandalone ? LastEditorCameraPosition.Rotation : Angles.Zero;
				playerComponent2.Controller.Controller.Velocity = Vector3.Zero;
				playerComponent2.Controller.Controller.BaseVelocity = Vector3.Zero;
			}
		}

		Player.NetworkSpawn( channel );
	}

	private void HandleEditor()
	{
		if ( Application.IsEditor )
		{
			_prefab ??= Scene.GetAllObjects( true )?.Where( obj => obj?.Name == "EditCam" )?.FirstOrDefault();
			_prefab ??= Scene.GetAllObjects( true )?.Where( obj => obj?.Name == "Editor Camera" )?.FirstOrDefault();
			CreateEditorStuff( false ); // we dont need to create anything in standalone because everything should already have it and if it doesnt we fucked up
		}
	}

	[Property] private GameObject _prefab { get; set; }

	public void CreateEditorStuff( bool IsRefresh, CameraExposure.ExposureMode exposureMode = CameraExposure.ExposureMode.Manual, float ISO = 100, float Shutter = 100, float Aperture = 16, float ND = 1 )
	{
		if ( Scene.IsEditor ) // dont do this if the game is running, apparently EditorOnly still picks it up
		{
			if ( Scene.IsValid() && !IsRefresh )
			{
				if ( !_prefab.IsValid() )
				{
					var editorCameraPrefab = GameObject.GetPrefab( "prefabs/editor/editor_camera.prefab" );

					if ( editorCameraPrefab is null )
					{
						Log.Warning( "Could not find editor camera prefab: prefabs/editor/editor_camera.prefab" );
						return;
					}

					_prefab = editorCameraPrefab.Clone();
					_prefab.Flags |= GameObjectFlags.Hidden | GameObjectFlags.EditorOnly;

					_prefab.LocalPosition = new Vector3( 0, 0, 0 );
					_prefab.Name = "EditCam";
				}
				else
				{
					_prefab.Flags |= GameObjectFlags.Hidden | GameObjectFlags.EditorOnly;
				}
			}
		}
	}
}
