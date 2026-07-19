namespace SDK;

using Core;

partial class Player
{
	[ReadOnly, Property, Feature( "Debug" )]
	protected bool _isSuitZooming
	{
		get;
		set
		{
			if ( field == value ) return;
			field = value;

			if ( value is true )
			{
				CurrentWeapon?.Holster();
				Controller.AimSensitivityScale = (float)SuitZoomFOV / CurrentFOV; // for some reason this is backwards, i have no idea but it works
			}
			else
			{
				CurrentWeapon?.Draw();
				Controller.AimSensitivityScale = 1f;
			}
		}
	}

	public const int SuitZoomFOV = 27;

	private void HandleSuitZoom()
	{
		if ( HasSuit && LifeState == LifeState.Alive )
		{
			if ( Input.Pressed( "zoom" ) )
			{
				if ( SetFOV( this, SuitZoomFOV, 0.4f ) ) _isSuitZooming = true;
			}
			else if ( Input.Released( "zoom" ) )
			{
				if ( SetFOV( this, 0, 0.2f ) ) _isSuitZooming = false;
			}
		}
	}

	[Property, Feature( "Defines" )] public WorldInput WorldInput { get; set; }

	[ConVar( "access_halo" )]
	static public bool Halo2Crosshair { get; set; } = false;

	private void CalculateBob()
	{
		var camera = Local.Controller?.Camera;
		if ( !camera.IsValid() )
			return;

		var camRot = camera.WorldRotation;

		ViewmodelWeaponObject.WorldPosition =
			camera.WorldPosition
			+ camRot.Forward * GetViewmodelOffsetForward()
			+ camRot.Right * GetViewmodelOffsetRight()
			+ camRot.Up * GetViewmodelOffsetUp();

		ViewmodelWeaponObject.WorldRotation = camRot;

		if ( Halo2Crosshair )
		{
			ViewmodelWeaponObject.LocalPosition += new Vector3( 0, 0, 1 );
			ViewmodelWeaponObject.LocalRotation *= new Angles( 6, 0, 0 );
		}
	}

	protected override void CalculateFOV() => Controller.Camera.FieldOfView = CurrentFOV = Screen.CreateVerticalFieldOfView( GetFOV(), Screen.Aspect * 0.75f );
	// some kind of bug with the fov zoom makes this useless. inserting vert FOV directly is fine???????
	// protected override void GetFOV() => Screen.CreateVerticalFieldOfView( base.GetFOV(), Screen.Aspect * 0.75f );
	protected override float GetViewmodelFOV() => Screen.CreateVerticalFieldOfView( base.GetViewmodelFOV(), Screen.Aspect * 0.75f );
}
