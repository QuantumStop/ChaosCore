using System;
using XMovement;

public partial class PlayerController : PlayerWalkControllerComplex
{
	public float DuckRatio = 0.0f;
	public float DuckSpeedScale = 1.0f;
	public override void DoCrouching()
	{
		if ( IsCrouching )
		{
			if ( Controller.IsOnGround )
			{
				// normal duck
				DuckRatio = Math.Clamp( DuckRatio + Time.Delta / 0.3f, 0, 1 );
			}
			else
			{
				// jump duck, snap to fully ducked
				DuckRatio = 1.0f;
			}
			DuckSpeedScale = Math.Clamp( DuckRatio * 3.0f - 2.0f, 0, 1 );
		}
		else
		{
			if ( Controller.IsOnGround )
			{
				// unduck, being careful not to shove our head through anything
				// the sphere trace is annoyingly round, so we just use it to figure out where to do our actual (ray) trace
				// SceneTraceResult tr = Scene.Trace.Sphere( cc.Radius * 0.98f, WorldPosition + 28.0f * Vector3.Up, WorldPosition + 120.0f * Vector3.Up ).IgnoreGameObjectHierarchy( Body ).Run();
				if ( !CanUncrouch() )
				{
					// send a ray there
					// SceneTraceResult tr2 = Scene.Trace.Sphere( 5.0f, tr.HitPosition.WithZ( tr.StartPosition.z ), WorldPosition + 64.0f * Vector3.Up ).IgnoreGameObjectHierarchy( Body ).Run();
					// DuckRatio = Math.Min( DuckRatio, Math.Clamp( DuckRatio - Time.Delta / 0.2f, Math.Clamp( (1.0f - tr2.Fraction) * 2.0f, 0, 1 ), 1 ) );

					DuckRatio = Math.Clamp( DuckRatio - Time.Delta / 0.2f, 0, 1 );
				}
				else
				{
					DuckRatio = Math.Clamp( DuckRatio - Time.Delta / 0.2f, 0, 1 );
				}
			}
			else
			{
				//	jump unduck, which is instant in hl2 but here its 0.1 seconds to allow for some degree of precision when contacting the floor (moveto wont let us clip through the floor, but our unduck logic doesnt
				//	account for that so if moveto moves less than the code is expecting the player view snaps upwards by the difference, properly accounting for that would be slightly difficult, this is good enough)
				DuckRatio = Math.Clamp( DuckRatio - Time.Delta / 0.1f, 0, 1 );
			}
			DuckSpeedScale = Math.Clamp( DuckRatio * 3.0f, 0, 1 );
		}

		EyeHeightOffset = DuckRatio * -36f;
	}

	public override void UpdateCrouching()
	{
		DoCrouching();
		Controller.Height = Height + EyeHeightOffset;
		// This moves our feet up when crouching in air
		var delta = LastEyeHeightOffset - EyeHeightOffset;
		if ( !Controller.IsOnGround )
		{
			var delmove = delta;
			delmove *= WorldScale.z;

			var offset = Vector3.Up * delmove;
			if ( !IsNoclipping )
			{
				Controller.MoveTo( Controller.WorldPosition + offset, true );
			}
			else
			{
				Controller.WorldPosition += offset;
			}
		}

		Head.LocalPosition = new Vector3( 0, 0, HeadHeight + EyeHeightOffset );
		LastEyeHeightOffset = EyeHeightOffset;
	}


	public bool CanUncrouchAtHeight( float height )
	{
		var b = Controller.Height;
		if ( !IsCrouching ) return true;
		Controller.Height = height;
		var tr = Controller.TraceDirection( Vector3.Zero );
		Controller.Height = b;
		return !tr.Hit;
	}
}
