using System;
using System.Linq;


[Description( "Node of a Path." )]
[Icon( "polyline" )]
[Title( "Path" )]
public class PathSingle : BaseEntity, Component.ExecuteInEditor
{
	protected override string GetEditorVis() { return null; }

	public bool IsSelected { get; private set; }

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		var pos = WorldPosition + Vector3.Up * 0.4f;
		float dist = (pos - Gizmo.Camera.Position).Length;
		float scale = Math.Clamp( dist * 0.05f, 1.2f, 4.0f );

		Texture vtex;
		vtex = Texture.Load( "materials/tools/handle_edged_circle_tga_b183d0e4.generated.vtex_c" );

		if ( Gizmo.IsHovered ) scale = MathX.Lerp( scale, scale * 1.5f, 0.2f );

		switch ( Gizmo.IsSelected )
		{
			case true:

				var parentPos = GameObject.Parent.Transform.World.Position;
				var localOffset = Vector3.Up * 4f; // or your desired offset
				var correctedWorldPos = parentPos + localOffset;
				var name = GameObject.Name?.Replace( "PathPoint_", "" ) ?? "";

				Gizmo.Draw.Color = Color.White;

				Gizmo.Draw.Text( name, new Transform( correctedWorldPos, Rotation.Identity ), font: "Roboto", size: 12f );
				IsSelected = true;

				// Hack to draw two separate gizmo colors
				Gizmo.Draw.Color = Color.Yellow;
				break;

			default:
				Gizmo.Draw.Color = Color.White;
				IsSelected = false;
				break;
		}

		var spriteparentPos = GameObject.Parent.Transform.World.Position;
		var spritelocalOffset = Vector3.Up * 0.2f; // or your desired offset
		var spritecorrectedWorldPos = spriteparentPos + spritelocalOffset;

		Gizmo.Hitbox.BBox( BBox.FromPositionAndSize( spritecorrectedWorldPos, 12f ) );
		Gizmo.Draw.Sprite( spritecorrectedWorldPos, scale, vtex );

	}


}
