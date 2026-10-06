namespace Core;

using System;

public partial class PlayerController
{
	/// <summary>
	/// How movement is converted into world space direction while using the isometric camera.
	/// </summary>
	public enum IsometricMoveModes
	{
		[Icon( "explore" )]
		Compass,

		[Icon( "near_me" )]
		Facing,

		[Icon( "ads_click" )]
		AimHeld,
	}

	/// <summary>
	/// How the player should turn while using the isometric camera.
	/// </summary>
	public enum IsometricLookModes
	{
		[Icon( "block" )]
		Disabled,

		[Title( "Move Direction" ), Icon( "directions_run" )]
		MoveDirection,

		[Icon( "ads_click" )]
		AimDirection,
	}

	/// <summary>
	/// Which camera can we toggl from our isometric camera, if at all.
	/// </summary>
	public enum IsometricToggleModes
	{
		[Icon( "block" )]
		None,

		[Icon( "visibility" )]
		FirstPerson,

		[Icon( "accessibility_new" )]
		ThirdPerson,
	}


	[Space( 6 )]
	[Header( "Isometric Options" )]

	[Property, Title( "Camera Distance" ), Group( "Camera" ), Order( 103 )]
	[ShowIf( nameof( _showZoomOptions ), true )]
	public float IsometricCameraDistance { get; set; } = 1200f;

	[Property, Title( "Ortho Height" ), Group( "Camera" ), Order( 103 )]
	[ShowIf( nameof( IsIsometricCamera ), true )]
	public float IsometricOrthographicHeight { get; set; } = 512f;

	[Property, Title( "Offset" ), Group( "Camera" ), Order( 103 )]
	[ShowIf( nameof( IsIsometricCamera ), true )]
	public Vector3 IsometricOffset { get; set; } = new Vector3( -320f, -320f, 320f );


	[Property, Title( "Move" ), Group( "Camera" ), Order( 104 )]
	[ShowIf( nameof( IsIsometricCamera ), true )]
	public IsometricMoveModes IsometricMoveMode { get; set; } = IsometricMoveModes.Compass;

	[Property, Title( "Look" ), Group( "Camera" ), Order( 104 )]
	[ShowIf( nameof( IsIsometricCamera ), true )]
	public IsometricLookModes IsometricLookMode { get; set; } = IsometricLookModes.AimDirection;

	[Property, Title( "Toggle" ), Group( "Camera" ), Order( 104 )]
	[ShowIf( nameof( IsIsometricCamera ), true )]
	public IsometricToggleModes IsometricToggleMode { get; set; } = IsometricToggleModes.None;

	[Property, Title( "Turn Curve" ), Group( "Camera" ), Order( 104 )]
	[ShowIf( nameof( IsIsometricCamera ), true )]
	public Curve IsometricMoveLookTurnCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 0.12f, 1f ) );

	[Property, Title( "Max Collision Distance" ), Group( "Camera" ), Order( 105 )]
	[ShowIf( nameof( IsIsometricCamera ), true )]
	public float IsometricCameraMaxCollisionDistance { get; set; } = 4096f;

	[Property, Title( "Surface Clearance" ), Group( "Camera" ), Order( 105 )]
	[ShowIf( nameof( IsIsometricCamera ), true )]
	public float IsometricCameraSurfaceClearance { get; set; } = 24f;

	[Property, Title( "Collision Radius" ), Group( "Camera" ), Order( 105 )]
	[ShowIf( nameof( IsIsometricCamera ), true )]
	public float IsometricCameraCollisionRadius { get; set; } = 16f;


	[Space( 6 )]
	[Header( "Aim" )]

	[Property, Title( "Aim Action" ), InputAction, Group( "Camera" ), Order( 106 ), ShowIf( nameof( IsIsometricCamera ), true )]
	public string IsometricLookAction { get; set; } = "Attack2";

	[Property, Title( "Aim Override" ), Group( "Camera" ), Order( 106 ), ShowIf( nameof( IsIsometricCamera ), true )]
	public bool IsometricMoveLookCursorOverride { get; set; } = true;

	[Property, Title( "Hold To Aim" ), Group( "Camera" ), Order( 106 ), ShowIf( nameof( IsIsometricCamera ), true )]
	public bool IsometricLookRequiresAction { get; set; } = false;


	[Space( 6 )]
	[Header( "Zoom" )]

	[Property, Title( "Allow Zoom" ), Group( "Camera" ), Order( 107 )]
	[ShowIf( nameof( IsIsometricCamera ), true )]
	public bool EnableIsometricZoom { get; set; } = false;

	[Property, Title( "Min Height" ), Group( "Camera" ), Order( 107 )]
	[ShowIf( nameof( _showZoomOptions ), true )]
	public float IsometricZoomMinOrthographicHeight { get; set; } = 256f;

	[Property, Title( "Max Height" ), Group( "Camera" ), Order( 107 )]
	[ShowIf( nameof( _showZoomOptions ), true )]
	public float IsometricZoomMaxOrthographicHeight { get; set; } = 768f;

	[Property, Title( "Steps" ), Group( "Camera" ), Order( 107 ), Range( 1, 32 )]
	[ShowIf( nameof( _showZoomOptions ), true )]
	public int IsometricZoomSteps { get; set; } = 5;

	[Property, Title( "Zoom Curve" ), Group( "Camera" ), Order( 107 )]
	[ShowIf( nameof( _showZoomOptions ), true )]
	public Curve IsometricZoomTransitionCurve { get; set; } = Curve.Linear;


	public bool IsIsometricCamera => CameraMode == CameraModes.Isometric;

	private bool _showZoomOptions => IsIsometricCamera && EnableIsometricZoom;

	private bool _wasUsingIsometricCamera;
	private bool _orthographicBeforeIsometric;
	private int _isometricZoomStep = -1;
	private float _isometricZoomHeight;
	private float _isometricZoomStartHeight;
	private float _isometricZoomTargetHeight;
	private float _isometricZoomTransitionStart;
	private bool _isometricMoveLookTurnActive;
	private Rotation _isometricMoveLookTurnStartRotation;
	private Rotation _isometricMoveLookTurnTargetRotation;
	private float _isometricMoveLookTurnStartTime;

	private void UpdateIsometricCamera()
	{
		if ( !Camera.IsValid() || !Head.IsValid() )
			return;

		StoreCameraProjectionBeforeIsometric();
		UpdateIsometricZoom();

		var targetPosition = Head.WorldPosition;
		var offsetLength = IsometricOffset.Length;
		var cameraDirection = offsetLength > 0.001f ? IsometricOffset / offsetLength : new Vector3( -1f, -1f, 1f ).Normal;
		var desiredDistance = MathF.Max( offsetLength, IsometricCameraDistance );

		// For ortho we don't pull inward when something blocks the camera.
		// We check whether our camera point itself is embedded in geometry and, 
		// if needed we move it further along the same ray.
		var targetCameraPosition = ResolveIsometricCameraPosition( targetPosition, cameraDirection, desiredDistance );
		var targetCameraRotation = Rotation.LookAt( targetPosition - targetCameraPosition, Vector3.Up );

		Camera.Orthographic = true;
		Camera.OrthographicHeight = GetIsometricOrthographicHeight();
		Camera.WorldPosition = targetCameraPosition;
		Camera.WorldRotation = targetCameraRotation;
	}

	/// <summary>
	/// Toggles between the chosen base camera mode and the isometric camera mode.
	/// </summary>
	private bool UpdateIsometricCameraToggle()
	{
		if ( IsometricToggleMode == IsometricToggleModes.None ) return false;
		if ( !Input.Pressed( CameraToggleAction ) ) return false;

		if ( IsIsometricCamera )
		{
			CameraMode = GetIsometricToggleCameraMode();
			return true;
		}

		if ( CameraMode == GetIsometricToggleCameraMode() )
		{
			CameraMode = CameraModes.Isometric;
			return true;
		}

		return false;
	}

	private CameraModes GetIsometricToggleCameraMode() => IsometricToggleMode == IsometricToggleModes.FirstPerson ? CameraModes.FirstPerson : CameraModes.ThirdPerson;

	private Vector3 ResolveIsometricCameraPosition( Vector3 targetPosition, Vector3 cameraDirection, float desiredDistance )
	{
		var traceDistance = MathF.Max( desiredDistance, IsometricCameraMaxCollisionDistance );
		var traceEnd = targetPosition + cameraDirection * traceDistance;

		var hits = Scene.Trace.Sphere( IsometricCameraCollisionRadius, targetPosition, traceEnd )
			.IgnoreGameObjectHierarchy( GameObject )
			.IgnoreDynamic()
			.RunAll();

		float requiredDistance = desiredDistance;

		foreach ( var hit in hits )
		{
			if ( !hit.Hit )
				continue;

			var hitDistance = (hit.HitPosition - targetPosition).Length;

			if ( hitDistance > requiredDistance )
				requiredDistance = hitDistance + IsometricCameraSurfaceClearance;
		}

		return targetPosition + cameraDirection * requiredDistance;
	}

	private void DoIsometricLook()
	{
		if ( IsometricLookMode == IsometricLookModes.Disabled ) return;

		if ( IsometricLookMode == IsometricLookModes.MoveDirection )
		{
			if ( IsometricMoveLookCursorOverride && IsometricLookActionHeld() )
			{
				_isometricMoveLookTurnActive = false;

				FaceIsometricCursor();
				return;
			}

			FaceIsometricMoveDirection();
			return;
		}

		_isometricMoveLookTurnActive = false;

		if ( !IsometricCursorAimActive() ) return;
		FaceIsometricCursor();
	}

	private void FaceIsometricCursor()
	{
		if ( !TryGetIsometricCursorAimPoint( out var cursorAimPoint ) ) return;
		var lookDirection = (cursorAimPoint - Head.WorldPosition).WithZ( 0f );
		if ( lookDirection.Length < 0.001f ) return;

		var yaw = Rotation.LookAt( lookDirection.Normal, Vector3.Up ).Yaw();
		EyeAngles = new Angles( 0f, yaw, 0f );
	}

	/// <summary>
	/// Smooths move direction facing with a curve so diagonal transitions do not snap between nearby yaws.
	/// </summary>
	private void FaceIsometricMoveDirection()
	{
		var wishMove = Input.AnalogMove;
		
		if ( wishMove.Length < 0.001f ) 
			return;

		var wishDirection = wishMove.Normal * new Angles( 0f, GetIsometricCompassYaw(), 0f ).ToRotation();
		wishDirection = wishDirection.WithZ( 0f );
		
		if ( wishDirection.Length < 0.001f ) 
			return;

		var targetRotation = Rotation.LookAt( wishDirection.Normal, Vector3.Up );
		FaceIsometricTargetRotation( targetRotation );
	}

	private void FaceIsometricTargetRotation( Rotation targetRotation )
	{
		var currentRotation = EyeAngles.WithPitch( 0f ).ToRotation();
		if ( !_isometricMoveLookTurnActive || _isometricMoveLookTurnTargetRotation.Distance( targetRotation ) > 1f )
		{
			_isometricMoveLookTurnStartRotation = currentRotation;
			_isometricMoveLookTurnTargetRotation = targetRotation;
			_isometricMoveLookTurnStartTime = WorldTime.Now;
			_isometricMoveLookTurnActive = true;
		}

		var timeRange = IsometricMoveLookTurnCurve.TimeRange;
		var transitionTime = timeRange.y - timeRange.x;
		if ( transitionTime <= 0f )
		{
			EyeAngles = targetRotation.Angles().WithPitch( 0f );
			_isometricMoveLookTurnActive = false;
			return;
		}

		var elapsed = WorldTime.Now - _isometricMoveLookTurnStartTime;
		var t = elapsed.Clamp( 0f, transitionTime );
		var eased = IsometricMoveLookTurnCurve.Evaluate( timeRange.x + t );

		EyeAngles = Rotation.Slerp( _isometricMoveLookTurnStartRotation, _isometricMoveLookTurnTargetRotation, eased ).Angles().WithPitch( 0f );

		if ( elapsed >= transitionTime )
			_isometricMoveLookTurnActive = false;
		
	}

	/// <summary>
	/// Finds the world point under the cursor so isometric aiming can target crosshair position instead of camera forward.
	/// </summary>
	private bool TryGetIsometricCursorAimPoint( out Vector3 worldPosition )
	{
		worldPosition = default;

		if ( !TryGetIsometricCursorRay( out var ray ) ) return false;

		var tr = Scene.Trace.Ray( ray, 8192f )
			.IgnoreGameObjectHierarchy( GameObject )
			.UseHitPosition()
			.Run();

		if ( tr.Hit )
		{
			worldPosition = tr.HitPosition;
			return true;
		}

		return TryGetIsometricCursorWorldPosition( ray, out worldPosition );
	}

	private bool TryGetIsometricCursorWorldPosition( Ray ray, out Vector3 worldPosition )
	{
		worldPosition = default;

		if ( MathF.Abs( ray.Forward.z ) < 0.001f ) return false;

		var distance = (Head.WorldPosition.z - ray.Position.z) / ray.Forward.z;
		if ( distance < 0f ) 
			return false;

		worldPosition = ray.Position + ray.Forward * distance;
		return true;
	}

	internal bool TryGetIsometricCursorRay( out Ray ray )
	{
		ray = default;

		if ( !Head.IsValid() || Game.ActiveScene?.Camera is not { } sceneCamera )
			return false;

		var cursorPosition = Crosshair.CalculateIsometricCursorPosition( Screen.Size );
		ray = sceneCamera.ScreenPixelToRay( cursorPosition );
		return true;
	}

	private void UpdateIsometricZoom()
	{
		if ( !EnableIsometricZoom )
		{
			ResetIsometricZoomState();
			return;
		}

		var steps = Math.Max( 1, IsometricZoomSteps );
		var minHeight = MathF.Min( IsometricZoomMinOrthographicHeight, IsometricZoomMaxOrthographicHeight );
		var maxHeight = MathF.Max( IsometricZoomMinOrthographicHeight, IsometricZoomMaxOrthographicHeight );

		if ( _isometricZoomStep < 0 || _isometricZoomStep >= steps )
		{
			_isometricZoomStep = GetNearestIsometricZoomStep( IsometricOrthographicHeight, minHeight, maxHeight, steps );
			_isometricZoomTargetHeight = GetIsometricZoomHeight( _isometricZoomStep, minHeight, maxHeight, steps );
			_isometricZoomHeight = _isometricZoomTargetHeight;
			_isometricZoomStartHeight = _isometricZoomTargetHeight;
		}

		var oldStep = _isometricZoomStep;
		_isometricZoomStep = Math.Clamp( _isometricZoomStep - Math.Sign( Input.MouseWheel.y ), 0, steps - 1 );
		if ( _isometricZoomStep != oldStep )
		{
			_isometricZoomStartHeight = GetIsometricOrthographicHeight();
			_isometricZoomTargetHeight = GetIsometricZoomHeight( _isometricZoomStep, minHeight, maxHeight, steps );
			_isometricZoomTransitionStart = WorldTime.Now;
		}

		UpdateIsometricZoomTransition();
	}

	private static int GetNearestIsometricZoomStep( float height, float minHeight, float maxHeight, int steps )
	{
		if ( steps <= 1 || MathF.Abs( maxHeight - minHeight ) < 0.001f ) return 0;

		var t = ((height - minHeight) / (maxHeight - minHeight)).Clamp( 0f, 1f );
		return Math.Clamp( (int)MathF.Round( t * (steps - 1) ), 0, steps - 1 );
	}

	private static float GetIsometricZoomHeight( int step, float minHeight, float maxHeight, int steps )
	{
		if ( steps <= 1 ) return minHeight;
		return minHeight.LerpTo( maxHeight, step / (float)(steps - 1) );
	}

	private float GetIsometricOrthographicHeight() => EnableIsometricZoom && _isometricZoomStep >= 0 ? _isometricZoomHeight : IsometricOrthographicHeight;

	private void UpdateIsometricZoomTransition()
	{
		var timeRange = IsometricZoomTransitionCurve.TimeRange;
		var transitionTime = timeRange.y - timeRange.x;

		if ( transitionTime <= 0f )
		{
			_isometricZoomHeight = _isometricZoomTargetHeight;
			return;
		}

		var t = (WorldTime.Now - _isometricZoomTransitionStart).Clamp( 0f, transitionTime );
		var eased = IsometricZoomTransitionCurve.Evaluate( timeRange.x + t );
		_isometricZoomHeight = _isometricZoomStartHeight.LerpTo( _isometricZoomTargetHeight, eased );
	}

	private void ResetIsometricZoomState()
	{
		_isometricZoomStep = -1;
		_isometricZoomHeight = IsometricOrthographicHeight;
		_isometricZoomStartHeight = IsometricOrthographicHeight;
		_isometricZoomTargetHeight = IsometricOrthographicHeight;
		_isometricZoomTransitionStart = WorldTime.Now;
	}

	private Rotation GetIsometricMovementRotation()
	{
		var useFacing = IsometricMoveMode == IsometricMoveModes.Facing || (IsometricMoveMode == IsometricMoveModes.AimHeld && IsometricLookActionHeld());
		return useFacing ? EyeAngles.WithPitch( 0f ).ToRotation() : new Angles( 0f, GetIsometricCompassYaw(), 0f ).ToRotation();
	}

	/// <summary>
	/// Special noclip handling when we're using our isometric view.
	/// </summary>
	private void DoIsometricNoclipMove()
	{
		Controller.IsOnGround = false;

		var wishMove = Input.AnalogMove;
		var wishVelocity = wishMove.Length > 0.001f
			? wishMove.Normal * GetIsometricMovementRotation() * GetWishSpeed() * NoclipSpeed
			: Vector3.Zero;

		if ( Input.Down( SwimUpAction ) ) wishVelocity = wishVelocity.WithZ( 300 );
		if ( Input.Down( SwimDownAction ) ) wishVelocity = wishVelocity.WithZ( -300 );

		Controller.WishVelocity = wishVelocity;
		Controller.Acceleration = NoclipAcceleration;
		Controller.Accelerate( Controller.WishVelocity );
		Controller.Velocity = Controller.ApplyFriction( Controller.Velocity, 4, 100 );
		WorldPosition += Controller.Velocity * Time.Delta;
	}

	private float GetIsometricCompassYaw()
	{
		if ( IsometricOffset.WithZ( 0f ).Length < 0.001f )
			return EyeAngles.yaw;

		var cameraForward = (-IsometricOffset).WithZ( 0f ).Normal;
		return Rotation.LookAt( cameraForward, Vector3.Up ).Yaw();
	}

	private bool IsometricCursorAimActive() => IsometricLookMode == IsometricLookModes.AimDirection && (!IsometricLookRequiresAction || IsometricLookActionHeld());

	private bool IsometricLookActionHeld() => !string.IsNullOrEmpty( IsometricLookAction ) && Input.Down( IsometricLookAction );

	/// <summary>
	/// Restores whichever projection the base camera expects after leaving isometric.
	/// </summary>
	private void ResetBaseCameraProjection()
	{
		if ( !Camera.IsValid() ) return;

		if ( _wasUsingIsometricCamera )
		{
			Camera.Orthographic = CameraMode == CameraModes.Manual && _orthographicBeforeIsometric;
			
			// Reset the isometric rotation only once when we return to a head relative camera.
			// Previously was done every frame and that overrode recoil and any camera shake, no longer the case!
			if ( CameraMode != CameraModes.Manual )
				Camera.LocalRotation = Rotation.Identity;

			ResetIsometricZoomState();
			_wasUsingIsometricCamera = false;
		}
		else if ( CameraMode != CameraModes.Manual )
			Camera.Orthographic = false;
	}

	/// <summary>
	/// Records the previous projection once, so camera setup can be restored if it was already orthographic.
	/// </summary>
	private void StoreCameraProjectionBeforeIsometric()
	{
		if ( _wasUsingIsometricCamera ) return;

		_orthographicBeforeIsometric = Camera.Orthographic;
		_wasUsingIsometricCamera = true;
	}
}
