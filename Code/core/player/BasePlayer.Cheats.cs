#if FMOD
using FMODSbox;
#endif
namespace Core;

public partial class BasePlayer
{
	private const float _spawnDistance = 1024f;

	[ConCmd( "ent_create", ConVarFlags.Cheat, Help = "Create an entity (a complete assembly of objects and components, making up a full thing), this is supposed to be developer and or cheats only" )]
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
			entcreate.Components.Create( TypeLibrary.GetType( entname ) );
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

	[ConCmd( "thirdperson", ConVarFlags.Cheat, Help = "Toggle third person view (if the game can even do it)" )]
	public static void ToggleThirdPerson() => Local.Controller.CameraMode = XMovement.PlayerWalkControllerComplex.CameraModes.ThirdPerson;

	[ConCmd( "firstperson", ConVarFlags.Cheat, Help = "Toggle first person view (if the game can even do it)" )]
	public static void ToggleFirstPerson() => Local.Controller.CameraMode = XMovement.PlayerWalkControllerComplex.CameraModes.FirstPerson;

	[ConCmd( "owie", ConVarFlags.Cheat, Help = "Deal this amount of damage to yourself but this is funnier to type" )] public static void Owie( int amount ) => Hurt( amount );

	[ConCmd( "hurtme", ConVarFlags.Cheat, Help = "Deal this amount of damage to yourself" )]
	public static void Hurt( int amount )
	{
		var dmg = new CoreDamageInfo( Local.GameObject, Local.GameObject, amount );
		dmg.Tags.Add( "bullet" );

		Local.OnDamage( dmg );
	}

	[ConVar( "ch_infinite_ammo", ConVarFlags.Cheat, Help = "Infinite ammo mode. 0 = Off, 1 = Infinite reserve, 2 = Infinite magazine." )] public static int InfiniteAmmoMode { get; set; } = 0;

	public static bool InfiniteAmmo => InfiniteAmmoMode > 0;
	public static bool NoReload => InfiniteAmmoMode > 1;
}
