using Core;
using System;

[EditorTool( "path.tool" )]
[Title( "Path Tool" )]
[Icon( "polyline" )]
[Group( "3" )]
[Description( "Create path objects or primitives" )]

public sealed class PathTool : EditorTool
{
	[Hide] private List<GameObject> pathPoints = new();

	public PathType currentPathType = PathType.Generic;
	public InterpolationMode currentInterpolation = InterpolationMode.Linear;

	private GameObject pathObject;
	private PathTrack pathTrack;

	public override void OnEnabled()
	{
		Selection.Clear();
		Selection.Set( this );
	}

	public PathTool()
	{
		AllowGameObjectSelection = false;
	}

	public override void OnDisabled()
	{
		// TODO: Evaluate
	}

	public override void OnUpdate()
	{
		base.OnUpdate();

		if ( !Scene.IsValid() )
			return;

		var tr = Scene.Trace.Ray( Gizmo.CurrentRay, 5000 )
			.UseRenderMeshes( true )
			.UsePhysicsWorld( false )
			.WithoutTags( "trigger" )
			.Run();

		if ( tr.Hit )
		{
			using ( Gizmo.Scope( "cursor" ) )
			{
				Gizmo.Transform = new Transform( tr.HitPosition, Rotation.LookAt( tr.Normal ) );
				Gizmo.Draw.LineCircle( 0, 10 );
			}

			if ( !Gizmo.Pressed.Any && Gizmo.WasLeftMousePressed )
			{
				AddPathPoint( tr.HitPosition );
			}
		}

		if ( !pathTrack.IsValid() ) return;
		pathTrack.CurrentPathType = currentPathType;
		pathTrack.CurrentInterpolation = currentInterpolation;
	}

	private void AddPathPoint( Vector3 position )
	{
		var pointObject = Scene.CreateObject();
		pointObject.Name = "PathPoint_" + pathPoints.Count;
		// Add a small offset to the position (e.g., 0.1 units above the hit point)
		pointObject.WorldPosition = position + new Vector3( 0, 0, 0.6f );

		pointObject.Flags = GameObjectFlags.Hidden;
		pointObject?.AddComponent<PathSingle>();

		pathPoints.Add( pointObject );

		if ( !pathTrack.IsValid() )
		{
			pathObject = Scene.CreateObject();
			pathObject.Name = $"Path_{Random.Shared.Next( 1, 1001 )}";
			pathTrack = pathObject.Components.Create<PathTrack>();
		}

		pointObject?.SetParent( pathObject, true );

		pathTrack.RefreshPathPoints();
	}

}
