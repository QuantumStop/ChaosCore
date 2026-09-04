#if FMOD
using FMODSbox;
#endif
using Sandbox.Internal;
namespace Core;

public partial class BasePlayer
{
	private const float _spawnDistance = 1024f;

	[ConCmd( "ent_create", ConVarFlags.Cheat )]
	public static void CreateEntity( string entname )
	{
		var tr = Local.Scene.Trace.Ray( Local.Controller.AimRay, _spawnDistance )
		.IgnoreGameObjectHierarchy( Local.GameObject )
		.WithoutTags( "trigger" )
		.HitTriggers()
		.UseHitPosition()
		.Run();

		if ( tr.Hit )
		{
			GameObject entcreate = Local.Scene.CreateObject();
			entcreate.WorldPosition = tr.HitPosition + tr.HitPosition.Normal * 32;
			entcreate.Components.Create( GlobalGameNamespace.TypeLibrary.GetType( entname ) );
#if FMOD
			FMODSound.Play( "event:/Player/HUD/LessonStart" );
#endif
		}
		else
		{
			Log.Warning( "Can't spawn " + entname.ToString() + "! \n Too far, or bad position!" );
#if FMOD
			FMODSound.Play( "event:/Player/HUD/DenyWeaponSelection" );
#endif
		}
	}

	[ConCmd( "thirdperson", ConVarFlags.Cheat )]
	public static void ToggleThirdPerson()
	{
		Local.Controller.CameraMode = XMovement.PlayerWalkControllerComplex.CameraModes.ThirdPerson;
		Local.ViewmodelVisible = false;
		Local.Controller.Camera.RenderExcludeTags.Remove( "thirdperson" );
	}

	[ConCmd( "firstperson", ConVarFlags.Cheat )]
	public static void ToggleFirstPerson()
	{
		Local.Controller.CameraMode = XMovement.PlayerWalkControllerComplex.CameraModes.FirstPerson;
		Local.ViewmodelVisible = true;
		Local.Controller.Camera.RenderExcludeTags.Add( "thirdperson" );

	}
	[ConVar( "ch_infinite_ammo", ConVarFlags.Cheat )] public static int InfiniteAmmoMode { get; set; } = 0;

	public static bool InfiniteAmmo => InfiniteAmmoMode > 0;
	public static bool NoReload => InfiniteAmmoMode > 1;
}
