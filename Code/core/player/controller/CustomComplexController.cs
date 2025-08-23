using System.Runtime.Serialization.Formatters;
using Sandbox.VR;
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

	/// <summary>
	/// Main sensitivity
	/// </summary>
	public float AimSensitivity
	{
		get => Input.UsingController ? GameSettings.Sensitivity.Controller * 0.5f : GameSettings.Sensitivity.Mouse;
		set => _sense = value;
	}

	/// <summary>
	/// This just has to exist, does nothing really
	/// </summary>
	private float _sense { get; set; }

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
			LocalEyeAngles += HandleInvert( Input.AnalogLook ) * AimSensitivity;

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

	public override void BuildInput()
	{
		HandleModes();

		var run = IsSprintDown;
		var walk = false;
		var crouch = IsCrouchDown;

		if ( RunByDefault )
			IsRunning = !run && EnableRunning;
		else
			IsRunning = run && EnableRunning;

		IsWalking = walk && EnableWalking;
		IsCrouching = crouch || !CanUncrouch();

		if ( !IsMoving )
			IsSprintDown = false;
	}
}
