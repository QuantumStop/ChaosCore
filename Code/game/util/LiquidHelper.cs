using System;
using System.Numerics;

namespace chaoscore;

// DONT put this into "editor" subfolder anywhere it thinks this is an editor project

[Category( "Rendering" )]
[Icon( "light_mode" )]
public sealed class LiquidHelper : Component
{

	[RequireComponent] static public ModelRenderer mdlrender { get; set; }

	float MaxWobble { get; set; } = 0.03f;
	float WobbleSpeedMove { get; set; } = 1f;
	float fillAmount { get; set; } = 0.5f;
	float Recovery { get; set; } = 1f;
	float Thickness { get; set; } = 1f;

	[Property]
	[Range( 0, 1 )]
	public float CompensateShapeAmount { get; set; }
	Mesh mesh { get; set; }
	Vector3 pos { get; set; }
	Vector3 lastPos { get; set; }
	Vector3 velocity { get; set; }
	Quaternion lastRot { get; set; }
	Vector3 angularVelocity { get; set; }
	float wobbleAmountX { get; set; }
	float wobbleAmountY { get; set; }
	float wobbleAmountToAddX { get; set; }
	float wobbleAmountToAddY { get; set; }
	float pulse { get; set; }
	float sinewave { get; set; }
	float time { get; set; } = 0.5f;
	Vector3 comp { get; set; }


	protected override void OnUpdate()
	{
		base.OnUpdate();

		float deltaTime = 0;
		time += deltaTime;

		if ( deltaTime != 0 )
		{
			// not sure this works
			if ( !mdlrender.IsValid() )
				return;

			// decrease wobble over time
			wobbleAmountToAddX = MathX.Lerp( wobbleAmountToAddX, 0, (deltaTime * Recovery) );
			wobbleAmountToAddY = MathX.Lerp( wobbleAmountToAddY, 0, (deltaTime * Recovery) );

			// make a sine wave of the decreasing wobble
			pulse = 2 * MathF.PI * WobbleSpeedMove;
			sinewave = MathX.Lerp( sinewave, MathF.Sin( pulse * time ), deltaTime * MathX.Clamp( velocity.Length + angularVelocity.Length, Thickness, 10 ) );

			wobbleAmountX = wobbleAmountToAddX * sinewave;
			wobbleAmountY = wobbleAmountToAddY * sinewave;

			// velocity
			velocity = (lastPos - WorldPosition) / deltaTime;

			angularVelocity = GetAngularVelocity( lastRot, WorldRotation );

			// add clamped velocity to wobble
			wobbleAmountToAddX += MathX.Clamp( (velocity.x + (velocity.y * 0.2f) + angularVelocity.y + angularVelocity.z) * MaxWobble, -MaxWobble, MaxWobble );
			wobbleAmountToAddY += MathX.Clamp( (velocity.y + (velocity.z * 0.2f) + angularVelocity.x + angularVelocity.z) * MaxWobble, -MaxWobble, MaxWobble );

			// send it to the shader
			mdlrender.SceneObject.Attributes.Set( "WobbleX", wobbleAmountX );
			mdlrender.SceneObject.Attributes.Set( "WobbleY", wobbleAmountY );

			// set fill amount
			UpdatePos( deltaTime );

			// keep last position
			lastPos = WorldPosition;
			lastRot = WorldRotation;
		}
	}

	void UpdatePos( float deltaTime )
	{

		Vector3 worldPos = WorldPosition;

		if ( CompensateShapeAmount > 0 )
		{
			// only lerp if not paused/normal update
			if ( deltaTime != 0 )
			{
				comp = Vector3.Lerp( comp, (worldPos - new Vector3( 0, 0, 0 )), deltaTime * 10 );
			}
			else
			{
				comp = (worldPos - new Vector3( 0, 0, 0 ));
			}

			pos = worldPos - WorldPosition - new Vector3( 0, fillAmount - (comp.y * CompensateShapeAmount), 0 );
		}
		else
		{
			pos = worldPos - WorldPosition - new Vector3( 0, fillAmount, 0 );
		}

		mdlrender.SceneObject.Attributes.Set( "fillAmount", pos );
	}

	Vector3 GetAngularVelocity( Quaternion foreLastFrameRotation, Quaternion lastFrameRotation )
	{
		var q = lastFrameRotation * Quaternion.Inverse( foreLastFrameRotation );

		// no rotation?
		// You may want to increase this closer to 1 if you want to handle very small rotations.
		// Beware, if it is too close to one your answer will be Nan
		if ( MathF.Abs( q.W ) > 1023.5f / 1024.0f )
			return Vector3.Zero;

		float gain;

		// handle negatives, we could just flip it but this is faster
		if ( q.W < 0.0f )
		{
			var angle = MathF.Acos( -q.W );
			gain = -2.0f * angle / (MathF.Sin( angle ) * Time.Delta);
		}
		else
		{
			var angle = MathF.Acos( q.W );
			gain = 2.0f * angle / (MathF.Sin( angle ) * Time.Delta);
		}

		Vector3 angularVelocity = new Vector3( q.X * gain, q.Y * gain, q.Z * gain );

		if ( float.IsNaN( angularVelocity.z ) )
		{
			angularVelocity = Vector3.Zero;
		}

		return angularVelocity;
	}
/*
	protected override void OnDirty()
	{
		// not sure this works
		if ( !mdlrender.IsValid() )
			return;

//		mdlrender.SceneObject.Attributes.Set( "BTintColor", TintB );
//		mdlrender.SceneObject.Attributes.Set( "CTintColor", TintC );

		base.OnDirty();
	}*/
}
