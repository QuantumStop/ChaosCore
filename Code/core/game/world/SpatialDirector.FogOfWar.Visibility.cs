namespace Core;

using Sandbox;
using System;

public sealed partial class SpatialDirector
{
	/// <summary>
	/// Rebuilds the current visibility grid from the shared observer's position.
	/// </summary>
	private void UpdateVisibility()
	{
		if ( !Target.IsValid() || Scene is null || !_spatialProbe.Result.HasSample )
			return;

		var radialSamples = _spatialProbe.RadialSamples;

		if ( radialSamples.Count < 2 )
			return;

		Array.Clear( _visibilityScratch );

		var origin = _spatialProbe.Result.Origin;
		var radius = MathF.Max( VisionRadius, 1f );

		RasterizeVisibility( origin, radius );

		for ( var i = 0; i < _visible.Length; i++ )
			_visible[i] = _visibilityScratch[i] ? (byte)255 : (byte)0;
	}

	/// <summary>
	/// Tests each nearby tile against occluders to prevent visibility leaking through walls.
	/// </summary>
	private void RasterizeVisibility( Vector3 origin, float radius )
	{
		var resolution = _allocatedResolution;
		var size = GetSafeMapSize();
		var mapMin = MapCenter - size * 0.5f;
		var cellSize = new Vector2( size.x / resolution, size.y / resolution );

		var origin2D = new Vector2( origin.x, origin.y );
		var radius2D = new Vector2( radius, radius );

		var minCell = WorldToCell( origin2D - radius2D );
		var maxCell = WorldToCell( origin2D + radius2D );

		var minX = Math.Clamp( minCell.x, 0, resolution - 1 );
		var minY = Math.Clamp( minCell.y, 0, resolution - 1 );
		var maxX = Math.Clamp( maxCell.x, 0, resolution - 1 );
		var maxY = Math.Clamp( maxCell.y, 0, resolution - 1 );

		for ( var y = minY; y <= maxY; y++ )
		{
			for ( var x = minX; x <= maxX; x++ )
			{
				var world = new Vector2( mapMin.x + (x + 0.5f) * cellSize.x, 
					mapMin.y + (y + 0.5f) * cellSize.y );

				var delta = world - origin2D;
				var distance = delta.Length;

				if ( distance > radius || !IsWithinVisionArc( new Vector3( world.x, world.y, origin.z ), origin ) )
					continue;

				if ( !HasFogCellLineOfSight( origin, new Vector3( world.x, world.y, origin.z ) ) )
					continue;

				_visibilityScratch[y * resolution + x] = true;
			}
		}
	}

	/// <summary>
	/// Checks collision visibility independently of shader cutaway transparency.
	/// </summary>
	private bool HasFogCellLineOfSight( Vector3 origin, Vector3 point )
	{
		var trace = Scene.Trace.Ray( origin, point ).IgnoreGameObjectHierarchy( Target.GameObject );

		if ( WorldOnlyOcclusion )
			trace = trace.WithAnyTags( "world" );

		var hit = trace.Run();

		return !hit.Hit && !hit.StartedSolid;
	}
}
