namespace SDK;

using Core;

partial class Player
{
	[Property, Feature( "Defines" )] public WorldInput WorldInput { get; set; }

	[ConVar( "access_halo" )]
	static public bool Halo2Crosshair { get; set; } = false;

	private void CalculateBob()
	{
		if ( !PawnCamera.IsValid() )
			return;

		if ( ShouldDrawViewmodel() )
		{
			ViewmodelWeaponObject.WorldPosition =
				PawnCamera.WorldPosition
				+ PawnCamera.WorldRotation.Forward * GetViewmodelOffsetForward()
				+ PawnCamera.WorldRotation.Right * GetViewmodelOffsetRight()
				+ PawnCamera.WorldRotation.Up * GetViewmodelOffsetUp();

			ViewmodelWeaponObject.WorldRotation = PawnCamera.WorldRotation;

			if ( Halo2Crosshair )
			{
				ViewmodelWeaponObject.LocalPosition += new Vector3( 0, 0, 1 );
				ViewmodelWeaponObject.LocalRotation *= new Angles( 6, 0, 0 );
			}
		}
	}

	protected override void CalculateFOV() => Controller.Camera.FieldOfView = CurrentFOV = Screen.CreateVerticalFieldOfView( GetFOV(), Screen.Aspect * 0.75f );
	// some kind of bug with the fov zoom makes this useless. inserting vert FOV directly is fine???????
	// protected override void GetFOV() => Screen.CreateVerticalFieldOfView( base.GetFOV(), Screen.Aspect * 0.75f );
	protected override float GetViewmodelFOV() => Screen.CreateVerticalFieldOfView( base.GetViewmodelFOV(), Screen.Aspect * 0.75f );
}
