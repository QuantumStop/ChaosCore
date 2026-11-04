namespace Core;

using Sandbox;
using System;

public partial class SpatialProbe
{
	[ConVar( "spatialprobe_debug", Help = "Spatial probe debug. 0=off, 1=text, 2=text+preview." )]
	public static int DebugMode { get; set; } = 0;

	public DebugTextBlock GetDebugText()
	{
		var block = new DebugTextBlock();

		if ( !Result.HasSample )
			return null;
			
		block.Header( "SPATIAL PROBE DEBUG" );
		block.Line( "Sampling / Arc", $"{Result.SamplingMode} / {Result.SampleAngle:0} deg", Color.White );
		block.Line( "Covered", $"{Result.IsCovered}", Color.Green );
		block.Line( "Overhead hits", $"{Result.OverheadHits} / {Result.OverheadSamples}", Color.Green );
		block.Line( "Compatible hits", $"{Result.CompatibleOverheadHits}", Color.Green );
		block.Line( "Coverage", $"{Result.OverheadCoverage:0.00}", Color.Green );
		block.Line( "Compatible cov.", $"{Result.CompatibleOverheadCoverage:0.00}", Color.Green );
		block.Line( "Ceiling depth", $"{Result.CeilingDepth:0.0}u", Color.Orange );
		block.Line( "Average ceiling", $"{Result.AverageCeilingDepth:0.0}u", Color.Orange );
		block.Line( "Sectors", $"{Result.OverheadSectors} / 4", Color.Cyan );
		block.Line( "Opposite", $"{Result.OppositeOverhead}", Color.Cyan );
		block.Line( "Radial hits", $"{Result.RadialHits} / {Result.RadialSamples}", Color.Green );
		block.Line( "Nearest wall", $"{Result.NearestWallDistance:0.0}u", Color.Orange );
		block.Line( "Average open", $"{Result.AverageOpenDistance:0.0}u", Color.Orange );
		block.Line( "Furthest open", $"{Result.FurthestOpenDistance:0.0}u", Color.Orange );
		block.Line( "Openness", $"{Result.Openness:0.00}", Color.Cyan );
		block.Line( "Enclosure", $"{Result.Enclosure:0.00}", Color.Cyan );
		block.Line( "Open direction", $"{Result.OpenDirection.x:0.0}, {Result.OpenDirection.y:0.0}, {Result.OpenDirection.z:0.0}", Color.Cyan );
		return block;
	}

	public void DrawDebugWorld()
	{
		if ( !Result.HasSample )
			return;

		var debug = DebugOverlaySystem.Current;

		debug.Sphere( new Sphere( Result.Origin, 4f ), Color.Cyan, 0f, overlay: true );

		for ( var i = 0; i < _overheadSamples.Length; i++ )
		{
			var sample = _overheadSamples[i];
			var color = sample.Hit ? sample.Compatible ? Color.Green : Color.Orange : Color.Red;

			debug.Line( new Line( sample.Start, sample.End ), color, 0f, overlay: true );
			debug.Sphere( new Sphere( sample.Start, 1.5f ), color, 0f, overlay: true );

			if ( !sample.Hit )
				continue;

			debug.Sphere( new Sphere( sample.End, 2.5f ), Color.Yellow, 0f, overlay: true );
			debug.Normal( sample.End, sample.Normal * 16f, Color.Yellow, 0f, overlay: true );
		}

		if ( _radialSamples.Length > 0 )
		{
			var step = Math.Max( _radialSamples.Length / 32, 1 );

			for ( var i = 0; i < _radialSamples.Length; i += step )
			{
				var sample = _radialSamples[i];
				debug.Line( new Line( Result.Origin, sample.End ), sample.Hit ? Color.Green : Color.Cyan, 0f, overlay: true );
			}
		}

		if ( Result.SamplingMode == SpatialProbeSamplingMode.Arc && Result.SampleAngle < 360f && _radialSamples.Length > 0 )
		{
			// Always show both sector edges, even when the ray preview is decimated.
			debug.Line( new Line( Result.Origin, _radialSamples[0].End ), Color.Orange, 0f, overlay: true );
			debug.Line( new Line( Result.Origin, _radialSamples[^1].End ), Color.Orange, 0f, overlay: true );
			debug.Line( new Line( Result.Origin, Result.Origin + Result.SampleDirection * 48f ), Color.Orange, 0f, overlay: true );
		}

		if ( Result.OpenDirection.Length > 0.001f )
			debug.Line( new Line( Result.Origin, Result.Origin + Result.OpenDirection * 64f ), Color.Yellow, 0f, overlay: true );
	}
}
