using Editor;
using System.Linq;

namespace Sandbox
{
	[EditorTool, Icon( "add_circle" )]
	public class EnvmapChangePivot : EditorTool
	{
		public override void OnEnabled()
		{
			AllowGameObjectSelection = true;

			base.OnEnabled();
		}

		public override void OnUpdate()
		{
			base.OnUpdate();

			if ( Selection.First() is not GameObject gameObject )
				return;

			if ( !gameObject.Components.TryGet( out EnvmapProbe probe ) )
				return;


			Vector3 newPos = Vector3.Zero;
			using ( Gizmo.ObjectScope( probe, probe.WorldTransform ) )
			{
				Gizmo.Control.Position( "pos", Vector3.Zero, out newPos );

				probe.WorldPosition += newPos;
				var newBounds = new BBox( probe.Bounds.Mins - newPos, probe.Bounds.Maxs - newPos );
				probe.Bounds = newBounds;
			}

		}
	}
}
