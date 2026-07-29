using Sandbox;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	[Property, Group( "Camera" )]
	public CameraModes CameraMode
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				SetupCamera();
			}
		}
	} = CameraModes.ThirdPerson;
	public enum CameraModes
	{
		FirstPerson,
		ThirdPerson,
		Manual,
	}

	[Property, Group( "Camera" ), ShowIf( nameof( CameraMode ), CameraModes.Manual )]
	public CameraComponent Camera { get; set; }


	[Property, Group( "Camera" ), ShowIf( nameof( CameraMode ), CameraModes.FirstPerson )]
	public bool PlayerShadowsOnly
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				SetupCamera();
			}
		}
	} = true;


	[Property, Group( "Camera" ), ShowIf( nameof( CameraMode ), CameraModes.FirstPerson )]
	public Vector3 FirstPersonOffset
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				SetupCamera();
			}
		}
	} = new Vector3( 0, 0, 0 );


	[Property, Group( "Camera" ), ShowIf( nameof( CameraMode ), CameraModes.ThirdPerson )]
	public Vector3 ThirdPersonOffset
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				SetupCamera();
			}
		}
	} = new Vector3( -180, 0, 0 );

	[Property, InputAction, Group( "Camera" )]
	public string CameraToggleAction { get; set; } = "View";

	public virtual void OnCameraModeChanged() { }
	public void SetupCamera()
	{
		if ( CameraMode != CameraModes.Manual && !Camera.IsValid() )
		{
			var cameraobj = Scene.CreateObject();
			cameraobj.SetParent( Head );
			cameraobj.Name = "Camera";
			Camera = cameraobj.AddComponent<CameraComponent>();
			Camera.Enabled = false;
			Camera.TargetEye = StereoTargetEye.Both;
		}
		if ( CameraMode == CameraModes.FirstPerson )
		{
			Camera.LocalPosition = FirstPersonOffset;
		}
		if ( CameraMode == CameraModes.ThirdPerson )
		{
			Camera.LocalPosition = ThirdPersonOffset;
		}

		OnCameraModeChanged();
	}

	public void UpdateCamera()
	{
		if ( CameraMode == CameraModes.ThirdPerson )
		{
			Camera.LocalPosition = ThirdPersonOffset * Scene.Trace.Ray( Head.WorldPosition, Head.WorldPosition + (ThirdPersonOffset * Head.WorldRotation) ).IgnoreDynamic().Run().Fraction;
		}
		if ( Input.Pressed( CameraToggleAction ) )
		{
			if ( CameraMode == CameraModes.ThirdPerson ) CameraMode = CameraModes.FirstPerson;
			else if ( CameraMode == CameraModes.FirstPerson ) CameraMode = CameraModes.ThirdPerson;
		}
	}

}
