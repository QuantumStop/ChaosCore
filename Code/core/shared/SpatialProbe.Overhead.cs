namespace Core;

using Sandbox;
using System;

public partial class SpatialProbe
{
	private void UpdateOverhead( SpatialProbeContext context )
	{
		var ringSamples = Math.Clamp( Settings.OverheadRingSamples, 4, 32 );
		var sampleCount = ringSamples + 1;

		if ( _overheadSamples.Length != sampleCount )
			_overheadSamples = new SpatialProbeOverheadSample[sampleCount];

		var result = Result;
		result.OverheadSamples = sampleCount;

		_overheadSamples[0] = TraceOverhead( context, context.Origin );

		var radius = MathF.Max( Settings.OverheadRadius, 0f );

		for ( var i = 0; i < ringSamples; i++ )
		{
			var angle = i / (float)ringSamples * MathF.PI * 2f;
			var direction = new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f );
			_overheadSamples[i + 1] = TraceOverhead( context, context.Origin + direction * radius );
		}

		var referenceDepth = float.MaxValue;
		var referenceNormal = Vector3.Zero;

		for ( var i = 0; i < sampleCount; i++ )
		{
			var sample = _overheadSamples[i];

			if ( !sample.Hit )
				continue;

			result.OverheadHits++;

			if ( sample.Depth < referenceDepth )
			{
				referenceDepth = sample.Depth;
				referenceNormal = sample.Normal;
			}
		}

		result.OverheadCoverage = result.OverheadHits / (float)Math.Max( sampleCount, 1 );

		if ( referenceDepth == float.MaxValue )
		{
			Result = result;
			return;
		}

		result.CeilingDepth = referenceDepth;
		result.CeilingNormal = referenceNormal;

		var compatibleDepthSum = 0f;

		for ( var i = 0; i < sampleCount; i++ )
		{
			var sample = _overheadSamples[i];

			if ( !sample.Hit || !IsOverheadDepthCompatible( sample.Depth, referenceDepth ) )
				continue;

			_overheadSamples[i] = sample.WithCompatible( true );
			result.CompatibleOverheadHits++;
			compatibleDepthSum += sample.Depth;
		}

		if ( result.CompatibleOverheadHits > 0 )
			result.AverageCeilingDepth = compatibleDepthSum / result.CompatibleOverheadHits;

		result.CompatibleOverheadCoverage = result.CompatibleOverheadHits / (float)Math.Max( sampleCount, 1 );
		result.OppositeOverhead = HasOppositeOverheadCoverage( ringSamples );
		result.OverheadSectors = CountOccupiedOverheadSectors( ringSamples, 4 );

		result.IsCovered =
			result.CompatibleOverheadHits >= Math.Max( Settings.MinimumCompatibleOverheadHits, 1 ) &&
			(result.OppositeOverhead || result.OverheadSectors >= Math.Clamp( Settings.MinimumOverheadSectors, 1, 4 ));

		Result = result;
	}

	private SpatialProbeOverheadSample TraceOverhead( SpatialProbeContext context, Vector3 start )
	{
		var distance = MathF.Max( Settings.OverheadDistance, 8f );
		var end = start + Vector3.Up * distance;
		var trace = context.Scene.Trace.Ray( start, end );

		if ( context.IgnoreHierarchy.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( context.IgnoreHierarchy );

		if ( Settings.WorldOnly && !string.IsNullOrWhiteSpace( Settings.WorldTag ) )
			trace = trace.WithAnyTags( Settings.WorldTag );

		var hit = trace.Run();

		if ( !hit.Hit || hit.StartedSolid )
			return new SpatialProbeOverheadSample( start, end, Vector3.Zero, 0f, false, false );

		var depth = hit.EndPosition.z - start.z;

		if ( depth <= 4f )
			return new SpatialProbeOverheadSample( start, end, Vector3.Zero, 0f, false, false );

		return new SpatialProbeOverheadSample( start, hit.EndPosition, hit.Normal, depth, true, false );
	}

	private bool IsOverheadDepthCompatible( float depth, float referenceDepth ) => MathF.Abs( depth - referenceDepth ) <= MathF.Max( Settings.OverheadDepthTolerance, 0f );
	
	private bool HasOppositeOverheadCoverage( int ringSamples )
	{
		if ( ringSamples <= 0 || ringSamples % 2 != 0 )
			return false;

		var half = ringSamples / 2;

		for ( var i = 0; i < half; i++ )
		{
			if ( _overheadSamples[i + 1].Compatible && _overheadSamples[i + half + 1].Compatible )
				return true;
		}

		return false;
	}

	private int CountOccupiedOverheadSectors( int ringSamples, int sectorCount )
	{
		if ( ringSamples <= 0 )
			return 0;

		var occupied = 0;

		for ( var sector = 0; sector < sectorCount; sector++ )
		{
			var start = sector * ringSamples / sectorCount;
			var end = (sector + 1) * ringSamples / sectorCount;
			var found = false;

			for ( var i = start; i < end; i++ )
			{
				if ( _overheadSamples[i + 1].Compatible )
				{
					found = true;
					break;
				}
			}

			if ( found )
				occupied++;
		}

		return occupied;
	}
}
