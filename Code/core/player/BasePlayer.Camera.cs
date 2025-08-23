using System;
using Sandbox.Engine.Settings;
public partial class BasePlayer
{
	// default_fov is goldsrc and regular source up to OB, where it was superceeded by fov (default_fov still exists but its a bit of a longer story)
	// it still exists to this day, which can be found here in sbox, not letting me override it :(
	public float DefaultFOV => GameSettings.FieldOfView;

	[ConVar( "cl_showpos" ), Description( "Show player position and rotation debug." )] public static bool ShowPos { get; set; } = false;
	[ConVar( "cl_showcrouch" ), Description( "Show player crouch debug." )] public static bool ShowCrouchDebug { get; set; } = false;
	[ConVar( "cl_drawhud" ), Description( "Show HUD (health, armor, ammo, all of that). Doesn't affect crosshair." )] public static bool ShowHud { get; set; } = true;
	[ConVar( "cl_drawcrosshair" ), Description( "Show (any) crosshair." )] public static bool ShowCrosshair { get; set; } = true;
	[Property, Title( "HUD GameObject" ), Feature( "Defines" )] public GameObject HUDGameObject { get; set; }

	[ConVar( "cl_showexpo" ), Description( "Show exposure metering debug" )] public static bool ShowExpo { get; set; } = false;

	// helper shit cus good luck remembering fuckin' "Local.Controller.Head.LocalPosition"
	public Angles GetEyeAngles() { return Local.Controller.EyeAngles; }
	public Vector3 GetEyePos() { return Local.Controller.Head.WorldPosition; }
	public Vector3 GetEyeForward() { return Local.Controller.Head.WorldRotation.Forward; }
	public Transform GetEyeTransform() { return Local.Controller.Head.Transform.World; }

	[Flags]
	public enum HIDEHUD_FLAGS
	{
		HIDEHUD_NONE = 0,
		[Description( "Hide ammocount & weapon selection" )]
		HIDEHUD_WEAPONSELECTION = 1,
		[Description( "Hide flashlight icon" )]
		HIDEHUD_FLASHLIGHT = 2,
		[Description( "Hide everything" )]
		HIDEHUD_ALL = 3,
		[Description( "Hide when local player's dead" )]
		HIDEHUD_PLAYERDEAD = 4,
		[Description( "Hide when the local player doesn't have the PCV suit" )]
		HIDEHUD_NEEDSUIT = 5,
		[Description( "Hide miscellaneous status elements (trains, pickup history, death notices, etc)" )]
		HIDEHUD_MISCSTATUS = 6,
		[Description( "Hide all communication elements (saytext, voice icon, etc)" )]
		HIDEHUD_CHAT = 7,
		[Description( "Hide crosshairs" )]
		HIDEHUD_CROSSHAIR = 8,
		[Description( "Hide vehicle crosshair" )]
		HIDEHUD_VEHICLE_CROSSHAIR = 9,
		[Description( "Hide vehicle HUD" )]
		HIDEHUD_INVEHICLE = 10
	}

	/// <summary>
	/// Marks current hidden hud
	/// </summary>
	public HIDEHUD_FLAGS CurrentHiddenHUDFlags { get; set; } = HIDEHUD_FLAGS.HIDEHUD_NONE;

	/// <summary>
	/// Check to know hidden HUD elements
	/// </summary>
	/// <param name="flag"></param>
	/// <returns></returns>
	public bool IsHUDElementHidden( HIDEHUD_FLAGS flag )
	{
		// Not in game?
		if ( !Game.InGame )
			return true;

		// No local player yet?
		if ( !Local.IsValid() && !IsProxy )
			return true;

		// Check active hidden flags
		if ( (CurrentHiddenHUDFlags & flag) != 0 )
			return true;

		// Everything hidden?
		if ( flag == HIDEHUD_FLAGS.HIDEHUD_ALL )
			return true;

		// Local player dead?
		if ( (flag == HIDEHUD_FLAGS.HIDEHUD_PLAYERDEAD) && Local.Health <= 0 && LifeState == LifeState.Dead )
		{
			/*Log.Info( "Definitely dead" );*/
			return true;
		}

		// Hide crosshair?
		if ( flag == HIDEHUD_FLAGS.HIDEHUD_CROSSHAIR )
			return true;

		// Need the HEV/PCV suit ( HL2K )
		if ( (flag == HIDEHUD_FLAGS.HIDEHUD_NEEDSUIT) && (!Local.HasSuit) )
			return true;

		// not hidden otherwise
		return false;
	}
}
