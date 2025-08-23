using Editor;

namespace Sandbox
{
	[EditorTool, Icon( "add_circle" )]
	public class EnvmapPlacer : EditorTool
	{
		enum PlaceMode
		{
			UnderCursor,
			UnderCursorVerticalCenter,
			BoundsCenter,
			BoundsCenterAboveFloor,
		}
		PlaceMode Mode { get; set; }
		float HeightAboveFloor { get; set; } = 64f;
		public override void OnEnabled()
		{
			base.OnEnabled();
			AllowGameObjectSelection = false;

			var window = new WidgetWindow( SceneOverlay );
			window.Layout = Layout.Column();
			window.MaximumWidth = 300;

			var modeLabel = new Label();
			modeLabel.Text = "Mode";
			var enumWidget = new EnumControlWidget( this.GetSerialized().GetProperty( "Mode" ) );
			var heightLabel = new Label();
			heightLabel.Text = "Height Above Floor";
			var heightWidget = new FloatControlWidget( this.GetSerialized().GetProperty( "HeightAboveFloor" ) );

			window.Layout.Add( modeLabel );
			window.Layout.Add( enumWidget );
			window.Layout.Add( heightLabel );
			window.Layout.Add( heightWidget );
			AddOverlay( window );
		}

		public override void OnUpdate()
		{
//			Log.Info( Mode );
			base.OnUpdate();

			var cameraRay = Gizmo.Camera.GetRay( Gizmo.CursorPosition );
			var seedTrace = MeshTrace.Ray( cameraRay, 4096f ).Run();

			//We haven't hit anything to start building an envmap with
			if ( !seedTrace.Hit )
				return;

			var seedPosition = seedTrace.EndPosition + seedTrace.Normal * HeightAboveFloor;

			float boundsTraceLength = 512f;
			float boundsPadding = 8f;

			float minX = MeshTrace.Ray( seedPosition, seedPosition + Vector3.Backward * boundsTraceLength ).Run().EndPosition.x - boundsPadding;
			float minY = MeshTrace.Ray( seedPosition, seedPosition + Vector3.Right * boundsTraceLength ).Run().EndPosition.y - boundsPadding;
			float minZ = MeshTrace.Ray( seedPosition, seedPosition + Vector3.Down * boundsTraceLength ).Run().EndPosition.z - boundsPadding;
			float maxX = MeshTrace.Ray( seedPosition, seedPosition + Vector3.Forward * boundsTraceLength ).Run().EndPosition.x + boundsPadding;
			float maxY = MeshTrace.Ray( seedPosition, seedPosition + Vector3.Left * boundsTraceLength ).Run().EndPosition.y + boundsPadding;
			float maxZ = MeshTrace.Ray( seedPosition, seedPosition + Vector3.Up * boundsTraceLength ).Run().EndPosition.z + boundsPadding;

			var bounds = new BBox( new Vector3( minX, minY, minZ ), new Vector3( maxX, maxY, maxZ ) );

			var placePosition = Vector3.Zero;

			switch ( Mode )
			{
				case PlaceMode.BoundsCenter:
					placePosition = bounds.Center;
					break;
				case PlaceMode.BoundsCenterAboveFloor:
					placePosition = bounds.Center.WithZ( bounds.Mins.z + HeightAboveFloor );
					break;
				case PlaceMode.UnderCursor:
					placePosition = seedPosition;
					break;
				case PlaceMode.UnderCursorVerticalCenter:
					placePosition = seedPosition.WithZ( bounds.Center.z );
					break;
				default:
					placePosition = seedPosition;
					break;
			}

			Gizmo.Draw.LineBBox( bounds );
			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.Sprite( placePosition, 16f, "materials/gizmo/envmap.png" );

			if ( !Gizmo.HasClicked )
				return;

			var gameobject = new GameObject();
			gameobject.WorldPosition = placePosition;

			var localBounds = new BBox( bounds.Mins - gameobject.WorldPosition, bounds.Maxs - gameobject.WorldPosition );
			var probe = gameobject.Components.Create<EnvmapProbe>();
			probe.Projection = SceneCubemap.ProjectionMode.Box;
			probe.Bounds = localBounds;

		}
	}
}
