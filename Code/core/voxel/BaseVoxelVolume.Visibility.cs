namespace Core.Voxels;

public partial class BaseVoxelVolume<T> where T : struct
{
	/// <summary>
	/// Flood-fill visibility from a point within the voxel volume.
	/// Marks voxels as visible using a provided setter action.
	/// </summary>
	public void ComputeVisibility( Vector3 origin, float radius, System.Action<Vector3Int> markVisible )
	{
		var startVoxel = WorldToVoxel( origin );
		var queue = new Queue<Vector3Int>();
		var visited = new HashSet<Vector3Int>();

		queue.Enqueue( startVoxel );
		visited.Add( startVoxel );

		while ( queue.Count > 0 )
		{
			var v = queue.Dequeue();
			var wpos = VoxelToWorld( v );

			// Skip if outside explosion radius
			if ( (wpos - origin).Length > radius ) continue;

			// Skip solid voxels (assume bit 0 is solid)
			dynamic voxelValue = GetVoxel( v );
			bool solid = false;
			if ( voxelValue is byte b ) solid = (b & 0x1) != 0;
			if ( solid ) continue;

			// Mark visible
			markVisible( v );

			// Enqueue neighbors
			foreach ( var n in GetNeighbors( v ) )
			{
				if ( !InBounds( n ) ) continue;
				if ( visited.Contains( n ) ) continue;
				visited.Add( n );
				queue.Enqueue( n );
			}
		}
	}

	/// <summary>
	/// Compute the fraction of a bounding box that is visible in the voxel volume.
	/// </summary>
	public float ComputeFractionVisible( Vector3 origin, BBox targetBox, int samplesPerAxis = 3 )
	{
		int total = 0;
		int visible = 0;

		var step = targetBox.Size / (samplesPerAxis - 1);

		for ( int x = 0; x < samplesPerAxis; x++ )
			for ( int y = 0; y < samplesPerAxis; y++ )
				for ( int z = 0; z < samplesPerAxis; z++ )
				{
					var samplePos = targetBox.Mins + new Vector3( x * step.x, y * step.y, z * step.z );

					var v = WorldToVoxel( samplePos );

					if ( !InBounds( v ) ) continue;

					dynamic voxelValue = GetVoxel( v );
					bool solid = false;
					if ( voxelValue is byte b ) solid = (b & 0x1) != 0;

					if ( !solid )
						visible++;

					total++;
				}

		return total == 0 ? 0f : (float)visible / total;
	}

	private IEnumerable<Vector3Int> GetNeighbors( Vector3Int v )
	{
		int[] offsets = { -1, 0, 1 };
		foreach ( var dx in offsets )
			foreach ( var dy in offsets )
				foreach ( var dz in offsets )
				{
					if ( dx == 0 && dy == 0 && dz == 0 ) continue;
					yield return new Vector3Int( v.x + dx, v.y + dy, v.z + dz );
				}
	}
}
