using Sandbox.Utility;
using System;

namespace chaoscore;

partial class Player
{
	[ReadOnly, Property, Range( 0, 1 ), Feature( "Debug" )] public float fSuitZoom { get; set; } = 0.0f;
	[ReadOnly, Property, Feature( "Debug" ), Change( nameof( zoomChange ) )] protected bool isZoomedFully { get; set; }
	private void zoomChange() { if ( isZoomedFully ) CurrentWeapon?.Holster(); else CurrentWeapon?.Draw(); }
	[Property, Feature( "Defines" )] public Sandbox.UI.WorldInput worldInput { get; set; }

	private Vector3 TraceEnd = Vector3.Zero;

	[ConVar( "access_halo" )]
	static public bool Halo2Crosshair { get; set; } = false;

	/// <summary>
	/// Do PCV Zoom related stuff in the FixedUpdate of main file
	/// </summary>
	private void CalculateFOV()
	{
		var camera = Local.Controller.Camera;

		float targetZoom = Input.Down( "zoom" ) ? 1.0f : 0.0f;
		float zoomLerpSpeed = Input.Down( "zoom" ) ? 6f : 5f; // Separate speeds for in/out

		fSuitZoom = MathX.Lerp( fSuitZoom, targetZoom, Time.Delta * zoomLerpSpeed );

		float easedZoom = Easing.QuadraticInOut( fSuitZoom );

		// Field of view adjustment
		float zoomFov = 27.0f;
		camera.FieldOfView = DefaultFOV * (1.0f - easedZoom) + zoomFov * easedZoom;

		// Aim sensitivity
		(Controller as PlayerController).AimSensitivity = MathX.Lerp( 1f, 0.25f, easedZoom );

		// Handle weapon visibility.
		if ( fSuitZoom >= 0.3f )
			isZoomedFully = true;
		else
			isZoomedFully = false;
	}

	private void CalculateBob()
	{
		var camPos = Local.Controller.Camera.WorldPosition;
		var camRot = Local.Controller.Camera.WorldRotation;

		ViewmodelWeaponObject.WorldPosition =
			camPos
			+ camRot.Forward * ViewmodelOffsetForward
			+ camRot.Right * ViewmodelOffsetRight
			+ camRot.Up * ViewmodelOffsetUp;

		ViewmodelWeaponObject.WorldRotation = camRot;

		if ( Halo2Crosshair )
		{
			ViewmodelWeaponObject.LocalPosition += new Vector3( 0, 0, 1 );
			ViewmodelWeaponObject.LocalRotation *= new Angles( 6, 0, 0 );
		}
	}
}
