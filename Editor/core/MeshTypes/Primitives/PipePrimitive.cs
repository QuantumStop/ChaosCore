
namespace Editor;

using System;
using Sandbox;
using Editor.MeshEditor;

[Title( "Pipe" ), Icon( "settings_input_svideo" )]
public sealed class PipePrimitive : PrimitiveBuilder
{
	[Title( "Sides" ), Range( 3, 64, slider: false ), Step( 1 ), WideMode]
	public int PipeSides { get; set; } = 16;

	[Title( "Thickness" ), Range( 0.01f, 128, slider: false ), Step( 1 ), WideMode]
	public float PipeThickness { get; set; } = 8.0f;

	[Title( "Align to Camera" ), WideMode]
	public bool AlignToCamera { get; set; } = true;

	[Hide] private BBox Bounds;

	public override void SetFromBox( BBox box ) => Bounds = box;

	public override void Build( PolygonMesh mesh )
	{
		const float minimumSectionSize = 0.01f;

		var size = Bounds.Size;
		var absoluteSize = new Vector3( MathF.Abs( size.x ), MathF.Abs( size.y ), MathF.Abs( size.z ) );

		if ( absoluteSize.x <= minimumSectionSize ||
			 absoluteSize.y <= minimumSectionSize ||
			 absoluteSize.z <= minimumSectionSize )
			return;

		var swapAxes = AlignToCamera && ShouldSwapAxes();

		var x0 = Bounds.Mins.x;
		var x1 = Bounds.Maxs.x;
		var y0 = Bounds.Mins.y;
		var y1 = Bounds.Maxs.y;
		var z0 = Bounds.Mins.z;
		var z1 = Bounds.Maxs.z;

		if ( swapAxes )
		{
			(x0, y0) = (y0, x0);
			(x1, y1) = (y1, x1);
		}

		var centerY = (y0 + y1) * 0.5f;
		var centerZ = (z0 + z1) * 0.5f;

		var radiusY = MathF.Abs( y1 - y0 ) * 0.5f;
		var radiusZ = MathF.Abs( z1 - z0 ) * 0.5f;

		var maximumThickness = MathF.Min( radiusY, radiusZ ) - minimumSectionSize;

		if ( maximumThickness <= minimumSectionSize )
			return;

		var thickness = Math.Clamp( PipeThickness, minimumSectionSize, maximumThickness );

		var innerRadiusY = radiusY - thickness;
		var innerRadiusZ = radiusZ - thickness;

		var segments = Math.Clamp( PipeSides, 3, 64 );

		void AddFace( params Vector3[] vertices )
		{
			if ( swapAxes )
			{
				for ( var i = 0; i < vertices.Length; i++ )
				{
					var v = vertices[i];
					vertices[i] = new Vector3( v.y, v.x, v.z );
				}

				Array.Reverse( vertices );
			}

			mesh.AddFace( vertices );
		}

		Vector3 Point( float x, float angle, bool inner )
		{
			var ry = inner ? innerRadiusY : radiusY;
			var rz = inner ? innerRadiusZ : radiusZ;

			return new Vector3( x, centerY + MathF.Cos( angle ) * ry, centerZ + MathF.Sin( angle ) * rz );
		}

		for ( var i = 0; i < segments; i++ )
		{
			var a0 = MathF.PI * 2.0f * i / segments;
			var a1 = MathF.PI * 2.0f * (i + 1) / segments;

			var o0f = Point( x0, a0, false );
			var o1f = Point( x0, a1, false );
			var o0b = Point( x1, a0, false );
			var o1b = Point( x1, a1, false );

			var i0f = Point( x0, a0, true );
			var i1f = Point( x0, a1, true );
			var i0b = Point( x1, a0, true );
			var i1b = Point( x1, a1, true );

			// Outer surface
			AddFace( o0f, o1f, o1b, o0b );

			// Inner surface
			AddFace( i0b, i1b, i1f, i0f );

			// Front cap
			AddFace( o0f, i0f, i1f, o1f );

			// Back cap
			AddFace( o1b, i1b, i0b, o0b );
		}
	}

	private static bool ShouldSwapAxes()
	{
		var viewport = SceneViewWidget.Current?.LastSelectedViewportWidget;
		if ( viewport?.State is null )
			return false;

		var forward = viewport.State.CameraRotation.Forward;
		return MathF.Abs( forward.y ) > MathF.Abs( forward.x );
	}
}
