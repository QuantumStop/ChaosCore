namespace SDK;

partial class Player
{
	private void AnimatePlayerModel()
	{
		BodyRotation();

		HeadLookAt();

		FeetMovement();
	}

	private void HeadLookAt()
	{
		Controller.BodyModelRenderer.Set( "HoldType", CurrentWeapon.IsValid() ? CurrentWeapon.WeaponData.HoldType.ToString() : "None" );
		Controller.BodyModelRenderer?.SetLookDirection( "aim_head", GetEyeAngles().Forward * 100, 1.0f );
	}

	private void FeetMovement()
	{
		var dir = Controller.Controller.Velocity;
		var forward = Controller.BodyModelRenderer.WorldRotation.Forward.Dot( dir );
		var sideward = Controller.BodyModelRenderer.WorldRotation.Right.Dot( dir );

		Controller.BodyModelRenderer?.Set( "move_groundspeed", dir.WithZ( 0 ).Length );
		Controller.BodyModelRenderer?.Set( "move_x", forward );
		Controller.BodyModelRenderer?.Set( "move_y", sideward );
		Controller.BodyModelRenderer?.Set( "move_z", dir.z );

		if ( Controller is Core.PlayerController controller ) Controller.BodyModelRenderer?.Set( "duck", controller.DuckRatio );

		Controller.BodyModelRenderer?.Set( "wish_groundspeed", dir.WithZ( 0 ).Length );
		Controller.BodyModelRenderer?.Set( "wish_x", forward );
		Controller.BodyModelRenderer?.Set( "wish_y", sideward );
		Controller.BodyModelRenderer?.Set( "wish_z", dir.z );
	}

	private float _animRotationSpeed;

	private void BodyRotation()
	{
		var targetAngle = new Angles( 0, GetEyeAngles().yaw, 0 ).ToRotation();

		var velocity = Controller.Controller.WishVelocity.WithZ( 0 );

		float rotateDifference = Controller.BodyModelRenderer.WorldRotation.Distance( targetAngle );

		// We're over the limit - snap it 
		if ( rotateDifference > 45 )
		{
			var delta = 0.999f - (45 / rotateDifference);
			var newRotation = Rotation.Lerp( Controller.BodyModelRenderer.WorldRotation, targetAngle, delta );

			var a = newRotation.Angles();
			var b = Controller.BodyModelRenderer.WorldRotation.Angles();

			var yaw = MathX.DeltaDegrees( a.yaw, b.yaw );

			_animRotationSpeed += yaw;
			_animRotationSpeed = _animRotationSpeed.Clamp( -90, 90 );

			Controller.BodyModelRenderer.WorldRotation = newRotation;
		}

		if ( velocity.Length > 10 )
		{
			var newRotation = Rotation.Slerp( Controller.BodyModelRenderer.WorldRotation, targetAngle, Time.Delta * 2.0f * velocity.Length.Remap( 0, 100 ) );

			var a = newRotation.Angles();
			var b = Controller.BodyModelRenderer.WorldRotation.Angles();

			var yaw = MathX.DeltaDegrees( a.yaw, b.yaw );

			_animRotationSpeed += yaw;
			_animRotationSpeed = _animRotationSpeed.Clamp( -90, 90 );

			Controller.BodyModelRenderer.WorldRotation = newRotation;
		}
	}
}
