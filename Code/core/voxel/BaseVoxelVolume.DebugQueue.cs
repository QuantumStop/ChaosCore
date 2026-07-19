namespace Core.Voxels;

public static class VoxelDebugQueue
{
	private struct DebugVoxel
	{
		public BBox Box;
		public Color Color;
		public bool Draw;
		public byte Flags;
		public float Lifetime;
		public float TotalLifetime;
	}

	private static readonly List<DebugVoxel> voxels = new();
	private static float lastLogTime = 0f;

	public static void Enqueue( BBox box, Color color, bool draw, byte flags = 0, float lifetime = 0.3f )
	{
		if ( !draw || box.Size.LengthSquared <= 0 ) return;

		voxels.Add( new DebugVoxel
		{
			Box = box,
			Color = color,
			Draw = draw,
			Flags = flags,
			Lifetime = lifetime,
			TotalLifetime = lifetime
		} );
	}

	public static void DrawAll( float deltaTime )
	{
		if ( voxels.Count == 0 ) return;

		if ( Time.Now - lastLogTime >= 0.10f )
		{
			Log.Info( $"[VoxelDebugQueue] Drawing {voxels.Count} voxels" );
			lastLogTime = Time.Now;
		}

		for ( int i = voxels.Count - 1; i >= 0; i-- )
		{
			var v = voxels[i];

			if ( !v.Draw || v.Box.Size.LengthSquared <= 0 )
			{
				voxels.RemoveAt( i );
				continue;
			}

			v.Lifetime -= deltaTime;
			if ( v.Lifetime <= 0 )
			{
				voxels.RemoveAt( i );
				continue;
			}

			bool fill = (v.Flags & 0x1) != 0;
			bool visible = (v.Flags & 0x2) != 0;
			bool blocked = (v.Flags & 0x4) != 0;

			if ( blocked )
			{
				voxels[i] = v;
				continue;
			}

			Color drawColor = fill ? Color.Red.WithAlpha( 0.3f )
								: visible ? Color.Yellow.WithAlpha( 0.3f )
								: Color.Gray.WithAlpha( 0.05f );

			Gizmo.Draw.Color = drawColor;
			if ( fill )
				Gizmo.Draw.SolidBox( v.Box );
			else
				Gizmo.Draw.LineBBox( v.Box );

			voxels[i] = v;
		}
	}

	public static void Clear()
	{
		if ( voxels.Count > 0 )
		{
			Log.Info( $"[VoxelDebugQueue] Clearing {voxels.Count} voxels" );
			voxels.Clear();
		}
	}
}
