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
		if ( !Controller.BodyModelRenderer.Animation.IsValid ) return;

		var parameters = Controller.BodyModelRenderer.Animation.Parameters;
		var eye = GetEyeTransform();
		var aimRotation = Controller.BodyModelRenderer.WorldRotation.Inverse * eye.Rotation;
		var aimAngles = aimRotation.Angles();

		parameters.TrySet( "aim_body", aimRotation.Forward );
		parameters.TrySet( "aim_target", eye.Position + eye.Forward * 10000f );
		parameters.TrySet( "aim_body_pitch", aimAngles.pitch );
		parameters.TrySet( "aim_body_yaw", aimAngles.yaw );
		parameters.TrySet( "aim_body_weight", 1 );
		parameters.TrySet( "aim_head", aimRotation.Forward );
		parameters.TrySet( "aim_head_pitch", aimAngles.pitch );
		parameters.TrySet( "aim_head_yaw", aimAngles.yaw );
		parameters.TrySet( "aim_head_weight", 1 );
		parameters.TrySet( "aim_eyes", aimRotation.Forward );
		parameters.TrySet( "aim_eyes_weight", 1 );

		Controller.BodyModelRenderer.Animation.Parameters.TrySet( "holdtype", CurrentWeapon.IsValid() ? CurrentWeapon.WeaponData.HoldType.ToString() : "none" );
	}

	private void FeetMovement()
	{
		var dir = Controller.Controller.Velocity;
		var forward = Controller.BodyModelRenderer.WorldRotation.Forward.Dot( dir );
		var sideward = Controller.BodyModelRenderer.WorldRotation.Right.Dot( dir );

		Controller.BodyModelRenderer?.Animation.Parameters.TrySet( "move_groundspeed", dir.WithZ( 0 ).Length );
		Controller.BodyModelRenderer?.Animation.Parameters.TrySet( "move_x", forward );
		Controller.BodyModelRenderer?.Animation.Parameters.TrySet( "move_y", sideward );
		Controller.BodyModelRenderer?.Animation.Parameters.TrySet( "move_z", dir.z );

		if ( Controller is Core.PlayerController controller ) Controller.BodyModelRenderer?.Animation.Parameters.TrySet( "duck", controller.DuckRatio );

		Controller.BodyModelRenderer?.Animation.Parameters.TrySet( "wish_groundspeed", dir.WithZ( 0 ).Length );
		Controller.BodyModelRenderer?.Animation.Parameters.TrySet( "wish_x", forward );
		Controller.BodyModelRenderer?.Animation.Parameters.TrySet( "wish_y", sideward );
		Controller.BodyModelRenderer?.Animation.Parameters.TrySet( "wish_z", dir.z );
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
