using Core;
using System;

/// <summary>Create path objects or primitives</summary>
[EditorTool( "path.tool" )]
[Title( "Path Tool" )]
[Icon( "polyline" )]
[Group( "3" )]

public sealed class PathTool : EditorTool
{
	[Hide] private List<GameObject> _pathPoints = [];

	public PathType CurrentPathType = PathType.Generic;
	public InterpolationMode CurrentInterpolation = InterpolationMode.Linear;

	private GameObject _pathObject;
	private PathTrack _pathTrack;

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

		if ( !_pathTrack.IsValid() ) return;
		_pathTrack.CurrentPathType = CurrentPathType;
		_pathTrack.CurrentInterpolation = CurrentInterpolation;
	}

	private void AddPathPoint( Vector3 position )
	{
		var pointObject = Scene.CreateObject();
		pointObject.Name = "PathPoint_" + _pathPoints.Count;
		// Add a small offset to the position (e.g., 0.1 units above the hit point)
		pointObject.WorldPosition = position + new Vector3( 0, 0, 0.6f );

		pointObject.Flags = GameObjectFlags.Hidden;
		pointObject?.AddComponent<PathSingle>();

		_pathPoints.Add( pointObject );

		if ( !_pathTrack.IsValid() )
		{
			_pathObject = Scene.CreateObject();
			_pathObject.Name = $"Path_{Random.Shared.Next( 1, 1001 )}";
			_pathTrack = _pathObject.Components.Create<PathTrack>();
		}

		pointObject?.SetParent( _pathObject, true );

		_pathTrack.RefreshPathPoints();
	}

}
