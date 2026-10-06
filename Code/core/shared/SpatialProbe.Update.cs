namespace Core;

using Sandbox;
using System;

public partial class SpatialProbe
{
	private void UpdateArc( SpatialProbeContext context ) => UpdateHorizontal( context, true );

	private void UpdateRadial( SpatialProbeContext context ) => UpdateHorizontal( context, false );

	private void UpdateHorizontal( SpatialProbeContext context, bool arc )
	{
		var rayCount = Math.Clamp( Settings.RadialRays, 8, 256 );
		var maximumDistance = MathF.Max( Settings.RadialDistance, 1f );
		var sampling = Settings;
		sampling.SamplingMode = arc ? SpatialProbeSamplingMode.Arc : SpatialProbeSamplingMode.Radial;

		if ( _radialSamples.Length != rayCount )
			_radialSamples = new SpatialProbeRadialSample[rayCount];

		var result = Result;
		result.RadialSamples = rayCount;
		result.SamplingMode = sampling.SamplingMode;
		result.SampleAngle = GetSampleAngle( sampling );

		// Store the sampled orientation independently of later settings changes!
		result.SampleDirection = arc ? NormalizeArcDirection( sampling.ArcDirection ) : Vector3.Forward;

		for ( var i = 0; i < rayCount; i++ )
		{
			var direction = GetSampleDirection( sampling, i, rayCount );
			var end = context.Origin + direction * maximumDistance;
			var trace = context.Scene.Trace.Ray( context.Origin, end );

			if ( context.IgnoreHierarchy.IsValid() )
				trace = trace.IgnoreGameObjectHierarchy( context.IgnoreHierarchy );

			if ( Settings.WorldOnly && !string.IsNullOrWhiteSpace( Settings.WorldTag ) )
				trace = trace.WithAnyTags( Settings.WorldTag );

			var hit = trace.Run();
			var blocked = hit.Hit || hit.StartedSolid;
			var distance = blocked ? (hit.EndPosition - context.Origin).WithZ( 0f ).Length : maximumDistance;
			var sampleEnd = blocked ? hit.EndPosition : end;

			_radialSamples[i] = new SpatialProbeRadialSample( direction, sampleEnd, distance, blocked );

			if ( blocked )
				result.RadialHits++;
		}

		result.RadialHitCoverage = result.RadialHits / (float)Math.Max( rayCount, 1 );
		Result = result;
	}

	private void UpdateDerivedContext()
	{
		var result = Result;

		if ( result.RadialSamples > 0 && _radialSamples.Length > 0 )
		{
			var minimum = float.MaxValue;
			var maximum = float.MinValue;
			var maximumIndex = 0;
			var total = 0f;

			for ( var i = 0; i < _radialSamples.Length; i++ )
			{
				var distance = _radialSamples[i].Distance;

				minimum = MathF.Min( minimum, distance );
				total += distance;

				if ( distance > maximum )
				{
					maximum = distance;
					maximumIndex = i;
				}
			}

			result.NearestWallDistance = minimum;
			result.FurthestOpenDistance = maximum;
			result.AverageOpenDistance = total / _radialSamples.Length;
			result.OpenDirection = _radialSamples[maximumIndex].Direction;
			result.Openness = Math.Clamp( result.AverageOpenDistance / MathF.Max( Settings.RadialDistance, 1f ), 0f, 1f );
		}

		var overheadWeight = Settings.OverheadEnabled ? MathF.Max( Settings.EnclosureOverheadWeight, 0f ) : 0f;
		var radialWeight = Settings.RadialEnabled ? MathF.Max( Settings.EnclosureRadialWeight, 0f ) : 0f;
		var totalWeight = overheadWeight + radialWeight;

		if ( totalWeight > 0.001f )
		{
			var overheadEnclosure = result.CompatibleOverheadCoverage;
			var radialEnclosure = 1f - result.Openness;
			result.Enclosure = Math.Clamp( (overheadEnclosure * overheadWeight + radialEnclosure * radialWeight) / totalWeight, 0f, 1f );
		}

		Result = result;
	}
}
