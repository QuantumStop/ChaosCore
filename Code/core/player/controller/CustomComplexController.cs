using System;
using XMovement;

public partial class PlayerController : PlayerWalkControllerComplex
{
	public override float GetWishSpeed()
	{
		var speed = DefaultSpeed;
		if ( IsRunning ) speed = RunSpeed;
		if ( IsWalking ) speed = WalkSpeed;
		speed = speed.LerpTo( CrouchSpeed, DuckSpeedScale );
		return speed;
	}

	private float timeSinceWishSprint = 0f;
	private float wishSprint = 0f;
	public bool canRun;

	/// <summary>
	/// Returns time since sprint was last requested
	/// </summary>
	public float TimeSinceWishSprint()
	{
		if ( IsSprintDown )
		{
			timeSinceWishSprint = 0f;
		}
		else
		{
			timeSinceWishSprint += Time.Delta;
			timeSinceWishSprint = MathF.Min( timeSinceWishSprint, 10f );
		}

		return timeSinceWishSprint;
	}

	/// <summary>
	/// Returns a value from 0-1 representing how much sprint is being wished for
	/// </summary>
	public float WishSprint()
	{
		if ( IsSprintDown )
		{
			wishSprint += Time.Delta / 2f;
			wishSprint = MathF.Min( wishSprint, 1f );
		}
		else
		{
			wishSprint -= Time.Delta * 2f;
			wishSprint = MathF.Max( wishSprint, 0f );
		}

		return wishSprint;
	}

	/// <summary>
	/// Main sensitivity, only a getter, if you want to modify this through game code use the Scale one
	/// </summary>
	public static float AimSensitivity => Input.UsingController ? GameSettings.Sensitivity.Controller * 0.5f : GameSettings.Sensitivity.Mouse;

	private Angles HandleInvert( Angles look )
	{
		if ( Input.UsingController ? GameSettings.InvertCamera.PitchController : GameSettings.InvertCamera.PitchMouse )
			look = look.WithPitch( -look.pitch );

		if ( Input.UsingController ? GameSettings.InvertCamera.YawController : GameSettings.InvertCamera.YawMouse )
			look = look.WithYaw( -look.yaw );


		return look;
	}

	public override void DoEyeLook()
	{
		if ( !IsProxy )
		{
			LocalEyeAngles += HandleInvert( Input.AnalogLook ) * AimSensitivity * AimSensitivityScale;

			LocalEyeAngles = LocalEyeAngles.WithPitch( LocalEyeAngles.pitch.Clamp( -89f, 89f ) );

			//	if ( IsInVR )
			//	{
			//		EyeAngles = Input.VR.Head.Rotation.Angles();
			//	}

			PositionHead();
		}
	}

	private bool IsSprintDown { get; set; }

	private bool IsCrouchDown { get; set; }

	/// <summary>
	/// Used to toggle the state correctly based on desired mode
	/// </summary>
	private void HandleModes()
	{
		if ( Input.UsingController ? GameSettings.SprintMode.SprintController : GameSettings.SprintMode.SprintKeyboard )   // do super logic only in toggle mode
		{
			if ( Input.Pressed( RunAction ) )
			{
				if ( !IsSprintDown ) IsSprintDown = true;
				else IsSprintDown = false;
			}
		}
		else { IsSprintDown = Input.Down( RunAction ); } // otherwise do standard thing

		if ( Input.UsingController ? GameSettings.CrouchMode.CrouchController : GameSettings.CrouchMode.CrouchKeyboard )   // same but for crouch
		{
			if ( Input.Pressed( CrouchAction ) )
			{
				if ( !IsCrouchDown ) IsCrouchDown = true;
				else IsCrouchDown = false;
			}
		}
		else { IsCrouchDown = Input.Down( CrouchAction ); }
	}

	private bool IsMoving => (Input.AnalogMove != Vector3.Zero) && Controller.Velocity.Length > Controller.StopSpeed; // up for debate, probably just analogmove check is fine

	/// <summary>
	/// Crouching was forced through external means (a trigger or else)
	/// </summary>
	[Property, Feature( "Crouching" )] public bool ForceCrouch { get; set; } = false;

	protected override void BuildInput()
	{
		HandleModes();

		var run = IsSprintDown;
		var walk = false;
		var crouch = IsCrouchDown || ForceCrouch;

		if ( RunByDefault )
			IsRunning = !run && EnableRunning && canRun && IsMoving;
		else
			IsRunning = run && EnableRunning && canRun && IsMoving;

		IsWalking = walk && EnableWalking;
		IsCrouching = crouch || !CanUncrouch();

		if ( !IsMoving )
			IsSprintDown = false;
	}
}
