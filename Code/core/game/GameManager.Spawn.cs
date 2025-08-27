
namespace Core;

public partial class GameManager
{
	/// <summary>
	/// The thing you should set
	/// </summary>
	[Property, ReadOnly] public bool UsePlayerStart { get; set; }

	/// <summary>
	/// UsePlayerStart but with standalone check on top, set the above and check for the below
	/// </summary>
	/// <returns>Spawn with </returns>
	public bool UsePlayerStartRule => !Application.IsEditor && UsePlayerStart;

	/// <summary>
	/// Use this if you dont want for player to spawn (menu scene, debug scene, etc)
	/// </summary>
	[Property] public bool DontSpawnPlayer { get; set; } = false;

	/// <summary>
	/// Called before Player is spawned in, usually to spawn additional managers
	/// </summary>
	protected virtual void PreSpawn()
	{
		// reset timescale
		TimeScaleSlider = 1f;
		PreviousTimeScaleSlider = 1f;
		TimeScale = 1f;

		// spawn all our managers
		Components.GetOrCreate<NpcSoundManager>();
		Scene.CreateObject().Components.GetOrCreate<DebrisManager>();

		if ( Rules is null )
		{ Log.Error( "No GameRules were given!" ); return; }

		// check if this is a level transition
		if ( Rules.CanTransition ) ExitLevelTransition();
	}

	/// <summary>
	/// Called after Player is spawned in, usually to decide spawn items
	/// </summary>
	protected virtual void PostSpawn()
	{
		foreach ( var item in Components.GetAll<BaseItem>() )
			item.PickUp = true;
	}

	/// <summary>
	/// Used to spawn the player, do not call the base class in the override
	/// </summary>
	protected virtual void PlayerSpawn()
	{
		if ( !DontSpawnPlayer )
		{
			//	otherwise spawn player at editor camera position
			SceneTraceResult tr = Scene.Trace.Ray( LastEditorCameraPosition.Position, LastEditorCameraPosition.Position - Vector3.Up * 64f ).Run();

			Player = Scene.GetPrefab( "prefabs/player.prefab" ).Clone();

			Player.WorldPosition = Application.IsEditor ? LastEditorCameraPosition.Position - Vector3.Up * 64f * tr.Fraction : Vector3.Zero;
			Player.WorldRotation = Application.IsEditor ? LastEditorCameraPosition.Rotation.Angles().WithPitch( 0 ).WithRoll( 0 ).ToRotation() : Angles.Zero;

			if ( Player.Components.TryGet<BasePlayer>( out var playerComponent2 ) )
			{
				playerComponent2.Controller.EyeAngles = Application.IsEditor ? LastEditorCameraPosition.Rotation : Angles.Zero;
				playerComponent2.Controller.Controller.Velocity = Vector3.Zero;
				playerComponent2.Controller.Controller.BaseVelocity = Vector3.Zero;
			}
		}
	}
}
