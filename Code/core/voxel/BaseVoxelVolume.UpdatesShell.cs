using System;

namespace Core.Voxels;

public partial class BaseVoxelVolume<T> where T : struct
{
	// Map voxel indices to entities currently inside them
	private readonly Dictionary<Vector3Int, List<GameObject>> voxelOccupants = new();

	/// <summary>
	/// Update only voxels within the current explosion wave (shell), with optional padding to reduce voxel density.
	/// </summary>
	public void UpdateRegionShell(
		Vector3 origin,
		float currentRadius,
		float stepSize,
		Func<Vector3Int, Vector3, T, T> updateFunc,
		float voxelPadding = 0f // 0 = all voxels, 1 = skip every other voxel
	)
	{
		if ( Voxels == null ) return;

		Vector3 min = origin - new Vector3( currentRadius );
		Vector3 max = origin + new Vector3( currentRadius );

		Vector3Int minVoxel = WorldToVoxel( min );
		Vector3Int maxVoxel = WorldToVoxel( max );

		minVoxel.x = Math.Max( minVoxel.x, 0 );
		minVoxel.y = Math.Max( minVoxel.y, 0 );
		minVoxel.z = Math.Max( minVoxel.z, 0 );

		maxVoxel.x = Math.Min( maxVoxel.x, DimX - 1 );
		maxVoxel.y = Math.Min( maxVoxel.y, DimY - 1 );
		maxVoxel.z = Math.Min( maxVoxel.z, DimZ - 1 );

		float innerRadius = currentRadius - stepSize;
		float step = Math.Max( 1f, 1f + voxelPadding ); // multiplier for skipping voxels

		for ( int x = minVoxel.x; x <= maxVoxel.x; x++ )
			for ( int y = minVoxel.y; y <= maxVoxel.y; y++ )
				for ( int z = minVoxel.z; z <= maxVoxel.z; z++ )
				{
					// Apply padding spacing
					if ( (x % (int)step) != 0 || (y % (int)step) != 0 || (z % (int)step) != 0 )
						continue;

					var g = new Vector3Int( x, y, z );
					Vector3 wpos = VoxelToWorld( g );
					float dist = Vector3.DistanceBetween( wpos, origin );

					// Only update voxels in current shell
					if ( dist < innerRadius || dist > currentRadius ) continue;

					T prev = GetVoxel( g );
					T updated = updateFunc( g, wpos, prev );
					SetVoxel( g, updated );
				}
	}

	/// <summary>
	/// Compute visibility only for the current shell (wavefront), with optional padding.
	/// </summary>
	public void ComputeVisibilityShell(
		Vector3 origin,
		float currentRadius,
		float stepSize,
		Action<Vector3Int> markVisible,
		float voxelPadding = 0f
	)
	{
		if ( Voxels == null ) return;

		Vector3 min = origin - new Vector3( currentRadius );
		Vector3 max = origin + new Vector3( currentRadius );

		Vector3Int minVoxel = WorldToVoxel( min );
		Vector3Int maxVoxel = WorldToVoxel( max );

		minVoxel.x = Math.Max( minVoxel.x, 0 );
		minVoxel.y = Math.Max( minVoxel.y, 0 );
		minVoxel.z = Math.Max( minVoxel.z, 0 );

		maxVoxel.x = Math.Min( maxVoxel.x, DimX - 1 );
		maxVoxel.y = Math.Min( maxVoxel.y, DimY - 1 );
		maxVoxel.z = Math.Min( maxVoxel.z, DimZ - 1 );

		float innerRadius = currentRadius - stepSize;
		float step = Math.Max( 1f, 1f + voxelPadding );

		for ( int x = minVoxel.x; x <= maxVoxel.x; x++ )
			for ( int y = minVoxel.y; y <= maxVoxel.y; y++ )
				for ( int z = minVoxel.z; z <= maxVoxel.z; z++ )
				{
					if ( (x % (int)step) != 0 || (y % (int)step) != 0 || (z % (int)step) != 0 )
						continue;

					var g = new Vector3Int( x, y, z );
					Vector3 wpos = VoxelToWorld( g );
					float dist = Vector3.DistanceBetween( wpos, origin );

					if ( dist < innerRadius || dist > currentRadius )
						continue;

					markVisible( g );
				}
	}


	// Below stuff is unused for now. TODO: Evaluate

	/// <summary>
	/// Call this whenever an entity moves to update which voxel it occupies.
	/// </summary>
	public void UpdateEntityVoxel( GameObject obj, Vector3 worldPos )
	{
		var g = WorldToVoxel( worldPos );

		if ( !voxelOccupants.TryGetValue( g, out var list ) )
		{
			list = new List<GameObject>();
			voxelOccupants[g] = list;
		}

		if ( !list.Contains( obj ) )
			list.Add( obj );
	}

	/// <summary>
	/// Get all entities currently occupying the given voxel.
	/// </summary>
	public IEnumerable<GameObject> GetObjectsAtVoxel( Vector3Int voxel )
	{
		if ( voxelOccupants.TryGetValue( voxel, out var list ) )
			return list;

		return Enumerable.Empty<GameObject>();
	}

	public void ClearOccupants()
	{
		voxelOccupants.Clear();
	}
}

