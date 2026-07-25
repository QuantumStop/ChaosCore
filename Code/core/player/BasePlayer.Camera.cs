using System;
using XMovement;
namespace Core;

public partial class BasePlayer
{
	// default_fov is goldsrc and regular source up to OB, where it was superceeded by fov (default_fov still exists but its a bit of a longer story)
	// it still exists to this day, which can be found here in sbox, not letting me override it :(
	public virtual float DefaultFOV => GameSettings.FieldOfView;

	[ConVar( "cl_drawhud", Saved = true ), Description( "Show HUD (health, armor, ammo, all of that). Doesn't affect crosshair." )] public static bool ShowHud { get; set; } = true;
	[ConVar( "cl_drawcrosshair", Saved = true ), Description( "Show (any) crosshair." )] public static bool ShowCrosshair { get; set; } = true;
	[Property, Title( "HUD GameObject" ), Feature( "Defines" )] public GameObject HUDGameObject { get; set; }

	[ConVar( "cl_showexpo" ), Description( "Show exposure metering debug" )] public static bool ShowExpo { get; set; } = false;

	// helper shit cus good luck remembering fuckin' "Local.Controller.Head.LocalPosition"
	public Angles GetEyeAngles() => Controller.EyeAngles;
	public Vector3 GetEyePos() => Controller.Head.WorldPosition;
	public Vector3 GetEyeForward() => Controller.Head.WorldRotation.Forward;
	public Transform GetEyeTransform() => Controller.Head.Transform.World;
	public Vector3 GetPos() => WorldPosition;

	[Flags]
	public enum HIDEHUD_FLAGS
	{
		HIDEHUD_NONE = 0,
		/// <summary>
		/// Hide everything
		/// </summary>
		HIDEHUD_ALL = 1 << 0,
		/// <summary>
		/// Hide weapon selection inventory ui
		/// </summary>
		HIDEHUD_WEAPONSELECTION = 1 << 1,
		/// <summary>
		/// Hide flashlight icon
		/// </summary>
		HIDEHUD_FLASHLIGHT = 1 << 2,
		/// <summary>
		/// Hide when local player is dead
		/// </summary>
		HIDEHUD_PLAYERDEAD = 1 << 3,
		/// <summary>
		/// Hide when the local player doesn't have the PCV suit
		/// </summary>
		HIDEHUD_NEEDSUIT = 1 << 4,
		/// <summary>
		/// Hide miscellaneous status elements (trains, pickup history, death notices, etc)
		/// </summary>
		HIDEHUD_MISCSTATUS = 1 << 5,
		/// <summary>
		/// Hide all communication elements (saytext, voice icon, etc)
		/// </summary>
		HIDEHUD_CHAT = 1 << 6,
		/// <summary>
		/// Hide crosshairs
		/// </summary>
		HIDEHUD_CROSSHAIR = 1 << 7,
		/// <summary>
		/// Hide vehicle crosshair
		/// </summary>
		HIDEHUD_VEHICLE_CROSSHAIR = 1 << 8,
		/// <summary>
		/// Hide vehicle HUD
		/// </summary>
		HIDEHUD_INVEHICLE = 1 << 9,
	}

	/// <summary>
	/// Add additional flags passed into our ui
	/// </summary>
	public HIDEHUD_FLAGS CurrentHiddenHUDFlags { get; set; } = HIDEHUD_FLAGS.HIDEHUD_NONE;

	/// <summary>
	/// Check to know hidden HUD elements
	/// </summary>
	/// <param name="flag"></param>
	/// <returns></returns>
	public bool IsHUDElementHidden( HIDEHUD_FLAGS flag )
	{
		// No local player yet?
		if ( !Local.IsValid() || !IsPossessedLocally )
			return true;

		bool check = false;

		// Check active hidden flags
		if ( (CurrentHiddenHUDFlags & flag) != 0 )
			return true; // force early out

		if ( (flag & HIDEHUD_FLAGS.HIDEHUD_ALL) != 0 )
			return true;

		if ( (flag & HIDEHUD_FLAGS.HIDEHUD_PLAYERDEAD) != 0 )
			check = Local.Health <= 0 && Local.LifeState == LifeState.Dead;
		if ( (flag & HIDEHUD_FLAGS.HIDEHUD_CROSSHAIR) != 0 && !check ) // only check if the check before didn't pass
			check = !ShowCrosshair;
		if ( (flag & HIDEHUD_FLAGS.HIDEHUD_NEEDSUIT) != 0 && !check )
			check = !Local.HasSuit;

		return check;
	}

	/// <summary>
	/// The absolute current modified FOV the player is seeing, as opposed to default inteded FOV you expect to return to
	/// </summary>
	[Property, Feature( "Debug" ), ReadOnly] public float CurrentFOV { get; protected set; }
	/// <summary>
	/// What speed are we lerping to new FOV with
	/// </summary>
	[Property, Feature( "Debug" ), ReadOnly] protected float _fovRate { get; set; }
	/// <summary>
	/// If we are lerping FOV, what are we lerping to
	/// </summary>
	[Property, Feature( "Debug" ), ReadOnly] protected float _fovTarget { get; set; }
	/// <summary>
	/// What the start FOV for our lerping is
	/// </summary>
	[Property, Feature( "Debug" ), ReadOnly] protected float _fovStart { get; set; }
	[Property, Feature( "Debug" ), ReadOnly] protected float _fovTime { get; set; }

	protected virtual float GetFOV()
	{
		float fFOV = (_fovTarget <= 0) ? DefaultFOV : _fovTarget;

		// If it's immediate, just do it
		if ( _fovRate <= 0 )
			return fFOV;

		float deltaTime = (WorldTime.Now - _fovTime) / _fovRate;

		if ( deltaTime >= 1.0f )
			_fovStart = fFOV;
		else
			fFOV = EasingPlus.SimpleSplineRemapValClamped( deltaTime, 0.0f, 1.0f, _fovStart, fFOV );

		return fFOV;
	}

	/// <summary>
	/// Handles FOV appliaction, virtual so we can also override this too for whatever reason
	/// </summary>
	protected virtual void CalculateFOV() => Controller.Camera.FieldOfView = CurrentFOV = GetFOV();

	[Property, Feature( "Debug" ), ReadOnly] protected BaseEntity _zoomOwner { get; set; }

	/// <summary>
	/// Smoothly blend the FOV to this
	/// </summary>
	/// <param name="Requester">Who requested this, if we don't have an owner (no zoom happening), this becomes the new owner</param>
	/// <param name="targetFOV">What FOV do we blend to</param>
	/// <param name="rate">Speed of the blend (in seconds)</param>
	/// <param name="zoomStart">What FOV do we start from (0 if current)</param>
	/// <param name="overrideowner">Do we force the zoom and override the owner</param>
	/// <returns>Did we succeed or the checks failed</returns>
	public virtual bool SetFOV( BaseEntity Requester, int targetFOV, float rate = 0, int zoomStart = 0, bool overrideowner = false )
	{
		if ( !Requester.IsValid() ) return false;

		if ( !overrideowner && _zoomOwner.IsValid() && _zoomOwner != Requester ) return false;
		else _zoomOwner = targetFOV <= 0 ? null : Requester;

		// Setup our FOV and our scaling time
		_fovStart = zoomStart > 0 ? zoomStart : GetFOV();

		_fovTime = WorldTime.Now;
		_fovTarget = targetFOV;

		_fovRate = rate;

		return true;
	}

	protected void UpdateBodyVisibility()
	{
		if ( !IsControlledLocally )
		{
			foreach ( ModelRenderer mdlrenderer in Controller.Body.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ) )
			{
				mdlrenderer.RenderType = ModelRenderer.ShadowRenderType.On;
			}
			return;
		}
		if ( Controller.CameraMode == PlayerWalkControllerComplex.CameraModes.FirstPerson )
		{
			foreach ( ModelRenderer mdlrenderer in Controller.Body.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ) )
			{
				mdlrenderer.RenderType = Controller.PlayerShadowsOnly ? ModelRenderer.ShadowRenderType.ShadowsOnly : ModelRenderer.ShadowRenderType.On;
			}
		}
		if ( Controller.CameraMode == PlayerWalkControllerComplex.CameraModes.ThirdPerson && Controller.BodyModelRenderer.RenderType == ModelRenderer.ShadowRenderType.ShadowsOnly && Controller.PlayerShadowsOnly )
		{
			foreach ( ModelRenderer mdlrenderer in Controller.Body.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ) )
			{
				mdlrenderer.RenderType = ModelRenderer.ShadowRenderType.On;
			}
		}
	}
}
