using System;

namespace Core.Voxels;

/// <summary>
/// Generic base voxel volume.
/// </summary>
public partial class BaseVoxelVolume<T> where T : struct
{
	public Vector3 Center;
	public float VoxelSize;
	public int DimX, DimY, DimZ;
	public Vector3 HalfExtents;

	protected T[] Voxels;

	public BaseVoxelVolume( Vector3 center, float voxelSize, int dimX, int dimY, int dimZ )
	{
		Center = center;
		VoxelSize = voxelSize;
		DimX = dimX;
		DimY = dimY;
		DimZ = dimZ;
		HalfExtents = new Vector3( dimX, dimY, dimZ ) * 0.5f * voxelSize;

		Voxels = new T[DimX * DimY * DimZ];
	}

	protected int FlattenIndex( int x, int y, int z ) => (z * DimY * DimX) + (y * DimX) + x;

	public bool InBounds( Vector3Int g ) =>
		g.x >= 0 && g.y >= 0 && g.z >= 0 &&
		g.x < DimX && g.y < DimY && g.z < DimZ;

	public Vector3Int WorldToVoxel( Vector3 wpos )
	{
		var local = wpos - (Center - HalfExtents);
		return new Vector3Int(
			(int)(local.x / VoxelSize),
			(int)(local.y / VoxelSize),
			(int)(local.z / VoxelSize)
		);
	}

	public Vector3 VoxelToWorld( Vector3Int g )
	{
		return Center - HalfExtents
			+ new Vector3( g.x + 0.5f, g.y + 0.5f, g.z + 0.5f ) * VoxelSize;
	}

	public T GetVoxel( Vector3Int g ) => Voxels[FlattenIndex( g.x, g.y, g.z )];
	public void SetVoxel( Vector3Int g, T value ) => Voxels[FlattenIndex( g.x, g.y, g.z )] = value;

	public void Reset( Func<T, T> resetFunc )
	{
		for ( int i = 0; i < Voxels.Length; i++ )
			Voxels[i] = resetFunc( Voxels[i] );
	}
}

