using System;

namespace Core.Voxels;

public partial class BaseVoxelVolume<T> where T : struct
{
	private Vector3[,,] _voxelWorldPositions;

	public Vector3 GetVoxelWorldPosition( Vector3Int g ) => _voxelWorldPositions[g.x, g.y, g.z];

	public void PrecomputeVoxelWorldPositions()
	{
		_voxelWorldPositions = new Vector3[DimX, DimY, DimZ];

		for ( int x = 0; x < DimX; x++ )
			for ( int y = 0; y < DimY; y++ )
				for ( int z = 0; z < DimZ; z++ )
				{
					_voxelWorldPositions[x, y, z] = VoxelToWorld( new Vector3Int( x, y, z ) );
				}
	}

	public void DebugDraw(
		Func<Vector3Int, T, Color?> colorFunc,
		bool solid = false,
		float currentRadius = float.MaxValue,
		int skip = 1
	)
	{
		float padding = 0.05f;
		float voxelDrawSize = VoxelSize * (1f - padding);
		float radiusSqr = currentRadius * currentRadius;

		var minVoxel = Vector3Int.Zero;
		var maxVoxel = new Vector3Int( DimX - 1, DimY - 1, DimZ - 1 );

		for ( int x = minVoxel.x; x <= maxVoxel.x; x += skip )
			for ( int y = minVoxel.y; y <= maxVoxel.y; y += skip )
				for ( int z = minVoxel.z; z <= maxVoxel.z; z += skip )
				{
					var g = new Vector3Int( x, y, z );
					var wpos = _voxelWorldPositions[x, y, z]; // <-- precomputed

					if ( (wpos - Center).LengthSquared > radiusSqr ) continue;

					var voxelValue = GetVoxel( g );
					var color = colorFunc( g, voxelValue );
					if ( !color.HasValue ) continue;

					var bbox = BBox.FromPositionAndSize( wpos, voxelDrawSize );
					VoxelDebugQueue.Enqueue( bbox, color.Value, solid, lifetime: 0.3f );
				}
	}



}
