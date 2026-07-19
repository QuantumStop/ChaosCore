using Sandbox;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	[RequireComponent] public PlayerMovement Controller { get; set; }
	/// <summary>
	/// Disabling the component ruins EyeAngles, so we have to block it manually
	/// </summary>
	[Property, Hide] public bool AllowMovement { get; set; } = true;
	protected override void OnStart()
	{
		base.OnStart();
		if ( !IsProxy )
		{
			SetupBody();
			SetupHead();
			SetupCamera();
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Scene.IsEditor ) return;

		if ( AllowMovement )
		{
			Camera.Enabled = !IsProxy;

			if ( !IsProxy )
				UpdateCamera();

			DoEyeLook();

			if ( !IsProxy )
			{
				BuildFrameInput();
				DoUsing();
				if ( Controller.MovementFrequency == PlayerMovement.MovementFrequencyMode.PerUpdate ) DoMovement();
			}
			Animate();
		}
		else
		{
			PositionHead();
		}
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		if ( Scene.IsEditor ) return;

		if ( AllowMovement )
		{
			if ( !IsProxy )
			{
				UpdateCrouching();
				if ( Controller.MovementFrequency == PlayerMovement.MovementFrequencyMode.PerFixedUpdate ) DoMovement();
			}
			Animate();

			// HACK: For shitty networking purposes do this per FixedUpdate, we cant do this on start because new players wont ever fucking run that on join, nor any other function.
			UpdateBodyVisibility();
		}
		WorldRotation = Rotation.Identity; // external forces can rotate the root gameobject which fuck up all sorts of worldrotation based tracing
	}
}
