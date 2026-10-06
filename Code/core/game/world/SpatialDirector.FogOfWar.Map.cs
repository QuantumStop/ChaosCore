namespace Core;

using Sandbox;
using System;

public sealed partial class SpatialDirector
{
	private Vector2 GetSafeMapSize() => new( MathF.Max( MathF.Abs( MapSize.x ), 1f ),
		MathF.Max( MathF.Abs( MapSize.y ), 1f ) );
	
	private Vector2Int WorldToCell( Vector2 world )
	{
		var size = GetSafeMapSize();
		var mapMin = MapCenter - size * 0.5f;
		var uv = (world - mapMin) / size;

		return new Vector2Int( (int)MathF.Floor( uv.x * _allocatedResolution ), 
			(int)MathF.Floor( uv.y * _allocatedResolution ) );
	}

	private bool IsCellValid( Vector2Int cell ) => cell.x >= 0 && cell.y >= 0 
		&& cell.x < _allocatedResolution && cell.y < _allocatedResolution;
	
	/// <summary>
	/// Checks if a world position is visible according to the fog of war.
	/// </summary>
	public bool IsVisible( Vector3 worldPosition )
	{
		if ( !IsWithinVisionArc( worldPosition, VisionWorldPosition ) )
			return false;

		if ( _visible is null )
			return true;

		var cell = WorldToCell( new Vector2( worldPosition.x, worldPosition.y ) );

		if ( !IsCellValid( cell ) )
			return false;

		var index = cell.y * _allocatedResolution + cell.x;

		return _visible[index] > 127;
	}

}
