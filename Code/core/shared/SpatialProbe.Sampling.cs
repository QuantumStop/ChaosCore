namespace Core;

using Sandbox;
using System;

public partial class SpatialProbe
{
	internal static float GetSampleAngle( SpatialProbeSettings settings ) => settings.SamplingMode == SpatialProbeSamplingMode.Arc
			? Math.Clamp( float.IsFinite( settings.ArcAngle ) ? settings.ArcAngle : 120f, 1f, 360f )
			: 360f;

	internal static Vector3 GetSampleDirection( SpatialProbeSettings settings, int index, int rayCount )
	{
		rayCount = Math.Clamp( rayCount, 8, 256 );
		index = Math.Clamp( index, 0, rayCount - 1 );
		
		var sweep = GetSampleAngle( settings );
		var center = 0f;
	
		if ( settings.SamplingMode == SpatialProbeSamplingMode.Arc )
		{
			var forward = settings.ArcDirection.WithZ( 0f );
			if ( float.IsFinite( forward.x ) && float.IsFinite( forward.y ) && forward.LengthSquared > 0.0001f )
				center = MathF.Atan2( forward.y, forward.x );
		}

		// Full circles are periodic, open arcs include both end rays.
		var angle = sweep >= 360f
			? center + index / (float)rayCount * MathF.PI * 2f
			: center + (-0.5f + index / (float)(rayCount - 1)) * sweep * MathF.PI / 180f;
		return new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f );
	}

	internal static Vector3 NormalizeArcDirection( Vector3 direction )
	{
		direction = direction.WithZ( 0f );
		
		return float.IsFinite( direction.x ) && float.IsFinite( direction.y ) && direction.LengthSquared > 0.0001f
			? direction.Normal : Vector3.Forward;
	}

	/// <summary>
	/// Tests a horizontal offset against an inclusive arc, including angles wider than 180 degrees.
	/// </summary>
	public static bool IsWithinArc( Vector3 offset, Vector3 direction, float angle )
	{
		angle = Math.Clamp( float.IsFinite( angle ) ? angle : 120f, 1f, 360f );
		offset = offset.WithZ( 0f );
		
		if ( angle >= 360f || offset.LengthSquared <= 0.0001f ) 
			return true;

		return Vector3.Dot( offset.Normal, NormalizeArcDirection( direction ) ) 
			>= MathF.Cos( angle * MathF.PI / 360f ) - 0.000001f;
	}
}
