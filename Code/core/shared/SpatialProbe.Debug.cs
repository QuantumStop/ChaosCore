namespace Core;

using Sandbox;
using System;

public partial class SpatialProbe
{
	[ConVar( "spatialprobe_debug", Help = "Spatial probe debug. 0=off, 1=text, 2=text+preview." )]
	public static int DebugMode { get; set; } = 0;

	public void DrawDebug( Vector2 textPosition, bool force = false, CameraComponent camera = null )
	{
		if ( (!force && DebugMode <= 0) || !Result.HasSample )
			return;

		DrawDebugText( textPosition, camera );

		if ( force || DebugMode >= 2 )
			DrawDebugWorld();
	}

	public void DrawDebugText( Vector2 position, CameraComponent camera = null )
	{
		if ( !Result.HasSample )
			return;

		camera ??= Game.ActiveScene?.Camera;
		if ( !camera.IsValid() )
			return;

		var scope = new TextRendering.Scope
		{
			FontName = "RobotoMono",
			FontSize = 11f,
			FontWeight = 500,
			TextColor = Color.White,
			LineHeight = 0.85f
		};

		scope.Outline.Enabled = true;
		scope.Outline.Color = Color.Black;
		scope.Outline.Size = 3.25f;

		scope.Text += "SPATIAL PROBE\n\n";
		scope.Text += $"Sampling: {Result.SamplingMode} ({Result.SampleAngle:F0} deg)\n";
		scope.Text += $"Covered:         	{Result.IsCovered}\n";
		scope.Text += $"Overhead:        	{Result.OverheadHits}/{Result.OverheadSamples}\n";
		scope.Text += $"Compatible:     	{Result.CompatibleOverheadHits}\n";
		scope.Text += $"Coverage:       	{Result.OverheadCoverage:F2}\n";
		scope.Text += $"CompatibleCov:  	{Result.CompatibleOverheadCoverage:F2}\n";
		scope.Text += $"Ceiling:        	{Result.CeilingDepth:F1}\n";
		scope.Text += $"AverageCeiling: 	{Result.AverageCeilingDepth:F1}\n";
		scope.Text += $"Sectors:        	{Result.OverheadSectors}/4\n";
		scope.Text += $"Opposite:       	{Result.OppositeOverhead}\n";
		scope.Text += $"Sample hits:     	{Result.RadialHits}/{Result.RadialSamples}\n";
		scope.Text += $"NearestWall:    	{Result.NearestWallDistance:F1}\n";
		scope.Text += $"AverageOpen:    	{Result.AverageOpenDistance:F1}\n";
		scope.Text += $"FurthestOpen:   	{Result.FurthestOpenDistance:F1}\n";
		scope.Text += $"Openness:       	{Result.Openness:F2}\n";
		scope.Text += $"Enclosure:      	{Result.Enclosure:F2}\n";
		scope.Text += $"OpenDirection:  	{Result.OpenDirection}\n";

		using var painter = camera.BeginOverlay();
		painter.TextStyle = new TextStyle( scope.FontName, scope.FontSize, scope.TextColor )
		{
			FontWeight = scope.FontWeight,
			LineHeight = scope.LineHeight,
			Alignment = TextFlag.LeftTop
		}.WithOutline( Color.Black, 3.25f );
		painter.Text( scope.Text, new Rect( position, Screen.Size - position ) );
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
