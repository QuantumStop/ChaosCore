using System;

namespace Core;

public partial class BasePlayer
{
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
	protected virtual string GetViewmodel() { return ""; }
	/// <summary>
	/// Vertex colors for blending in/out the FOV correction
	/// </summary>
	/// <returns>The three colors as a whole</returns>
	protected virtual Vector3 GetViewmodelFovMask() { return Vector3.Up; }


	[ConVar( "viewmodel_fov" )]
	private static float ViewmodelFOV { get; set; } = 85f;

	private float lastPitch;
	private float lastYaw;

	[ReadOnly, Property, Feature( "Viewmodel" )] public float YawInertia { get; set; }
	[ReadOnly, Property, Feature( "Viewmodel" )] public float PitchInertia { get; set; }
	private Vector3 lerpedWishMove;

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


	[Group( "Viewmodel Roll" ), Property, Range( 0f, 10f ), Feature( "Viewmodel" )] public float StrafeRollIntensity { get; set; } = 0.35f;
	[Group( "Viewmodel Roll" ), Property, Range( 0f, 10f ), Feature( "Viewmodel" )] public float ForwardRollIntensity { get; set; } = 0.2f;
	[Group( "Viewmodel Roll" ), Property, Range( 0f, 10f ), Feature( "Viewmodel" )] public float InertiaRollIntensity { get; set; } = 0.1f;
	[Group( "Viewmodel Roll" ), Property, Range( 0f, 10f ), Feature( "Viewmodel" )] public float RotationRollIntensity { get; set; } = 0.25f;
	[Group( "Viewmodel Roll" ), Property, Range( 0f, 50f ), Feature( "Viewmodel" )] public float MaxTotalRoll { get; set; } = 15f;
	[Group( "Viewmodel Roll" ), Property, Range( 0f, 20f ), Feature( "Viewmodel" )] public float RollLerpSpeed { get; set; } = 6f;

	[Feature( "Debug" ), Property] public bool isDebug { get; set; } = false;


	// Need to do this here, as it doesn't get initialized properly in the curve itself
	private static float localCrouchSpeed => BasePlayer.Local?.Controller?.CrouchSpeed ?? 0f;
	private static float localWalkSpeed => BasePlayer.Local?.Controller?.WalkSpeed ?? 0f;
	private static float localDefaultSpeed => BasePlayer.Local?.Controller?.DefaultSpeed ?? 0f;
	private static float localRunSpeed => BasePlayer.Local?.Controller?.RunSpeed ?? 0f;

	[Group( "ViewBob" ), Property, Range( 0f, 50f ), Feature( "Viewmodel" )]
	public Curve BobAmplitudeCurve { get; set; } = new Curve(
	new[] {
		new Curve.Frame( localCrouchSpeed,  0.05f ),
		new Curve.Frame( localWalkSpeed,    0.15f ),
		new Curve.Frame( localDefaultSpeed, 0.35f ),
		new Curve.Frame( localRunSpeed,     1.0f )
		}
	);

	[Group( "ViewBob" ), Property, Feature( "Viewmodel" )]
	public Curve BobFrequencyCurve { get; set; } = new Curve(
	new[] {
		new Curve.Frame( localCrouchSpeed,  0.5f ),
		new Curve.Frame( localWalkSpeed,    1.0f ),
		new Curve.Frame( localDefaultSpeed, 1.5f ),
		new Curve.Frame( localRunSpeed,     2.0f )
		}
	);

	[Group( "ViewBob" ), Property, Range( 0f, 5f ), Feature( "Viewmodel" )] public float BobHorizontalScale { get; set; } = 1.25f;
	[Group( "ViewBob" ), Property, Range( 0f, 10f ), Feature( "Viewmodel" )] public float BobRollScale { get; set; } = 0.45f;

	[Group( "ViewRoll" ), Property, Range( 0f, 10f ), Feature( "Viewmodel" )] public float StrafeRollAngle { get; set; } = 5f;
	[Group( "ViewRoll" ), Property, Range( 0f, 500f ), Feature( "Viewmodel" )] public float StrafeRollSpeed { get; set; } = 350f;
	[Group( "ViewRoll" ), Property, Range( 0f, 20f ), Feature( "Viewmodel" )] public float StrafeRollLerpSpeed { get; set; } = 20f;

	[Group( "Viewmodel Offset" ), Property, Feature( "Viewmodel" )] public float ViewmodelOffsetForward { get; set; } = 2;
	[Group( "Viewmodel Offset" ), Property, Feature( "Viewmodel" )] public float ViewmodelOffsetRight { get; set; } = 0.2f;
	[Group( "Viewmodel Offset" ), Property, Feature( "Viewmodel" )] public float ViewmodelOffsetUp { get; set; } = 0f;


	/// <summary>
	/// To be able to externally override this from anywhere 
	/// </summary>
	public Vector3 ViewmodelBlend { get; set; } = Vector3.Up;

	protected virtual void ViewmodelFixedUpdate()
	{
		SetAllAnimgraphParams( "f_walking", Local.Movement.Velocity.Length / (Local.Controller.IsRunning ? 320 : 190) );
		SetAllAnimgraphParams( "b_sprint", Local.Controller.IsRunning );
		SetAllAnimgraphParams( "b_grounded", Controller.Controller.IsOnGround );

		ApplyInertia();
		ApplyVelocity();

		UpdateViewBob();
	}

	protected virtual void ViewmodelUpdate()
	{
		SetAllAnimgraphParams( "aim_pitch_inertia", -aimPitchLeanSmoothed + ViewPitchInertia * ViewmodelPitchInertiaScale );
		SetAllAnimgraphParams( "aim_yaw_inertia", ViewYawInertia * ViewmodelYawInertiaScale + smoothedYawOffset );
		SetAllAnimgraphParams( "aim_yaw", smoothedRoll );
		SetAllAnimgraphParams( "aim_pitch", aimPitchLean );

		UpdateViewmodelOffset();
		UpdateVisualRecoil();
	}

	void ApplyInertia()
	{
		var camera = Local.Controller.Camera;
		//	var inRot  = camera.WorldRotation;

		var newPitch = camera.WorldRotation.Pitch();
		var newYaw = camera.WorldRotation.Yaw();

		PitchInertia = Angles.NormalizeAngle( newPitch - lastPitch );
		YawInertia = Angles.NormalizeAngle( lastYaw - newYaw );

		lastPitch = newPitch;
		lastYaw = newYaw;
	}


	// ======== Viewmodel Offset ======== //

	// Viewmodel Offset control fields //	

	/// <summary>
	/// Stores the extra pitch lean when looking up/down past a specific threshold.
	/// This helps simulate more pronounced lean based on pitch changes.
	/// </summary>
	private float retainedPitchLean = 0f;

	/// <summary>
	/// Holds the smoothed roll value for the viewmodel's roll effect.
	/// This is used to make the roll transition smoother over time.
	/// </summary>
	private float smoothedRoll = 0f;

	/// <summary>
	/// Holds the smoothed pitch lean value for aiming. This is used to apply smooth changes when aiming.
	/// </summary>
	private float aimPitchLeanSmoothed = 0f;

	/// <summary>
	/// Holds the smoothed yaw offset derived from the camera's pitch.
	/// This helps smooth out the yaw during transitions when the pitch changes.
	/// </summary>
	private float smoothedYawOffset = 0f;

	/// <summary>
	/// Holds the smoothed camera pitch value, which is used for lean calculations during camera movement.
	/// </summary>
	private float smoothedCameraPitch = 0f;

	/// <summary>
	/// Holds the current yaw inertia from the camera's view rotation.
	/// This is used to simulate inertia effects when the camera rotates along the yaw axis.
	/// </summary>
	private float ViewYawInertia;

	/// <summary>
	/// Holds the current pitch inertia from the camera's view rotation.
	/// This value helps simulate inertia effects when the camera rotates along the pitch axis.
	/// </summary>
	private float ViewPitchInertia;

	/// <summary>
	/// Holds the target pitch lean value.
	/// This is the desired pitch lean that the system will gradually reach over time.
	/// </summary>
	private float aimPitchLean;

	/// <summary>
	/// Holds the smoothed yaw inertia for the sway effect.
	/// This value allows smoother transitions for the sway effect based on yaw rotation.
	/// </summary>
	private float smoothedViewYawInertia;

	/// <summary>
	/// Holds the smoothed pitch inertia for the sway effect.
	/// This value allows smoother transitions for the sway effect based on pitch rotation.
	/// </summary>
	private float smoothedViewPitchInertia;

	/// <summary>
	/// Holds the last frame's camera rotation, used for delta calculation in determining view rotation changes.
	/// </summary>
	private Rotation lastViewRot;

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

		var delta = lastViewRot.Inverse * currentViewRot;
		var deltaAngles = delta.Angles();

		lastViewRot = currentViewRot;

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

		smoothedViewYawInertia = MathX.Lerp( smoothedViewYawInertia, deltaAngles.yaw, t );
		smoothedViewPitchInertia = MathX.Lerp( smoothedViewPitchInertia, deltaAngles.pitch, t );

		ViewYawInertia = smoothedViewYawInertia.Clamp( -ViewMaxInertia, ViewMaxInertia );
		ViewPitchInertia = smoothedViewPitchInertia.Clamp( -ViewMaxInertia, ViewMaxInertia );
	}

	private void UpdateSwayOffset()
	{
		Vector3 targetSway = new(
			-ViewYawInertia * SwayIntensity,
			ViewPitchInertia * SwayIntensity
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
			retainedPitchLean = overThreshold * leanDir * LeanLookBackMultiplier;
		}
		else
		{
			retainedPitchLean = MathX.Lerp( retainedPitchLean, 0f, Time.Delta * 1.5f );
		}

		var normalizedPitch = MathX.Clamp( cameraPitch / MaxLeanAngle, -1f, 1f );
		var pitchLean = normalizedPitch >= 0
			? normalizedPitch * PitchLeanMax
			: normalizedPitch * -PitchLeanMin;

		// Combine retained and regular lean
		var targetPitchLean = pitchLean + retainedPitchLean;

		aimPitchLeanSmoothed = MathX.Lerp( aimPitchLeanSmoothed, targetPitchLean, Time.Delta * LeanLerpSpeed );
	}

	private void UpdateYawOffsetFromPitch( float cameraPitch )
	{
		smoothedCameraPitch = MathX.Lerp( smoothedCameraPitch, cameraPitch, Time.Delta * 10f );

		float targetYawOffset = 0f;
		if ( smoothedCameraPitch > 0f )
		{
			float pitchUpFactor = (smoothedCameraPitch / MaxLeanAngle).Clamp( 0f, 1f );
			targetYawOffset = DownwardPitchYawOffset * pitchUpFactor;
		}

		smoothedYawOffset = MathX.Lerp( smoothedYawOffset, targetYawOffset, Time.Delta * YawOffsetLerpSpeed );
	}

	private void UpdateMovementLean( Vector3 velocity, Vector3 cameraForward )
	{
		float proximity = WallProximityFactor( 60f ); // Distance check in inches

		proximity = MathX.Clamp( proximity, 0f, 1f );

		float proximityInfluence = 1f - proximity;  // The closer you are, the smaller the value

		// Threshold for when to start applying wall proximity lean
		float threshold = ProximityThreshold;

		if ( proximityInfluence < threshold )
		{
			float wallLean = -ProximityLeanFactor * proximityInfluence * 10f;

			aimPitchLean = MathX.Lerp( aimPitchLean, wallLean, Time.Delta * 10f );
		}
		else
			aimPitchLean = MathX.Lerp( aimPitchLean, 0f, Time.Delta * 6f );

	}

	private void UpdateRollLean( Vector3 velocity, Vector3 cameraSide, Vector3 cameraForward, Angles deltaAngles )
	{
		float strafeRoll = velocity.Dot( cameraSide ) * StrafeRollIntensity;
		float forwardRoll = velocity.Dot( cameraForward ) * ForwardRollIntensity;
		float inertiaRoll = -ViewYawInertia * InertiaRollIntensity;
		float rotationRoll = deltaAngles.yaw * RotationRollIntensity;

		float targetRoll = strafeRoll + forwardRoll + inertiaRoll + rotationRoll;

		smoothedRoll = MathX.Lerp( smoothedRoll, targetRoll, Time.Delta * RollLerpSpeed );
		smoothedRoll = MathX.Clamp( smoothedRoll, -MaxTotalRoll, MaxTotalRoll );
	}

	private float WallProximityFactor( float maxDistance = 60f )
	{
		var ray = BasePlayer.Local.Controller.AimRay;
		var trace = Scene.Trace.Ray( ray.Position, ray.Position + ray.Forward * maxDistance )
			.WithAnyTags( "solid" )
			.UseHitboxes( false )
			.UsePhysicsWorld( true )
			.Run();

		// Ensure that the trace hits only a wall, not the floor or ceiling
		if ( !trace.Hit || MathF.Abs( trace.Normal.z ) > 0.5f )
			return 0f;

		if ( MathF.Abs( trace.Normal.z ) > 0.5f )
			return 0f;

		// Calculate proximity value (returns between 0 and 1)
		return Math.Clamp( 1f - (trace.Distance / maxDistance), 0f, 1f );
	}


	// ======== ViewBob ======== //

	// ViewBob control fields //	

	private float bobTime = 0f;

	/// <summary> Lerp factor, we use this to reset lerping when the player stopped moving. </summary>
	private float boblerpFactor = 0f;

	/// <summary> The current roll we're applying to our camera from strafing </summary>
	private float currentStrafeRoll;

	/// <summary> The current roll we're applying to our camera from bobbing</summary>
	/// Field 'BaseViewmodel.currentBobRoll' is never assigned to, and will always have its default value 0
	//	private float currentBobRoll;

	private void UpdateViewBob()
	{
		if ( Local.LifeState == LifeState.Dead )
			return;

		var camera = BasePlayer.Local.Controller.Camera;
		var player = BasePlayer.Local;
		var controller = player?.Controller;

		if ( controller == null || camera == null )
			return;

		bool isOnGround = controller.Controller.IsOnGround;
		Vector3 velocity = controller.Controller.Velocity;
		Rotation eyeRot = controller.EyeAngles.ToRotation();
		Vector3 headPos = controller.Head.WorldPosition;
		Vector3 basePosition = player.WorldPosition + Vector3.Up * controller.HeadHeight;
		Vector3 speed = player.Movement.Velocity;
		float Velocity2D = speed.WithZ( 0 ).Length;

		float speedNorm = RemapSpeedNormalized(
			controller.CrouchSpeed,
			controller.WalkSpeed,
			controller.DefaultSpeed,
			controller.RunSpeed,
			Velocity2D
		);

		float bobStrength = (Velocity2D < 50f) ? 0f : BobAmplitudeCurve.Evaluate( speedNorm );
		float frequency = (Velocity2D < 50f) ? 0f : BobFrequencyCurve.Evaluate( speedNorm );

		// Sync camera and head position
		camera.WorldPosition = headPos;

		// Strafe roll doing is here
		float targetStrafeRoll = CalculateStrafeRoll( eyeRot, velocity );
		currentStrafeRoll = MathX.Lerp( currentStrafeRoll, targetStrafeRoll, Time.Delta * StrafeRollLerpSpeed );

		if ( bobStrength > 0f && isOnGround )
		{
			bobTime += Time.Delta * frequency;

			// Bobbing movement with lerpFactor controlling smoothness
			float verticalBob = MathF.Sin( bobTime * 2f ) * bobStrength;
			float horizontalBob = MathF.Sin( bobTime ) * BobHorizontalScale * bobStrength;
			float rollBob = MathF.Sin( bobTime ) * BobRollScale * bobStrength;

			// Apply bob offset relative to current view rotation
			Vector3 offset = camera.WorldRotation.Up * verticalBob
							 + camera.WorldRotation.Right * horizontalBob;

			// Smoothly transition camera position based on bobbing
			camera.WorldPosition = Vector3.Lerp( camera.WorldPosition, basePosition + offset, Time.Delta * 10f );
			camera.WorldRotation = controller.EyeAngles.ToRotation() * Rotation.From( 0, 0, rollBob );
		}
		else
		{
			// Reset bob time and lerp factor when not moving
			bobTime = 0f;
			boblerpFactor = MathX.Lerp( boblerpFactor, 0f, Time.Delta * 8f );

			// Smoothly return to default camera position (no bob effect)
			camera.WorldPosition = Vector3.Lerp( camera.WorldPosition, basePosition, Time.Delta * 10f );
			camera.WorldRotation = controller.EyeAngles.ToRotation();
		}

		// And then lets output it:
		camera.WorldRotation = eyeRot * Rotation.From( 0f, 0f, currentStrafeRoll /*+ currentBobRoll*/ );
	}

	// ======== Recoil ======== //

	// Recoil control fields //	
	private TimeSince timeSinceVisualRecoilStarted;

	/// <summary>
	/// The speed at which the camera rolls to the target. 
	/// Higher values result in faster roll transitions.
	/// </summary>
	private float rollSpeed = 100f;

	/// <summary>
	/// The maximum visual recoil roll in degrees. This defines how much the camera can tilt due to recoil.
	/// </summary>
	private float maxRollAngle = 10f;

	/// <summary>
	/// The actual roll applied to the camera during the current frame. This will be gradually adjusted to the target roll.
	/// </summary>
	private float currentRoll = 0f;

	/// <summary>
	/// The target roll value based on the most recent recoil event. The camera will roll towards this value.
	/// </summary>
	private float targetRoll = 0f;

	/// <summary>
	/// The direction of the roll. It can be either -1 (left) or 1 (right), based on the recoil's direction.
	/// </summary>
	private int rollDirection = 1;

	/// <summary>
	/// The current recoil strength. It determines the intensity of the recoil effect on the camera.
	/// </summary>
	private float currentRecoilStrength = 0f;

	/// <summary>
	/// The maximum strength the recoil curve can reach. This defines the peak of the recoil effect.
	/// </summary>
	private float maxCurveStrength = 1f;

	/// <summary>
	/// The total ramp-up time accumulated so far. This value is used to smoothly transition recoil effects over time.
	/// </summary>
	private float rampTime = 0f;

	/// <summary>
	/// The timer that tracks the time since the last recoil shot, used to determine the decay delay before the next recoil.
	/// </summary>
	private float recoilHoldTimer = 0f;

	private bool isRecoiling;
	private bool justStartedRecoil;

	public void StartVisualRecoil()
	{
		timeSinceVisualRecoilStarted = 0f;
		recoilHoldTimer = 0f;

		isRecoiling = true;
		justStartedRecoil = true;

		// Alternate recoil roll direction each shot
		rollDirection *= -1;

		var player = BasePlayer.Local;
		var weapon = player?.CurrentWeapon;
		var weaponData = weapon?.WeaponData;

		if ( weaponData == null || player.LifeState == LifeState.Dead )
			return;

		// Get the curve's max strength. TODO: Do this better, store as property maybe?
		CacheMaxCurveStrength( weaponData );

		rampTime += Time.Delta;
		rampTime = Math.Clamp( rampTime, 0f, weaponData.RecoilRampSpeed );
	}

	public void UpdateVisualRecoil()
	{
		if ( BasePlayer.Local.LifeState == LifeState.Dead )
			return;

		var player = BasePlayer.Local;
		var camera = player.Controller?.Camera;
		var weapon = player?.CurrentWeapon;
		var weaponData = weapon?.WeaponData;

		if ( weaponData == null || camera == null )
			return;

		if ( justStartedRecoil )
		{
			timeSinceVisualRecoilStarted = 0f;
			justStartedRecoil = false;
		}
		else
		{
			timeSinceVisualRecoilStarted += Time.Delta;
		}

		if ( isRecoiling )
		{
			rampTime += Time.Delta;
			rampTime = Math.Clamp( rampTime, 0f, weaponData.RecoilRampSpeed );

			recoilHoldTimer = 0f;
		}
		else
		{
			recoilHoldTimer += Time.Delta;

			if ( recoilHoldTimer > weaponData.RecoilResetThreshold )
			{
				rampTime -= Time.Delta;
				rampTime = Math.Max( rampTime, 0f ); // No negative ramp!
			}
		}

		// Normalized the ramp
		float rampT = Math.Clamp( rampTime / weaponData.RecoilRampSpeed, 0f, 1f );
		float targetStrength = weaponData.RecoilStrengthCurve.Evaluate(
			EasingPlus.EaseOutCubic( rampT )
		);

		// Clamp to curve's max strength
		targetStrength = Math.Clamp( targetStrength, 0f, maxCurveStrength );

		currentRecoilStrength = targetStrength * 0.1f;

		if ( isDebug )
			Log.Info( $"RecoilRamp Debug -> RampT: {rampT:F2}, Strength: {targetStrength:F2}, MaxCurve: {maxCurveStrength:F2}" );

		if ( isRecoiling )
		{
			rollDirection *= -1;

			float recoilImpulse = Game.Random.Float( 6f, 8f ) * currentRecoilStrength * rollDirection;

			// Clamping to curve's max value
			targetRoll = Math.Clamp( recoilImpulse, -maxRollAngle, maxRollAngle );
			currentRoll = MoveTowards( currentRoll, targetRoll, rollSpeed * Time.Delta );

			Angles clampedRecoil = new Angles( 0f, 0f, currentRoll );
			camera.WorldRotation *= Rotation.From( clampedRecoil );

			isRecoiling = false;
		}

		// Reset case/condition
		if ( timeSinceVisualRecoilStarted > weaponData.RecoilResetThreshold && MathF.Abs( currentRoll ) < 0.01f )
		{
			currentRoll = 0f;
			targetRoll = 0f;
			isRecoiling = false;
		}
	}


	public void ApplyPhysRecoil()
	{
		var player = BasePlayer.Local.Controller;
		var weapon = BasePlayer.Local.CurrentWeapon;
		var weaponData = weapon?.WeaponData;

		if ( weaponData == null || BasePlayer.Local.LifeState == LifeState.Dead )
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
		if ( !ViewmodelWeapon.IsValid() )
			return;

		var moveLen = BasePlayer.Local.Movement.Velocity.Length;
		var wishMove = BasePlayer.Local.Movement.WishVelocity.Normal * 1f;

		//	if ( Equipment.EquipmentFlags.HasFlag( EquipmentFlags.Aiming ) ) wishMove = 0;

		if ( BasePlayer.Local.Controller.IsWalking || BasePlayer.Local.Controller.IsCrouching )
			moveLen *= 0.5f;

		lerpedWishMove = lerpedWishMove.LerpTo( wishMove, Time.Delta * 7.0f );
		YawInertia += lerpedWishMove.y * 10f;

		//	ModelRenderer?.Set( "move_bob", moveLen.Remap( 0, 300, 0, 1, true ) );

	}


	/// <summary>
	/// Send shit to all parts of viewmodel
	/// </summary>
	public void SetAllAnimgraphParams( string v, float value )
	{
		ViewmodelWeapon.Set( v, value );
	}
	/// <summary>
	/// Send shit to all parts of viewmodel
	/// </summary>
	public void SetAllAnimgraphParams( string v, bool value )
	{
		ViewmodelWeapon.Set( v, value );
	}

	/// <summary>
	/// Send shit to all parts of viewmodel
	/// </summary>
	public void SetAllAnimgraphParams( string v, int value )
	{
		ViewmodelWeapon.Set( v, value );
	}
	/// <summary>
	/// Send shit to all parts of viewmodel
	/// </summary>
	public void SetAllWeaponModels( Model model )
	{
		ViewmodelWeapon.Model = model;
	}

	/// <summary>
	/// Process all animtags from animgraph
	/// </summary>
	/// <param name="tag">Which tag</param>
	protected virtual void HandleAnimTag( SceneModel.AnimTagEvent tag )
	{
		if ( tag.Status != SceneModel.AnimTagStatus.End ) switch ( tag.Name )
			{
				case "mag_out":
					Local.CurrentWeapon?.EventMagOut();
					break;
				case "mag_in":
					Local.CurrentWeapon?.EventMagIn();
					break;
				case "bolt_release":
					Local.CurrentWeapon?.EventBoltRelease();
					break;
				case "primary_fire":
					Local.CurrentWeapon?.EventPrimaryFire();
					break;
				case "draw_finished":
					Local.CurrentWeapon?.EventDrawFinished();
					break;
				case "reload_finished":
					Local.CurrentWeapon?.EventReloadFinished();
					break;
				case "holster_finished":
					Local.CurrentWeapon?.EventHolsterFinished();
					break;
				//	case "swap_mag":
				//	BasePlayer.Local.CurrentWeapon?.EventSwapMag();
				//	break;
				case "disallow_firing":
					Local.CurrentWeapon?.EventDisallowFiring( true );
					break;
			}
		if ( tag.Status == SceneModel.AnimTagStatus.End ) switch ( tag.Name )
			{
				case "disallow_firing":
					Local.CurrentWeapon?.EventDisallowFiring( false );
					break;
			}
	}

	protected override void OnPreRender()
	{
		base.OnPreRender();

		if ( IsProxy ) // only on viewmodel you can see
			return;

		if ( !ViewmodelWeaponObject.IsValid() || !ViewmodelHands.IsValid() || !ViewmodelWeapon.IsValid() )
			return;

		if ( !ViewmodelWeaponObject.Active )    // because we toggle the viewmodel object, check if its active (enabled)
			return;

		//		set attributes for viewmodel fov
		if ( ViewmodelWeapon.Model != null )
		{
			ViewmodelWeapon.SceneModel.Attributes.Set( "vm_blend", ViewmodelBlend );
			ViewmodelWeapon.SceneModel.Attributes.Set( "cam_forward", GetEyeForward() );
			ViewmodelWeapon.SceneModel.Attributes.Set( "cam_fov", Local.Controller.Camera.FieldOfView );
			ViewmodelWeapon.SceneModel.Attributes.Set( "cam_pos", GetEyePos() );

			ViewmodelWeapon.SceneModel.Attributes.Set( "vm_fov", ViewmodelFOV );

			ViewmodelHands.SceneModel.Attributes.Set( "vm_blend", ViewmodelBlend );
			ViewmodelHands.SceneModel.Attributes.Set( "cam_forward", GetEyeForward() );
			ViewmodelHands.SceneModel.Attributes.Set( "cam_fov", Local.Controller.Camera.FieldOfView );
			ViewmodelHands.SceneModel.Attributes.Set( "cam_pos", GetEyePos() );

			ViewmodelHands.SceneModel.Attributes.Set( "vm_fov", ViewmodelFOV );
		}
	}


	// ==== Helper Methods ==== //
	public static float RemapClamped( float inMin, float inMax, float value )
	{
		if ( inMin == inMax ) return 0f;
		return Math.Clamp( (value - inMin) / (inMax - inMin), 0f, 1f );
	}

	private void CacheMaxCurveStrength( WeaponParse weaponData )
	{
		maxCurveStrength = 0f;

		for ( float t = 0f; t <= 1f; t += 0.01f )
		{
			float value = weaponData.RecoilStrengthCurve.Evaluate( t );
			if ( value > maxCurveStrength )
				maxCurveStrength = value;
		}
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

	private float MoveTowards( float current, float target, float maxDelta )
	{
		if ( MathF.Abs( target - current ) <= maxDelta )
			return target;
		return current + MathF.Sign( target - current ) * maxDelta;
	}


}
