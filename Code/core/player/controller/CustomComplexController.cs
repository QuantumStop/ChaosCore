namespace Core;

using System;
using XMovement;

[Icon( "Lightbulb" )]
public partial class PlayerController : PlayerWalkControllerComplex
{
	[Property] private BasePawn _ownerPawn { get; set; }

	protected override void OnStart()
	{
		_ownerPawn ??= Components.Get<BasePawn>();

		if ( _ownerPawn?.IsControlledLocally is true )
		{
			SetupBody();
			SetupHead();
		}
	}

	public override Ray AimRay
	{
		get
		{
			if ( IsIsometricCamera && TryGetIsometricCursorAimPoint( out var cursorAimPoint ) )
			{
				var direction = (cursorAimPoint - Head.WorldPosition).Normal;
				if ( direction.Length > 0.001f ) return new Ray( Head.WorldPosition, direction );
			}

			return base.AimRay;
		}
	}

	/// <summary>
	/// Keeps the base controller update intact, but routes camera, 
	/// look and movement through isometric helpers while we're in Isometric mode.
	/// </summary>
	protected override void OnUpdate()
	{
		// have to have nothing because base class has the input stuff that was moved to the interface in the player
	}

	public override void BuildWishVelocity()
	{
		if ( _ownerPawn?.IsControlledLocally is not true ) return;

		if ( !IsIsometricCamera )
		{
			base.BuildWishVelocity();
			return;
		}

		var wishMove = Input.AnalogMove;

		if ( wishMove.Length < 0.001f )
		{
			Controller.WishVelocity = Vector3.Zero;
			return;
		}

		var rot = GetIsometricMovementRotation();
		var wishDirection = wishMove.Normal * rot;
		Controller.WishVelocity = wishDirection.Normal * GetWishSpeed();
	}

	public override void DoNoclipMove()
	{
		if ( !IsIsometricCamera )
		{
			base.DoNoclipMove();
			return;
		}

		DoIsometricNoclipMove();
	}

	protected override void OnFixedUpdate()
	{
		if ( Scene.IsEditor ) return;

		WorldRotation = Rotation.Identity; // external forces can rotate the root gameobject which fuck up all sorts of worldrotation based tracing
	}

	public override float GetWishSpeed()
	{
		var speed = DefaultSpeed;
		if ( IsRunning ) speed = RunSpeed;
		if ( IsWalking ) speed = WalkSpeed;
		speed = speed.LerpTo( CrouchSpeed, DuckSpeedScale );
		return speed;
	}

	private float _timeSinceWishSprint = 0f;
	private float _wishSprint = 0f;
	public bool CanRun = true;

	/// <summary>Returns time since sprint was last requested</summary>
	public float TimeSinceWishSprint()
	{
		if ( _isSprintDown )
		{
			_timeSinceWishSprint = 0f;
		}
		else
		{
			_timeSinceWishSprint += Time.Delta;
			_timeSinceWishSprint = MathF.Min( _timeSinceWishSprint, 10f );
		}

		return _timeSinceWishSprint;
	}

	/// <summary>Returns a value from 0-1 representing how much sprint is being wished for</summary>
	public float WishSprint()
	{
		if ( _isSprintDown )
		{
			_wishSprint += Time.Delta / 2f;
			_wishSprint = MathF.Min( _wishSprint, 1f );
		}
		else
		{
			_wishSprint -= Time.Delta * 2f;
			_wishSprint = MathF.Max( _wishSprint, 0f );
		}

		return _wishSprint;
	}

	/// <summary>Main sensitivity, only a getter, if you want to modify this through game code use the Scale one</summary>
	public static float AimSensitivity => Input.UsingController ? GameSettings.Sensitivity.Controller * 0.5f : GameSettings.Sensitivity.Mouse;

	private static Angles HandleInvert( Angles look )
	{
		if ( Input.UsingController ? GameSettings.InvertCamera.PitchController : GameSettings.InvertCamera.PitchMouse ) look = look.WithPitch( -look.pitch );
		if ( Input.UsingController ? GameSettings.InvertCamera.YawController : GameSettings.InvertCamera.YawMouse ) look = look.WithYaw( -look.yaw );
		return look;
	}

	/// <summary>Input.AnalogLook but put through our custom keyboard/controller inversion logic and with applied sensitivity slider (both setting and gameplay scalar)</summary>
	/// <returns>Final local angles that you add onto your whatever uses it (which assuming the location of this functiion, usually is <see cref="PlayerWalkControllerComplex.LocalEyeAngles"/>)</returns>
	public static Angles CalculateLookInput() => HandleInvert( Input.AnalogLook ) * AimSensitivity * AimSensitivityScale;
	/// <summary>Clamp the camera input to "almost 90", so that looking straight up and walking forward actually has a forward, put this at the very end</summary>
	/// <param name="input">Your <see cref="PlayerWalkControllerComplex.LocalEyeAngles"/> (or equivalent), usually added together with <see cref="CalculateLookInput()"/></param>
	/// <returns>Final Eye Angles ready to be used in whatever</returns>
	public static Angles CalculateLookClamp( Angles input ) => input.WithPitch( input.pitch.Clamp( -89f, 89f ) );

	public override void DoEyeLook()
	{
		if ( _ownerPawn?.IsControlledLocally is true )
		{
			if ( IsIsometricCamera )
			{
				DoIsometricLook();
				LocalEyeAngles = LocalEyeAngles.WithPitch( 0f );
			}
			else
			{
				LocalEyeAngles += CalculateLookInput();
				LocalEyeAngles = CalculateLookClamp( LocalEyeAngles );
			}
		}

		PositionHead();
	}

	private bool _isSprintDown { get; set; }

	private bool _isCrouchDown { get; set; }

	/// <summary>Used to toggle the state correctly based on desired mode</summary>
	private void HandleModes()
	{
		// do super logic only in toggle mode
		if ( Input.UsingController ? GameSettings.SprintMode.SprintController : GameSettings.SprintMode.SprintKeyboard && Input.Pressed( RunAction ) ) _isSprintDown = !_isSprintDown;
		else _isSprintDown = Input.Down( RunAction ); // otherwise do standard thing

		// same for crouch
		if ( Input.UsingController ? GameSettings.CrouchMode.CrouchController : GameSettings.CrouchMode.CrouchKeyboard && Input.Pressed( CrouchAction ) ) _isCrouchDown = !_isCrouchDown;
		else _isCrouchDown = Input.Down( CrouchAction );
	}

	private bool _isMoving => (Input.AnalogMove != Vector3.Zero) && Controller.Velocity.Length > Controller.StopSpeed; // up for debate, probably just analogmove check is fine

	/// <summary>Crouching was forced through external means (a trigger or else)</summary>
	[Property, Feature( "Crouching" )] public bool ForceCrouch { get; set; } = false;

	protected override void BuildInput()
	{
		if ( _ownerPawn?.IsControlledLocally is false ) return;

		HandleModes();

		var run = _isSprintDown;
		var walk = false;
		var crouch = _isCrouchDown || ForceCrouch;

		IsRunning = RunByDefault ? !run && EnableRunning && CanRun && _isMoving : run && EnableRunning && CanRun && _isMoving;

		IsWalking = walk && EnableWalking;
		IsCrouching = crouch || !CanUncrouch();

		if ( !_isMoving ) _isSprintDown = false;
	}

	public override void OnCameraModeChanged()
	{
		if ( _ownerPawn?.IsPossessedLocally is false ) return;

		// Inherit the head rotation and discard offsets left by the previous view.
		// This fixes third person retaining roll from first person, making it widely offset when toggled.
		if ( Camera.IsValid() && (CameraMode == CameraModes.FirstPerson || CameraMode == CameraModes.ThirdPerson) ) Camera.LocalRotation = Rotation.Identity;

		if ( _ownerPawn is BasePlayer basePlayer )
		{
			if ( !Camera.IsValid() ) basePlayer?.ViewmodelVisible = false;

			basePlayer?.ViewmodelVisible = CameraMode == CameraModes.FirstPerson;
			basePlayer?.WorldmodelVisible = !basePlayer.ViewmodelVisible;

			basePlayer?.UpdateBodyVisibility();
		}
	}

	public void DoInput()
	{
		BuildWishVelocity();
		BuildInput();

		if ( Controller.IsOnGround && WantsJump && CanJump() ) Jump();
	}

	public override void DoMovement()
	{
		CheckWater();
		CheckLadder();
		CheckNoclip();

		if ( IsNoclipping )
		{
			DoNoclipMove();
		}
		else if ( _isTouchingLadder )
		{
			LadderMove();
		}
		else if ( IsSwimming )
		{
			SwimMove();
		}
		else
		{
			Controller.Move();
		}

		ResetFrameInput();

		LadderLatchCheck();
	}
}
