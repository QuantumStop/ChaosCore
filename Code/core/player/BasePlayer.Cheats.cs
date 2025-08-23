using Sandbox.Internal;

public partial class BasePlayer
{
	private static float SpawnDistance = 1024f;

	[ConCmd( "ent_create", ConVarFlags.Cheat )]
	public static void CreateEntity( string entname )
	{
		var tr = Local.Scene.Trace.Ray( Local.Controller.AimRay, SpawnDistance )
		.IgnoreGameObjectHierarchy( Local.GameObject )
		.WithoutTags( "trigger" )
		.HitTriggers()
		.Run();

		if ( tr.Hit )
		{
			GameObject entcreate = Local.Scene.CreateObject();
			entcreate.WorldPosition = tr.EndPosition;
			entcreate.Components.Create( GlobalGameNamespace.TypeLibrary.GetType( entname ) );
		}
		else
		{
			Log.Warning( "Can't spawn " + entname.ToString() + "! \n Too far, or bad position!" );
		}
	}

	[ConCmd( "thirdperson", ConVarFlags.Cheat )]
	public static void ToggleThirdPerson() { Local.Controller.CameraMode = XMovement.PlayerWalkControllerComplex.CameraModes.ThirdPerson; }

	[ConCmd( "firstperson", ConVarFlags.Cheat )]
	public static void ToggleFirstPerson() { Local.Controller.CameraMode = XMovement.PlayerWalkControllerComplex.CameraModes.FirstPerson; }
	[ConVar( "ch_noreload", ConVarFlags.Cheat )] public static bool NoReload { get; set; } = false;
	[ConVar( "ch_infiniteammo", ConVarFlags.Cheat )] public static bool InfiniteAmmo { get; set; } = false;
}
