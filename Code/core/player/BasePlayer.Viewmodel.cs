using System;

namespace Core;

public partial class BasePlayer
{
	[Property, ReadOnly, Feature( "Viewmodel" )]
	public bool ViewmodelVisible
	{
		get;
		set
		{
			if ( field != value )
			{
				if ( value == true && !IsPossessedLocally ) return;
				field = value;
				ToggleViewmodel( value );
			}
		}
	} = true;

	/// <summary>
	/// TODO: Revisit this when we'll do first person body
	/// </summary>
	public void ToggleViewmodel( bool newval ) => ViewmodelWeaponObject.Enabled = newval == true && ShouldDrawViewmodel();

	public virtual bool ShouldDrawViewmodel()
	{
		if ( !IsPossessedLocally ) return false;
		if ( !CurrentWeapon.IsValid() ) return false;
		if ( !CurrentWeapon.WeaponData.WeaponViewmodel.IsValid() ) return false;

		return true;
	}

	/// <summary>
	/// There are certain moments when we don't want to calculate sway etc (mostly for code exception reasons),
	/// this determines when we want that
	/// </summary>
	private bool _allowSway => ViewmodelVisible && WantSway;
	public bool WantSway { get; set; } = true;

	/// <summary>
	/// Renderer for the gun
	/// </summary>
	[Property, Feature( "Viewmodel" )] public SkinnedModelRenderer ViewmodelWeapon { get; set; }
	/// <summary>
	/// Renderer for the hands
	/// </summary>
	[Property, Feature( "Viewmodel" )] public SkinnedModelRenderer ViewmodelHands { get; set; }
	/// <summary>
	/// The GameObject we put two renderers on
	/// </summary>
	[Property, Feature( "Viewmodel" )] public GameObject ViewmodelWeaponObject { get; set; }

	/// <summary>
	/// Get the viewmodel model
	/// </summary>
	/// <returns>Model as path</returns>
	protected virtual string GetViewmodel() => "";
	/// <summary>
	/// Vertex colors for blending in/out the FOV correction
	/// </summary>
	/// <returns>The three colors as a whole</returns>
	protected virtual Vector3 GetViewmodelFovMask() => Vector3.Up;


	[ConVar( "viewmodel_fov", Saved = true )]
	public static float ViewmodelFOV { get; set; } = 55f;

	[Property, Feature( "Viewmodel" ), Range( 1f, 179f )]
	public float ViewmodelFOVOverride { get; set; } = 0f;

	private float _lastPitch;
	private float _lastYaw;
	[ReadOnly, Property, Feature( "Viewmodel" )] public float YawInertia { get; set; }
	[ReadOnly, Property, Feature( "Viewmodel" )] public float PitchInertia { get; set; }
	private Vector3 _lerpedWishMove;

	[Group( "General Sway" ), Property, Feature( "Viewmodel" )] public float ViewInertiaSmoothTime { get; set; } = 0.015f;
	[Group( "General Sway" ), Property, Feature( "Viewmodel" )] public float ViewMaxInertia { get; set; } = 60f;
	[Group( "General Sway" ), Property, Feature( "Viewmodel" )] public float SwayIntensity { get; set; } = 8f;
	[Group( "General Sway" ), Property, Feature( "Viewmodel" )] public float SwayReturnSpeed { get; set; } = 8f;
	[Group( "General Sway" ), Property, Feature( "Viewmodel" )] public float SwayMaxOffset { get; set; } = 2.5f;
	[Group( "General Sway" ), Property, Feature( "Viewmodel" )] public float ViewmodelYawInertiaScale { get; set; } = 5f;
	[Group( "General Sway" ), Property, Feature( "Viewmodel" )] public float ViewmodelPitchInertiaScale { get; set; } = 2.5f;


	[Group( "General Leaning" ), Property, Range( -100, 100 ), Feature( "Viewmodel" )] public float MaxLeanAngle { get; set; } = 60f;
	[Group( "General Leaning" ), Property, Range( -50, 50 ), Feature( "Viewmodel" )] public float PitchLeanMin { get; set; } = -1f;
	[Group( "General Leaning" ), Property, Range( -50, 50 ), Feature( "Viewmodel" )] public float PitchLeanMax { get; set; } = 1f;
	[Group( "General Leaning" ), Property, Feature( "Viewmodel" )] public float LeanLerpSpeed { get; set; } = 6f;
	[Group( "General Leaning" ), Property, Feature( "Viewmodel" )] public float YawOffsetLerpSpeed { get; set; } = 6f;
	[Group( "General Leaning" ), Property, Feature( "Viewmodel" )] public float DownwardPitchYawOffset { get; set; } = 0.5f;


	[Group( "Forward Leaning" ), Property, Range( 0, 150 ), Feature( "Viewmodel" )] public float ForwardLeanAmount { get; set; } = 15f;
	[Group( "Forward Leaning" ), Property, Range( 0, 150 ), Feature( "Viewmodel" )] public float BackwardLeanAmount { get; set; } = 15f;
	[Group( "Forward Leaning" ), Property, Range( 0, 1f ), Feature( "Viewmodel" )] public float LeanLookBackMultiplier { get; set; } = 0.25f;
	[Group( "Forward Leaning" ), Property, Range( 0f, 50f ), Step( 1 ), Feature( "Viewmodel" )] public float ProximityLeanFactor { get; set; } = 40f;
	[Group( "Forward Leaning" ), Property, Range( 0f, 1f ), Step( 0.01f ), Feature( "Viewmodel" )] public float ProximityThreshold { get; set; } = 0.35f;


	[Group( "Viewmodel Roll" ), Property, Range( -5f, 5f ), Feature( "Viewmodel" )] public float StrafeDirectionRoll { get; set; } = 0.35f;
	[Group( "Viewmodel Roll" ), Property, Range( 0f, 10f ), Feature( "Viewmodel" )] public float InertiaRollIntensity { get; set; } = 0.1f;
	[Group( "Viewmodel Roll" ), Property, Range( 0f, 10f ), Feature( "Viewmodel" )] public float RotationRollIntensity { get; set; } = 0.25f;
	[Group( "Viewmodel Roll" ), Property, Range( 0f, 50f ), Feature( "Viewmodel" )] public float MaxTotalRoll { get; set; } = 15f;
	[Group( "Viewmodel Roll" ), Property, Range( 0f, 20f ), Feature( "Viewmodel" )] public float RollLerpSpeed { get; set; } = 6f;

	[Feature( "Debug" ), Property] public bool IsDebug { get; set; } = false;


	// Need to do this here, as it doesn't get initialized properly in the curve itself
	private static float _localCrouchSpeed => Local?.Controller?.CrouchSpeed ?? 0f;
	private static float _localWalkSpeed => Local?.Controller?.WalkSpeed ?? 0f;
	private static float _localDefaultSpeed => Local?.Controller?.DefaultSpeed ?? 0f;
	private static float _localRunSpeed => Local?.Controller?.RunSpeed ?? 0f;

	[Group( "ViewBob" ), Property, Range( 0f, 50f ), Feature( "Viewmodel" )]
	public Curve BobAmplitudeCurve { get; set; } = new Curve(
	new[] {
		new Curve.Frame( _localCrouchSpeed,  0.05f ),
		new Curve.Frame( _localWalkSpeed,    0.15f ),
		new Curve.Frame( _localDefaultSpeed, 0.35f ),
		new Curve.Frame( _localRunSpeed,     1.0f )
		}
	);

	[Group( "ViewBob" ), Property, Feature( "Viewmodel" )]
	public Curve BobFrequencyCurve { get; set; } = new Curve(
	new[] {
		new Curve.Frame( _localCrouchSpeed,  0.5f ),
		new Curve.Frame( _localWalkSpeed,    1.0f ),
		new Curve.Frame( _localDefaultSpeed, 1.5f ),
		new Curve.Frame( _localRunSpeed,     2.0f )
		}
	);

	[Group( "ViewBob" ), Property, Range( 0f, 5f ), Feature( "Viewmodel" )] public float BobHorizontalScale { get; set; } = 1.25f;
	[Group( "ViewBob" ), Property, Range( 0f, 10f ), Feature( "Viewmodel" )] public float BobRollScale { get; set; } = 0.45f;

	[Group( "ViewRoll" ), Property, Range( 0f, 10f ), Feature( "Viewmodel" )] public float StrafeRollAngle { get; set; } = 5f;
	[Group( "ViewRoll" ), Property, Range( 0f, 500f ), Feature( "Viewmodel" )] public float StrafeRollSpeed { get; set; } = 350f;
	[Group( "ViewRoll" ), Property, Range( 0f, 20f ), Feature( "Viewmodel" )] public float StrafeRollLerpSpeed { get; set; } = 20f;

	[Group( "Viewmodel Offset" ), Property, Feature( "Viewmodel" )] public float ViewmodelOffsetForward { get; set; } = 0;
	[Group( "Viewmodel Offset" ), Property, Feature( "Viewmodel" )] public float ViewmodelOffsetRight { get; set; } = 0f;
	[Group( "Viewmodel Offset" ), Property, Feature( "Viewmodel" )] public float ViewmodelOffsetUp { get; set; } = 0f;

	[ConVar( "viewmodel_offset_forward", Help = "Additive viewmodel forward offset (inches)" )]
	private static float _viewmodelOffsetForwardConvar { get; set; } = 0f;
	[ConVar( "viewmodel_offset_right", Help = "Additive viewmodel right offset (inches)" )]
	private static float _viewmodelOffsetRightConvar { get; set; } = 0f;
	[ConVar( "viewmodel_offset_up", Help = "Additive viewmodel up offset (inches)" )]
	private static float _viewmodelOffsetUpConvar { get; set; } = 0f;

	protected float GetViewmodelOffsetForward() => ViewmodelOffsetForward + _viewmodelOffsetForwardConvar;
	protected float GetViewmodelOffsetRight() => ViewmodelOffsetRight + _viewmodelOffsetRightConvar;
	protected float GetViewmodelOffsetUp() => ViewmodelOffsetUp + _viewmodelOffsetUpConvar;


	/// <summary>
	/// To be able to externally override this from anywhere 
	/// </summary>
	public Vector3 ViewmodelBlend { get; set; } = Vector3.Up;

	protected virtual void ViewmodelFixedUpdate()
	{
		SetAllAnimgraphParams( "f_walking", Local.Movement.Velocity.Length / (Local.Controller.IsRunning ? 320 : 190) );
		SetAllAnimgraphParams( "f_timesincewishsprint", (Local.Controller as PlayerController).TimeSinceWishSprint() );
		SetAllAnimgraphParams( "f_wishsprint", (Local.Controller as PlayerController).WishSprint() );
		SetAllAnimgraphParams( "b_sprint", Local.Controller.IsRunning );
		SetAllAnimgraphParams( "b_grounded", Controller.Controller.IsOnGround );
		SetAllAnimgraphParams( "aim_pitch_inertia", -_aimPitchLeanSmoothed + _viewPitchInertia * ViewmodelPitchInertiaScale );
		SetAllAnimgraphParams( "aim_yaw_inertia", _viewYawInertia * ViewmodelYawInertiaScale + _smoothedYawOffset );
		SetAllAnimgraphParams( "aim_yaw", _smoothedRoll );
		SetAllAnimgraphParams( "aim_pitch", _aimPitchLean );
		ApplyInertia();
		ApplyVelocity();

		UpdateViewBob();
	}

	protected virtual void ViewmodelUpdate()
	{
		UpdateViewmodelOffset();
		CameraEffects.Update( Local.Controller.Camera, Local.CurrentWeapon?.LastAttackTime );
	}

	void ApplyInertia()
	{
		var camera = Local.Controller.Camera;
		var newPitch = camera.WorldRotation.Pitch();
		var newYaw = camera.WorldRotation.Yaw();

		PitchInertia = Angles.NormalizeAngle( newPitch - _lastPitch );
		YawInertia = Angles.NormalizeAngle( _lastYaw - newYaw );

		_lastPitch = newPitch;
		_lastYaw = newYaw;
	}

	// ======== Viewmodel Offset ======== //

	// Viewmodel Offset control fields //	

	/// <summary>
	/// Stores the extra pitch lean when looking up/down past a specific threshold.
	/// This helps simulate more pronounced lean based on pitch changes.
	/// </summary>
	private float _retainedPitchLean = 0f;

	/// <summary>
	/// Holds the smoothed roll value for the viewmodel's roll effect.
	/// This is used to make the roll transition smoother over time.
	/// </summary>
	private float _smoothedRoll = 0f;

	/// <summary>
	/// Holds the smoothed pitch lean value for aiming. This is used to apply smooth changes when aiming.
	/// </summary>
	private float _aimPitchLeanSmoothed = 0f;

	/// <summary>
	/// Holds the smoothed yaw offset derived from the camera's pitch.
	/// This helps smooth out the yaw during transitions when the pitch changes.
	/// </summary>
	private float _smoothedYawOffset = 0f;

	/// <summary>
	/// Holds the smoothed camera pitch value, which is used for lean calculations during camera movement.
	/// </summary>
	private float _smoothedCameraPitch = 0f;

	/// <summary>
	/// Holds the current yaw inertia from the camera's view rotation.
	/// This is used to simulate inertia effects when the camera rotates along the yaw axis.
	/// </summary>
	private float _viewYawInertia;

	/// <summary>
	/// Holds the current pitch inertia from the camera's view rotation.
	/// This value helps simulate inertia effects when the camera rotates along the pitch axis.
	/// </summary>
	private float _viewPitchInertia;

	/// <summary>
	/// Holds the target pitch lean value.
	/// This is the desired pitch lean that the system will gradually reach over time.
	/// </summary>
	private float _aimPitchLean;

	/// <summary>
	/// Holds the smoothed yaw inertia for the sway effect.
	/// This value allows smoother transitions for the sway effect based on yaw rotation.
	/// </summary>
	private float _smoothedViewYawInertia;

	/// <summary>
	/// Holds the smoothed pitch inertia for the sway effect.
	/// This value allows smoother transitions for the sway effect based on pitch rotation.
	/// </summary>
	private float _smoothedViewPitchInertia;

	private const float _viewmodelReferenceFramerate = 60f;
	private float ToReferenceFrameDelta( float angleDelta )
	{
		float dt = MathF.Max( Time.Delta, 0.0001f );
		return angleDelta / dt * (1f / _viewmodelReferenceFramerate);
	}

	/// <summary>
	/// Holds the last frame's camera rotation, used for delta calculation in determining view rotation changes.
	/// </summary>
	private Rotation _lastViewRot;

	/// <summary>
	/// The current sway offset applied to the viewmodel.
	/// This is the offset that will be applied to the viewmodel's position for simulating camera sway.
	/// </summary>
	public Vector3 SwayOffset { get; private set; }

	public void UpdateViewmodelOffset()
	{
		if ( Local.LifeState == LifeState.Dead ) // Don't do anything if player is dead.
			return;

		var camera = Local.Controller.Camera;
		var currentViewRot = camera.WorldRotation;
		var cameraForward = currentViewRot.Forward.Normal;
		var cameraSide = currentViewRot.Left.Normal;
		var playerVelocity = Local.Movement.Velocity;
		var cameraPitch = Local.Controller.WorldRotation.Pitch();

		var delta = _lastViewRot.Inverse * currentViewRot;
		var deltaAngles = delta.Angles();

		_lastViewRot = currentViewRot;

		UpdateViewInertia( deltaAngles );                                              // Calculate view rotation deltas for inertia
		UpdateSwayOffset();                                                            // Apply weapon sway based on view deltas
		UpdatePitchLean( cameraPitch );                                                // Apply vertical lean from camera pitch
		UpdateYawOffsetFromPitch( cameraPitch );                                       // Add yaw offset when looking up/down
		UpdateMovementLean( playerVelocity, cameraForward );                           // Add forward/backward lean based on movement
		UpdateRollLean( playerVelocity, cameraSide, cameraForward, deltaAngles );      // Roll lean from strafing, movement and turning
	}


	private void UpdateViewInertia( Angles deltaAngles )
	{
		float t = Time.Delta / (ViewInertiaSmoothTime + 0.0001f);

		float yawDelta = ToReferenceFrameDelta( deltaAngles.yaw );
		float pitchDelta = ToReferenceFrameDelta( deltaAngles.pitch );

		_smoothedViewYawInertia = MathX.Lerp( _smoothedViewYawInertia, yawDelta, t );
		_smoothedViewPitchInertia = MathX.Lerp( _smoothedViewPitchInertia, pitchDelta, t );

		_viewYawInertia = _smoothedViewYawInertia.Clamp( -ViewMaxInertia, ViewMaxInertia );
		_viewPitchInertia = _smoothedViewPitchInertia.Clamp( -ViewMaxInertia, ViewMaxInertia );
	}

	private void UpdateSwayOffset()
	{
		Vector3 targetSway = new(
			-_viewYawInertia * SwayIntensity,
			_viewPitchInertia * SwayIntensity
		);

		SwayOffset = Vector3.Lerp( SwayOffset, targetSway, Time.Delta * SwayReturnSpeed );
		SwayOffset = SwayOffset.ClampLength( SwayMaxOffset );
	}

	private void UpdatePitchLean( float cameraPitch )
	{
		var absPitch = MathF.Abs( cameraPitch );
		var overThreshold = absPitch - MaxLeanAngle;

		// If outside the lean threshold, retain pitch lean
		if ( overThreshold > 60f )
		{
			var leanDir = cameraPitch > 0f ? 1f : -1f;
			_retainedPitchLean = overThreshold * leanDir * LeanLookBackMultiplier;
		}
		else
		{
			_retainedPitchLean = MathX.Lerp( _retainedPitchLean, 0f, Time.Delta * 1.5f );
		}

		var normalizedPitch = MathX.Clamp( cameraPitch / MaxLeanAngle, -1f, 1f );
		var pitchLean = normalizedPitch >= 0
			? normalizedPitch * PitchLeanMax
			: normalizedPitch * -PitchLeanMin;

		// Combine retained and regular lean
		var targetPitchLean = pitchLean + _retainedPitchLean;

		_aimPitchLeanSmoothed = MathX.Lerp( _aimPitchLeanSmoothed, targetPitchLean, Time.Delta * LeanLerpSpeed );
	}

	private void UpdateYawOffsetFromPitch( float cameraPitch )
	{
		_smoothedCameraPitch = MathX.Lerp( _smoothedCameraPitch, cameraPitch, Time.Delta * 10f );

		float targetYawOffset = 0f;
		if ( _smoothedCameraPitch > 0f )
		{
			float pitchUpFactor = (_smoothedCameraPitch / MaxLeanAngle).Clamp( 0f, 1f );
			targetYawOffset = DownwardPitchYawOffset * pitchUpFactor;
		}

		_smoothedYawOffset = MathX.Lerp( _smoothedYawOffset, targetYawOffset, Time.Delta * YawOffsetLerpSpeed );
	}

	private void UpdateMovementLean( Vector3 velocity, Vector3 cameraForward )
	{
		float proximity = WallProximityFactor( 60f ); // Distance check in inches

		proximity = MathX.Clamp( proximity, 0f, 1f );

		float normalizedDistance = 1f - proximity; // The closer you are, the smaller the value

		// Threshold for when to start applying wall proximity lean
		float threshold = ProximityThreshold;

		float targetWallLean = 0f;
		if ( normalizedDistance < threshold )
		{
			float invThreshold = 1f / MathF.Max( threshold, 0.0001f );
			float intensity = 1f - (normalizedDistance * invThreshold); // 1 at wall, 0 at threshold distance
			targetWallLean = -ProximityLeanFactor * threshold * 10f * intensity;
		}

		// Forward/backward movement lean (based on momentum relative to view forward)
		float targetMoveLean = 0f;
		var flatVel = velocity.WithZ( 0f );
		float speed = flatVel.Length;
		if ( speed > 0.01f )
		{
			var flatForward = cameraForward.WithZ( 0f );
			if ( flatForward.Length > 0.001f )
				flatForward = flatForward.Normal;
			else
				flatForward = Vector3.Forward;

			float forwardSpeed = flatVel.Dot( flatForward );
			float direction = Math.Clamp( forwardSpeed / speed, -1f, 1f ); // direction only
			float speedNorm = Math.Clamp( speed / MathF.Max( Controller?.RunSpeed ?? 320f, 1f ), 0f, 1f );
			float signedAmount = direction >= 0f
				? -ForwardLeanAmount * direction
				: BackwardLeanAmount * -direction;

			targetMoveLean = signedAmount * speedNorm;
		}

		float targetLean = targetWallLean + targetMoveLean;
		float lerpSpeed = (MathF.Abs( targetLean ) > 0.0001f) ? 10f : 6f;
		_aimPitchLean = MathX.Lerp( _aimPitchLean, targetLean, Time.Delta * lerpSpeed );

	}

	private void UpdateRollLean( Vector3 velocity, Vector3 cameraSide, Vector3 cameraForward, Angles deltaAngles )
	{
		float strafeRoll = velocity.Dot( cameraSide ) * StrafeDirectionRoll;
		float inertiaRoll = -_viewYawInertia * InertiaRollIntensity;
		float rotationRoll = ToReferenceFrameDelta( deltaAngles.yaw ) * RotationRollIntensity;

		float targetRoll = strafeRoll + inertiaRoll + rotationRoll;

		_smoothedRoll = MathX.Lerp( _smoothedRoll, targetRoll, Time.Delta * RollLerpSpeed );
		_smoothedRoll = MathX.Clamp( _smoothedRoll, -MaxTotalRoll, MaxTotalRoll );
	}

	private float WallProximityFactor( float maxDistance = 60f )
	{
		var ray = Local.Controller.AimRay;
		var forward = ray.Forward.WithZ( 0f );
		if ( forward.Length < 0.001f )
			forward = ray.Forward;
		else
			forward = forward.Normal;

		var trace = Scene.Trace.Ray( ray.Position, ray.Position + forward * maxDistance )
			.WithAnyTags( "solid" )
			.WithoutTags( "player" )
			.UseHitboxes( false )
			.UsePhysicsWorld( true )
			.Run();

		// Ensure that the trace hits only a wall, not the floor or ceiling
		if ( !trace.Hit || MathF.Abs( trace.Normal.z ) > 0.5f )
			return 0f;

		if ( MathF.Abs( trace.Normal.z ) > 0.5f )
			return 0f;

		// Calculate proximity value
		return Math.Clamp( 1f - (trace.Distance / maxDistance), 0f, 1f );
	}


	// ======== ViewBob ======== //

	// ViewBob control fields //	

	private float _bobTime = 0f;

	/// <summary> Lerp factor, we use this to reset lerping when the player stopped moving. </summary>
	private float _boblerpFactor = 0f;

	/// <summary> The current roll we're applying to our camera from strafing </summary>
	private float _currentStrafeRoll;

	/// <summary> The current roll we're applying to our camera from bobbing</summary>
	/// Field 'BaseViewmodel.currentBobRoll' is never assigned to, and will always have its default value 0
	//	private float currentBobRoll;

	private void UpdateViewBob()
	{
		if ( Local.LifeState == LifeState.Dead )
			return;


		if ( !(Local?.Controller).IsValid() || !Local.Controller.Camera.IsValid() )
			return;

		bool isOnGround = (Local?.Controller).Controller.IsOnGround;
		Vector3 velocity = (Local?.Controller).Controller.Velocity;
		Rotation eyeRot = (Local?.Controller).EyeAngles.ToRotation();
		Vector3 headPos = (Local?.Controller).Head.WorldPosition;
		Vector3 speed = Local.Movement.Velocity;
		float Velocity2D = speed.WithZ( 0 ).Length;

		float speedNorm = RemapSpeedNormalized(
			(Local?.Controller).CrouchSpeed,
			(Local?.Controller).WalkSpeed,
			(Local?.Controller).DefaultSpeed,
			(Local?.Controller).RunSpeed,
			Velocity2D
		);

		float bobStrength = (Velocity2D < 50f) ? 0f : BobAmplitudeCurve.Evaluate( speedNorm );
		float frequency = (Velocity2D < 50f) ? 0f : BobFrequencyCurve.Evaluate( speedNorm );

		//	Log.Info( $"camera.WorldPos: {camera.WorldPosition}" );
		//	Log.Info( $"headPos: {headPos}" );

		// Sync camera and head position
		Local.Controller.Camera.WorldPosition = headPos;

		// Strafe roll doing is here
		float targetStrafeRoll = CalculateStrafeRoll( eyeRot, velocity );
		_currentStrafeRoll = MathX.Lerp( _currentStrafeRoll, targetStrafeRoll, Time.Delta * StrafeRollLerpSpeed );

		if ( bobStrength > 0f && isOnGround )
		{
			_bobTime += Time.Delta * frequency;

			// Bobbing movement with lerpFactor controlling smoothness
			float verticalBob = MathF.Sin( _bobTime * 2f ) * bobStrength;
			float horizontalBob = MathF.Sin( _bobTime ) * BobHorizontalScale * bobStrength;
			float rollBob = MathF.Sin( _bobTime ) * BobRollScale * bobStrength;

			// Apply bob offset relative to current view rotation
			Vector3 offset = Local.Controller.Camera.WorldRotation.Up * verticalBob
							 + Local.Controller.Camera.WorldRotation.Right * horizontalBob;

			// Smoothly transition camera position based on bobbing
			Local.Controller.Camera.WorldPosition = Vector3.Lerp( Local.Controller.Camera.WorldPosition, headPos + offset, Time.Delta * 10f );
			Local.Controller.Camera.WorldRotation = (Local?.Controller).EyeAngles.ToRotation() * Rotation.From( 0, 0, rollBob );
		}
		else
		{
			// Reset bob time and lerp factor when not moving
			_bobTime = 0f;
			_boblerpFactor = MathX.Lerp( _boblerpFactor, 0f, Time.Delta * 8f );

			// Smoothly return to default camera position (no bob effect)
			Local.Controller.Camera.WorldPosition = Vector3.Lerp( Local.Controller.Camera.WorldPosition, headPos, Time.Delta * 10f );
			Local.Controller.Camera.WorldRotation = (Local?.Controller).EyeAngles.ToRotation();
		}

		// And then lets output it:
		Local.Controller.Camera.WorldRotation = eyeRot * Rotation.From( 0f, 0f, _currentStrafeRoll /*+ currentBobRoll*/ );
	}

	// ======== Recoil ======== //

	public static void ApplyPhysRecoil( BasePlayer owner )
	{
		var player = owner.Controller;
		var weaponData = owner.CurrentWeapon?.WeaponData;

		if ( !weaponData.IsValid() || Local.LifeState == LifeState.Dead )
			return;

		float strength = Math.Clamp( weaponData.RecoilPushForce, 0f, 10f );
		float easedStrength = EasingPlus.Linear( strength );

		float pitch = easedStrength;

		player.LocalEyeAngles = player.LocalEyeAngles.WithPitch(
			(player.LocalEyeAngles.pitch - pitch).Clamp( -89f, 89f )
		);
	}


	protected void ApplyVelocity()
	{
		if ( !Local.ViewmodelWeapon.IsValid() )
			return;

		var moveLen = Local.Movement.Velocity.Length;
		var wishMove = Local.Movement.WishVelocity.Normal * 1f;

		//	if ( Equipment.EquipmentFlags.HasFlag( EquipmentFlags.Aiming ) ) wishMove = 0;

		if ( Local.Controller.IsWalking || Local.Controller.IsCrouching )
			moveLen *= 0.5f;

		_lerpedWishMove = _lerpedWishMove.LerpTo( wishMove, Time.Delta * 7.0f );
		YawInertia += _lerpedWishMove.y * 10f;

		//	ModelRenderer?.Set( "move_bob", moveLen.Remap( 0, 300, 0, 1, true ) );
	}


	/// <summary>
	/// Send shit to all parts of viewmodel
	/// </summary>
	public void SetAllAnimgraphParams( string v, float value ) => ViewmodelWeapon?.Set( v, value );
	/// <summary>
	/// Send shit to all parts of viewmodel
	/// </summary>
	public void SetAllAnimgraphParams( string v, bool value ) => ViewmodelWeapon?.Set( v, value );
	/// <summary>
	/// Send shit to all parts of viewmodel
	/// </summary>
	public void SetAllAnimgraphParams( string v, int value ) => ViewmodelWeapon?.Set( v, value );
	/// <summary>
	/// Send shit to all parts of viewmodel
	/// </summary>
	public void SetAllWeaponModels( Model model ) => ViewmodelWeapon?.Model = model;

	/// <summary>
	/// Process all animtags from animgraph
	/// </summary>
	/// <param name="tag">Which tag</param>
	protected virtual void HandleAnimTag( SceneModel.AnimTagEvent tag )
	{
		if ( tag.Status != SceneModel.AnimTagStatus.End ) switch ( tag.Name )
			{
				case "mag_out":
					CurrentWeapon?.EventMagOut();
					break;
				case "mag_in":
					CurrentWeapon?.EventMagIn();
					break;
				case "bolt_release":
					CurrentWeapon?.EventBoltRelease();
					break;
				case "primary_fire":
					CurrentWeapon?.EventPrimaryFire();
					break;
				case "draw_finished":
					CurrentWeapon?.EventDrawFinished();
					break;
				case "reload_finished":
					CurrentWeapon?.EventReloadFinished();
					break;
				case "holster_finished":
					CurrentWeapon?.EventHolsterFinished();
					break;
				//	case "swap_mag":
				//	BasePlayer.Local.CurrentWeapon?.EventSwapMag();
				//	break;
				case "disallow_firing":
					CurrentWeapon?.EventDisallowFiring( true );
					break;
			}
		if ( tag.Status == SceneModel.AnimTagStatus.End ) switch ( tag.Name )
			{
				case "disallow_firing":
					CurrentWeapon?.EventDisallowFiring( false );
					break;
			}
	}

	protected virtual float GetViewmodelFOV() => ViewmodelFOVOverride > 0f ? ViewmodelFOVOverride : ViewmodelFOV;

	protected override void OnPreRender()
	{
		base.OnPreRender();

		if ( !IsControlledLocally ) // only on viewmodel you can see
			return;

		if ( !ViewmodelWeaponObject.IsValid() || !ViewmodelHands.IsValid() || !ViewmodelWeapon.IsValid() )
			return;

		if ( !ViewmodelWeaponObject.Active )    // because we toggle the viewmodel object, check if its active (enabled)
			return;

		var camera = Local.Controller?.Camera;
		if ( !camera.IsValid() )
			return;

		//		set attributes for viewmodel fov
		if ( ViewmodelWeapon.Model.IsValid() )
		{
			float vmFov = GetViewmodelFOV();

			ViewmodelWeapon.SceneModel.Attributes.Set( "vm_blend", ViewmodelBlend );
			ViewmodelWeapon.SceneModel.Attributes.Set( "cam_forward", GetEyeForward() );
			ViewmodelWeapon.SceneModel.Attributes.Set( "cam_fov", camera.FieldOfView );
			ViewmodelWeapon.SceneModel.Attributes.Set( "cam_pos", camera.WorldPosition );

			ViewmodelWeapon.SceneModel.Attributes.Set( "vm_fov", vmFov );

			ViewmodelHands.SceneModel.Attributes.Set( "vm_blend", ViewmodelBlend );
			ViewmodelHands.SceneModel.Attributes.Set( "cam_forward", GetEyeForward() );
			ViewmodelHands.SceneModel.Attributes.Set( "cam_fov", camera.FieldOfView );
			ViewmodelHands.SceneModel.Attributes.Set( "cam_pos", camera.WorldPosition );

			ViewmodelHands.SceneModel.Attributes.Set( "vm_fov", vmFov );
		}
	}


	// ==== Helper Methods ==== //
	public static float RemapClamped( float inMin, float inMax, float value )
	{
		if ( inMin == inMax ) return 0f;
		return Math.Clamp( (value - inMin) / (inMax - inMin), 0f, 1f );
	}

	private float CalculateStrafeRoll( Rotation viewRot, Vector3 velocity )
	{
		var flatVel = velocity.WithZ( 0 );
		if ( flatVel.Length < 0.001f ) return 0f;

		float sideSpeed = Vector3.Dot( flatVel.Normal, viewRot.Right );
		float speed = flatVel.Length;

		float roll = StrafeRollAngle;
		if ( speed < StrafeRollSpeed )
			roll *= speed / StrafeRollSpeed;

		return roll * sideSpeed;
	}

	private float RemapSpeedNormalized( float crouch, float walk, float normal, float run, float speed )
	{
		// Clamp to valid range
		speed = Math.Clamp( speed, crouch, run );

		// Map speed to a normalized [0,1] range across 4 tiers:
		// 0.0 = crouch, 0.33 = walk, 0.66 = normal, 1.0 = run

		if ( speed <= walk )
		{
			// From crouch to walk [0.0 -> 0.33]
			return RemapClamped( crouch, walk, speed ) * (1f / 3f);
		}
		else if ( speed <= normal )
		{
			// From walk to default [0.33 -> 0.66]
			return (1f / 3f) + RemapClamped( walk, normal, speed ) * (1f / 3f);
		}
		else
		{
			// From default to run [0.66 -> 1.0]
			return (2f / 3f) + RemapClamped( normal, run, speed ) * (1f / 3f);
		}
	}


}
