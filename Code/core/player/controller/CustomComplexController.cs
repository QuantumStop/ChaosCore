using System;
using Core;
using Sandbox.Internal;
using XMovement;

public partial class PlayerController : PlayerWalkControllerComplex
{
	[Property] private BasePawn _ownerPawn { get; set; }

	protected override void OnStart()
	{
		if ( _ownerPawn.IsControlledLocally )
		{
			SetupBody();
			SetupHead();
			SetupCamera();
		}
	}

	protected override void OnUpdate()
	{
		if ( Scene.IsEditor ) return;

		if ( AllowMovement )
		{
			if ( _ownerPawn.IsControlledLocally ) UpdateCamera();

			DoEyeLook();

			if ( _ownerPawn.IsControlledLocally )
			{
				BuildFrameInput();
				DoUsing();
			}

			if ( Controller.MovementFrequency == PlayerMovement.MovementFrequencyMode.PerUpdate ) DoMovement();

			Animate();
		}
		else
		{
			PositionHead();
		}
	}

	public override void BuildWishVelocity()
	{
		if ( _ownerPawn.IsControlledLocally ) base.BuildWishVelocity();
	}

	protected override void OnFixedUpdate()
	{
		if ( Scene.IsEditor ) return;

		if ( AllowMovement )
		{
			//			if ( _ownerPawn.IsControlledLocally )
			{
				UpdateCrouching();
			}

			if ( Controller.MovementFrequency == PlayerMovement.MovementFrequencyMode.PerFixedUpdate ) DoMovement();

			Animate();
		}
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

	/// <summary>
	/// Returns time since sprint was last requested
	/// </summary>
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

	/// <summary>
	/// Returns a value from 0-1 representing how much sprint is being wished for
	/// </summary>
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

	/// <summary>
	/// Main sensitivity, only a getter, if you want to modify this through game code use the Scale one
	/// </summary>
	public static float AimSensitivity => Input.UsingController ? GameSettings.Sensitivity.Controller * 0.5f : GameSettings.Sensitivity.Mouse;

	private static Angles HandleInvert( Angles look )
	{
		if ( Input.UsingController ? GameSettings.InvertCamera.PitchController : GameSettings.InvertCamera.PitchMouse ) look = look.WithPitch( -look.pitch );

		if ( Input.UsingController ? GameSettings.InvertCamera.YawController : GameSettings.InvertCamera.YawMouse ) look = look.WithYaw( -look.yaw );

		return look;
	}

	public override void DoEyeLook()
	{
		if ( _ownerPawn.IsControlledLocally )
		{
			LocalEyeAngles += HandleInvert( Input.AnalogLook ) * AimSensitivity * AimSensitivityScale;

			LocalEyeAngles = LocalEyeAngles.WithPitch( LocalEyeAngles.pitch.Clamp( -89f, 89f ) );
		}

		if ( _ownerPawn.IsPossessedLocally ) PositionHead();

	}

	private bool _isSprintDown { get; set; }

	private bool _isCrouchDown { get; set; }

	/// <summary>
	/// Used to toggle the state correctly based on desired mode
	/// </summary>
	private void HandleModes()
	{
		if ( Input.UsingController ? GameSettings.SprintMode.SprintController : GameSettings.SprintMode.SprintKeyboard )   // do super logic only in toggle mode
		{
			if ( Input.Pressed( RunAction ) )
			{
				if ( !_isSprintDown ) _isSprintDown = true;
				else _isSprintDown = false;
			}
		}
		else { _isSprintDown = Input.Down( RunAction ); } // otherwise do standard thing

		if ( Input.UsingController ? GameSettings.CrouchMode.CrouchController : GameSettings.CrouchMode.CrouchKeyboard )   // same but for crouch
		{
			if ( Input.Pressed( CrouchAction ) )
			{
				if ( !_isCrouchDown ) _isCrouchDown = true;
				else _isCrouchDown = false;
			}
		}
		else { _isCrouchDown = Input.Down( CrouchAction ); }
	}

	private bool _isMoving => (Input.AnalogMove != Vector3.Zero) && Controller.Velocity.Length > Controller.StopSpeed; // up for debate, probably just analogmove check is fine

	/// <summary>
	/// Crouching was forced through external means (a trigger or else)
	/// </summary>
	[Property, Feature( "Crouching" )] public bool ForceCrouch { get; set; } = false;

	protected override void BuildInput()
	{
		if ( !_ownerPawn.IsControlledLocally ) return;

		HandleModes();

		var run = _isSprintDown;
		var walk = false;
		var crouch = _isCrouchDown || ForceCrouch;

		if ( RunByDefault )
			IsRunning = !run && EnableRunning && CanRun && _isMoving;
		else
			IsRunning = run && EnableRunning && CanRun && _isMoving;

		IsWalking = walk && EnableWalking;
		IsCrouching = crouch || !CanUncrouch();

		if ( !_isMoving ) _isSprintDown = false;
	}
}
