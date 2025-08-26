using System;
using System.Dynamic;
using System.Numerics;
using System.Collections.Generic;

namespace Core;

public enum PathType { Generic, StaticCable, Rope, Trajectory, PathCorner }
public enum InterpolationMode { Linear, Spline }
public enum TextureOrientation { Horizontal, Vertical } // U && V along paths

[Description( "A path." )]
[Icon( "polyline" )]
[Title( "Path" )]
public class PathTrack : BaseEntity, Component.ExecuteInEditor
{
	protected override string GetEditorVis() { return null; }

	[Property, MakeDirty] public PathType CurrentPathType { get; set; } = PathType.Generic;
	[Property, MakeDirty] public InterpolationMode CurrentInterpolation { get; set; } = InterpolationMode.Linear;

	[Property, Feature( "Debug" )] public List<GameObject> pathPoints = new(); // Store GameObjects as path points
	private Dictionary<GameObject, Vector3> lastKnownPositions = new();
	private HashSet<GameObject> knownPathPoints = new();

	private SceneObject cableObject;

	[Space( 12 )]
	//-- Everything texture related --//
	[Header( "Cable Texture options:" )]
	[Property, ShowIf( nameof( CurrentPathType ), PathType.StaticCable ), MakeDirty] public Material CableMaterial { get; set; }


	[Property, ShowIf( nameof( CurrentPathType ), PathType.StaticCable ), MakeDirty, Title( "Texture Orientation" )]
	public TextureOrientation TexOrientation { get; set; } = TextureOrientation.Horizontal;


	[Property, ShowIf( nameof( CurrentPathType ), PathType.StaticCable ), MakeDirty, Range( 0.03125f, 4f ), Title( "Texture Scale" )]
	public float TextureScale { get; set; } = 1f;


	[Property, ShowIf( nameof( CurrentPathType ), PathType.StaticCable ), MakeDirty, Range( 0.03125f, 32f ), Title( "Texture Repeat Around" )]
	public float TextureRepeatCircumference { get; set; } = 1f;


	[Property, ShowIf( nameof( CurrentPathType ), PathType.StaticCable ), MakeDirty, Range( -1f, 1f ), Title( "Texture Offset Along" )]
	public float TextureOffsetAlong { get; set; } = 0f;


	[Property, ShowIf( nameof( CurrentPathType ), PathType.StaticCable ), MakeDirty, Range( -1f, 1f ), Title( "Texture Offset Around" )]
	public float TextureOffsetAround { get; set; } = 0f;

	[Space( 12 )]
	//-- Everything cable visuals related --//
	[Header( "3D Spline options:" )]
	[Order( 1 )][Property, ShowIf( nameof( CurrentPathType ), PathType.StaticCable ), MakeDirty, Range( 3, 64 ), Step( 1 ), Title( "Number of slides" )] public int Sides { get; set; } = 12;
	[Order( 1 )][Property, ShowIf( nameof( CurrentPathType ), PathType.StaticCable ), MakeDirty, Range( 10, 512 ), Step( 1 )] public float Spacing { get; set; } = 10;
	[Order( 1 )][Property, ShowIf( nameof( CurrentPathType ), PathType.StaticCable ), MakeDirty, Range( 1, 256 ), Step( 1 )] public float Radius { get; set; } = 6.0f;


	[Property, MakeDirty] public bool ShowObjects { get; set; } = true;
	bool isDirty = true;
	List<Vector3> splinePoints = new();

	public SceneCamera EditorCamera;


	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( cableObject != null )
			cableObject.RenderingEnabled = true;
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( cableObject != null )
			cableObject.RenderingEnabled = false;
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		cableObject?.Delete();
		cableObject = null;
	}

	protected override void OnDirty()
	{
		base.OnDirty();
		isDirty = true;

		switch ( CurrentInterpolation )
		{
			case InterpolationMode.Linear:
				UpdatePath();
				break;
			case InterpolationMode.Spline:
				UpdatePath();
				break;
		}

		switch ( ShowObjects )
		{
			case true:
				ToggleObjVis( true );
				break;
			case false:
				ToggleObjVis( false );
				break;
		}

	}

	public void ToggleObjVis( bool condition )
	{
		var allObjects = GameObject.Children.ToList();
		foreach ( var obj in allObjects )
		{
			if ( obj.Name.StartsWith( "PathPoint_" ) )
			{
				if ( (obj.Flags & GameObjectFlags.Hidden) != 0 && condition )
					obj.Flags &= ~GameObjectFlags.Hidden;
				else if ( !condition )
					obj.Flags |= GameObjectFlags.Hidden;
			}
		}
	}

	private void UpdatePath()
	{
		if ( !isDirty ) return;

		splinePoints.Clear();

		if ( pathPoints.Count < 2 )
			return;

		float scaledSpacing = Spacing / 1000f;

		if ( CurrentInterpolation == InterpolationMode.Linear )
		{
			foreach ( var point in pathPoints )
				splinePoints.Add( point.WorldPosition );
		}
		else if ( CurrentInterpolation == InterpolationMode.Spline )
		{
			var extendedPoints = new List<Vector3>();

			extendedPoints.Add( pathPoints[0].WorldPosition ); // duplicate first point
			foreach ( var point in pathPoints )
				extendedPoints.Add( point.WorldPosition );
			extendedPoints.Add( pathPoints[^1].WorldPosition ); // duplicate last point

			for ( int i = 0; i < extendedPoints.Count - 3; i++ )
			{
				var p0 = extendedPoints[i];
				var p1 = extendedPoints[i + 1];
				var p2 = extendedPoints[i + 2];
				var p3 = extendedPoints[i + 3];

				for ( float t = 0; t <= 1.0f; t += scaledSpacing )
				{
					var point = CatmullRom( p0, p1, p2, p3, t );
					splinePoints.Add( point );
				}
			}

			// Ensure last point is included
			splinePoints.Add( extendedPoints[^2] );
		}

		// Generate cable mesh if type is StaticCable or Rope
		if ( CurrentPathType == PathType.StaticCable || CurrentPathType == PathType.Rope )
		{
			if ( Game.IsEditor && splinePoints.Count >= 2 && lodCheckTimer > 0.2f )
			{
				var cameraPosition = Scene.Camera.WorldPosition;
				GenerateCableMesh( cameraPosition );
			}
			else
			{
				var cameraPosition = Scene.Camera.WorldPosition;
				GenerateCableMesh( cameraPosition );
			}
		}
		else
		{
			cableObject?.Delete();
			cableObject = null;
		}

		isDirty = false;
	}

	public void RefreshPathPoints()
	{
		var currentPoints = GameObject.Children
			.Where( child => child.Name.StartsWith( "PathPoint_" ) )
			.ToList();

		var currentSet = currentPoints.ToHashSet();

		// Detect if any points were added or removed
		if ( !currentSet.SetEquals( knownPathPoints ) )
		{
			pathPoints = currentPoints;
			knownPathPoints = currentSet;

			// Track last known positions
			lastKnownPositions.Clear();
			foreach ( var point in pathPoints )
				lastKnownPositions[point] = point.WorldPosition;

			isDirty = true;
		}
	}

	private void CheckForMovedPoints()
	{
		bool anyMoved = false;

		foreach ( var point in pathPoints )
		{
			var current = point.WorldPosition;
			
			#if EDITOR

			if ( !lastKnownPositions.TryGetValue( point, out var last ) || current != last )
			{
				lastKnownPositions[point] = current;
				anyMoved = true;
			}

			#endif
		}

		if ( anyMoved )
		{
			isDirty = true;
		}
	}


	// CLamping update time for gizmo.drawtext/anything else we need to update
	private Dictionary<GameObject, (Vector3 position, string label)> textCache = new();
	private bool isTextCacheDirty = true;

	private TimeSince timeSinceLastTextDraw = 0;
	private const float textDrawInterval = 0.0026f; // seconds (200ms)

	private float lodCheckTimer = 0f;
	private int previousLODLevel = -1;


	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( !Game.IsEditor ) return;

		EditorCamera = Gizmo.Camera;

		RefreshPathPoints();
		CheckForMovedPoints();

		if ( isDirty )
		{
			UpdatePath(); // Only rebuild if really needed
			InvalidateTextCache();
		}

		Gizmo.Draw.LineThickness = 2.5f;

		bool isPathSelected = Gizmo.IsSelected;
		GameObject selectedPoint = pathPoints.FirstOrDefault( point => point.GetComponent<PathSingle>()?.IsSelected == true );


		// Draw path point handles (sprites)
		foreach ( var point in pathPoints )
		{
			var pos = point.WorldPosition + Vector3.Up * 0.4f;
			float dist = (pos - Gizmo.Camera.Position).Length;
			float scale = Math.Clamp( dist * 0.05f, 0.5f, 12.0f );

			Gizmo.Hitbox.BBox( BBox.FromPositionAndSize( point.WorldPosition, 12f ) );
		}


		// Draw Path and it's information
		if ( splinePoints.Count >= 2 )
		{
			Gizmo.Draw.LineThickness = 2.5f;

			var selectedPoints = pathPoints
				.Where( p =>
				{
					var ps = p.Components.Get<PathSingle>();
					return ps != null && ps.IsSelected;
				} )
				.ToList();

			int firstSelectedIndex = selectedPoints.Count > 0 ? pathPoints.IndexOf( selectedPoints.First() ) : -1;
			int lastSelectedIndex = selectedPoints.Count > 0 ? pathPoints.IndexOf( selectedPoints.Last() ) : -1;

			if ( firstSelectedIndex >= 0 && lastSelectedIndex >= 0 && lastSelectedIndex < firstSelectedIndex )
			{
				// Swap to ensure ordering
				(firstSelectedIndex, lastSelectedIndex) = (lastSelectedIndex, firstSelectedIndex);
			}

			for ( int i = 0; i < splinePoints.Count - 1; i++ )
			{
				var a = splinePoints[i];
				var b = splinePoints[i + 1];
				Color lineColor = Color.White;

				bool shouldDrawText = timeSinceLastTextDraw > textDrawInterval;

				if ( isPathSelected )
				{
					if ( shouldDrawText )
					{
						lineColor = Color.Yellow;

						// Only rebuild the text cache when it's dirty
						if ( isTextCacheDirty )
							RebuildTextCache();

						foreach ( var kvp in textCache )
						{
							Gizmo.Draw.Color = Color.White;
							Gizmo.Draw.Text( kvp.Value.label, new Transform( kvp.Value.position, Rotation.Identity ), font: "Roboto", size: 12f );
						}
					}
				}
				else
				{
					foreach ( var sp in selectedPoints )
					{
						int index = pathPoints.IndexOf( sp );
						if ( index == -1 ) continue;

						Vector3 curr = pathPoints[index].WorldPosition;
						Vector3? prev = index > 0 ? pathPoints[index - 1].WorldPosition : (Vector3?)null;
						Vector3? next = index < pathPoints.Count - 1 ? pathPoints[index + 1].WorldPosition : (Vector3?)null;

						// Fade white -> yellow near prev
						if ( prev.HasValue && IsInSegment( a, b, prev.Value, curr ) )
						{
							float t = DistanceRatio( a, b, prev.Value, curr );
							lineColor = Color.Lerp( Color.White, Color.Yellow, t );
							break;
						}
						// Fade yellow -> white near next
						if ( next.HasValue && IsInSegment( a, b, curr, next.Value ) )
						{
							float t = DistanceRatio( a, b, curr, next.Value );
							lineColor = Color.Lerp( Color.Yellow, Color.White, t );
							break;
						}
					}
				}

				if ( shouldDrawText )
					timeSinceLastTextDraw = 0;

				Gizmo.Draw.Color = lineColor;
				Gizmo.Draw.Line( a, b );
			}

			if ( Game.IsEditor && splinePoints.Count >= 2 && lodCheckTimer > 0.2f )
			{
				Vector3 cameraPos = Gizmo.Camera.Position;
				float distance = splinePoints.Min( p => Vector3.DistanceBetween( p, cameraPos ) );
				int lodLevel = GetLODLevel( distance );

				if ( lodLevel != previousLODLevel )
				{
					previousLODLevel = lodLevel;
					GenerateCableMesh( cameraPos );
				}
			}


		}
	}

	protected override void OnFixedUpdate()
	{
		lodCheckTimer += Time.Delta;
		if ( lodCheckTimer < 0.2f ) // check every 200ms
			return;


		lodCheckTimer = 0f;

		Vector3 cameraPos =
			!Game.IsPlaying && EditorCamera != null ? EditorCamera.Position :
			Game.IsPlaying && Scene.Camera != null ? Scene.Camera.WorldPosition :
			Vector3.Zero;

		float distance = splinePoints.Min( p => Vector3.DistanceBetween( p, cameraPos ) );
		int lodLevel = GetLODLevel( distance );

		if ( lodLevel != previousLODLevel )
		{
			previousLODLevel = lodLevel;
			GenerateCableMesh( cameraPos );

		}

	}

	private void InvalidateTextCache()
	{
		isTextCacheDirty = true;
	}

	private void RebuildTextCache()
	{
		// Only rebuild the cache if it's marked dirty
		if ( isTextCacheDirty )
		{
			textCache.Clear();

			foreach ( var point in pathPoints )
			{
				if ( point == null ) continue;

				Vector3 pos = point.WorldPosition + Vector3.Up * 4f;
				string label = point.Name?.Replace( "PathPoint_", "" ) ?? "";

				textCache[point] = (pos, label);
			}

			isTextCacheDirty = false; // After rebuilding, set it to false
		}
	}


	Vector3 CatmullRom( Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t )
	{
		float t2 = t * t;
		float t3 = t2 * t;

		return 0.5f * (
			2f * p1 +
			(-p0 + p2) * t +
			(2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
			(-p0 + 3f * p1 - 3f * p2 + p3) * t3
		);
	}

	private bool IsInSegment( Vector3 a, Vector3 b, Vector3 p1, Vector3 p2 )
	{
		var segmentMin = Vector3.Min( p1, p2 );
		var segmentMax = Vector3.Max( p1, p2 );
		var mid = (a + b) * 0.5f;
		return mid.x >= segmentMin.x && mid.x <= segmentMax.x &&
			   mid.y >= segmentMin.y && mid.y <= segmentMax.y &&
			   mid.z >= segmentMin.z && mid.z <= segmentMax.z;
	}

	private float DistanceRatio( Vector3 a, Vector3 b, Vector3 p1, Vector3 p2 )
	{
		var mid = (a + b) * 0.5f;
		float totalDist = (p1 - p2).Length;
		if ( totalDist <= 0.001f ) return 0f;
		float dist = (p1 - mid).Length;
		return dist / totalDist;
	}

	private void GenerateCableMesh( Vector3 cameraPosition )
	{
		if ( splinePoints == null || splinePoints.Count < 2 )
			return;


		List<float> originalLengths = ComputeCumulativeLengths( splinePoints );


		// Calculate camera distance to determine LOD level
		var cameraPos = Scene.Camera?.WorldPosition ?? Gizmo.Camera.Position;
		float distance = Vector3.DistanceBetween( splinePoints[0], cameraPos );
		int lodLevel = GetLODLevel( distance );
		var (curvatureThreshold, straightSpacing) = GetLODSettings( lodLevel );

		// Generate LOD-reduced spline based on curvatureThreshold
		var lodSplinePoints = GenerateCurvatureLODPoints( curvatureThreshold, straightSpacing );
		if ( lodSplinePoints.Count < 2 )
			return;

		// Init material and mesh buffers
		var material = CableMaterial != null ? Material.Load( CableMaterial.ResourcePath ) : Material.Load( "materials/dev/dev_texture_surface_concrete1_tinted.vmat" );
		var mesh = new Mesh( material );
		var vb = new VertexBuffer();
		vb.Init( true );

		float radius = Radius * 0.5f;
		int segmentCount = lodSplinePoints.Count;

		// Use the existing Sides property as the base number of sides
		int baseSides = Sides;

		// Calculate sides for this LOD level based on the base Sides value
		int sidesForLOD = (int)Math.Clamp( baseSides * GetSidesMultiplier( lodLevel ), 3, 64 );  // Ensure sides are between 3 and 64

		int vertexCount = 0; // Track total vertex count manually


		// Generate vertices based on sides for this LOD level
		for ( int i = 0; i < segmentCount; i++ )
		{
			var center = lodSplinePoints[i];

			// Calculate tangent direction (between points)
			Vector3 tangent = (i < segmentCount - 1)
				? (lodSplinePoints[i + 1] - lodSplinePoints[i]).Normal
				: (i > 0 ? (lodSplinePoints[i] - lodSplinePoints[i - 1]).Normal : Vector3.Zero);

			// Make sure the bitangent is perpendicular to both tangent and up vector
			Vector3 bitangent = MathF.Abs( Vector3.Dot( tangent, Vector3.Up ) ) > 0.95f
				? Vector3.Forward : Vector3.Up;

			Vector3 normal = Vector3.Cross( tangent, bitangent ).Normal;
			bitangent = Vector3.Cross( tangent, normal ).Normal;


			// Remember the index of the first vertex in this ring
			int ringStartIndex = vertexCount;


			// Loop through sides for the cable cross-section
			for ( int j = 0; j < sidesForLOD - 1; j++ )
			{
				float angle = j / (float)(sidesForLOD - 1) * MathF.Tau;
				Vector3 offset = normal * MathF.Cos( angle ) + bitangent * MathF.Sin( angle );
				Vector3 position = center + offset * radius;

				Vector3 _normalVec = offset.Normal;
				Vector3 _tangentVec = Vector3.Cross( _normalVec, tangent ).Normal;
				Vector4 _tangent = new Vector4( _tangentVec, -1.0f );

				// UV Mapping
				Vector2 uv = GetUVMapping( i, j, lodSplinePoints, sidesForLOD, splinePoints, originalLengths );

				Vertex vertex = new Vertex
				{
					Position = position,
					Normal = _normalVec,
					Tangent = _tangent,
					TexCoord0 = uv
				};

				vb.Add( vertex );
				vertexCount++;


			}
		}

		int vertsPerRing = sidesForLOD - 1; // only this many unique verts per ring


		// Triangle generation for meshvertexCount
		for ( int i = 0; i < segmentCount - 1; i++ )
		{
			int ringStart = i * vertsPerRing;
			int nextRingStart = (i + 1) * vertsPerRing;

			for ( int j = 0; j < vertsPerRing; j++ )

			{
				int a = ringStart + j;
				int b = ringStart + (j + 1) % vertsPerRing;
				int c = nextRingStart + j;
				int d = nextRingStart + (j + 1) % vertsPerRing;

				vb.AddRawIndex( a );
				vb.AddRawIndex( b );
				vb.AddRawIndex( c );

				vb.AddRawIndex( b );
				vb.AddRawIndex( d );
				vb.AddRawIndex( c );
			}
		}

		mesh.CreateBuffers( vb );

		var model = new ModelBuilder().AddMesh( mesh ).Create();

		if ( cableObject == null )
		{
			var transform = new Transform( Vector3.Zero, Rotation.Identity );
			cableObject = new SceneObject( Scene.SceneWorld, model, transform );
		}
		else
		{
			cableObject.Model = model;
		}
	}

	private Vector2 GetUVMapping( int segmentIndex, int sideIndex, List<Vector3> lodSplinePoints, int sidesForLOD, List<Vector3> originalSplinePoints, List<float> originalLengths )
	{
		// Get accurate "along" distance by mapping LOD point to original arc length
		float trueLength = GetOriginalSplineDistanceAt(
			lodSplinePoints[segmentIndex],
			originalSplinePoints,
			originalLengths
		);

		float totalLength = originalLengths[^1]; // Last value = total length
		float scaleMultiplier = 100f; // tweak as needed
		float along = (trueLength / totalLength) * (TextureScale * scaleMultiplier) + TextureOffsetAlong;

		// Around (circumferential)
		float around = sideIndex / (float)(sidesForLOD - 1);
		if ( sideIndex == sidesForLOD - 1 ) around = 1f;

		around = around * TextureRepeatCircumference + TextureOffsetAround;

		return (TexOrientation == TextureOrientation.Horizontal)
			? new Vector2( around, along )
			: new Vector2( along, around );

	}


	private List<float> ComputeCumulativeLengths( List<Vector3> points )
	{
		var lengths = new List<float> { 0f };
		for ( int i = 1; i < points.Count; i++ )
		{
			float segmentLength = Vector3.DistanceBetween( points[i - 1], points[i] );
			lengths.Add( lengths[^1] + segmentLength );
		}
		return lengths;
	}

	private float GetOriginalSplineDistanceAt( Vector3 point, List<Vector3> originalPoints, List<float> originalLengths )
	{
		float minDist = float.MaxValue;
		int closestIndex = 0;

		for ( int i = 0; i < originalPoints.Count; i++ )
		{
			float dist = Vector3.DistanceBetween( point, originalPoints[i] );
			if ( dist < minDist )
			{
				minDist = dist;
				closestIndex = i;
			}
		}

		return originalLengths[Math.Clamp( closestIndex, 0, originalLengths.Count - 1 )];
	}



	private int GetLODLevel( float distanceToCamera )
	{
		// Define distance thresholds for LOD levels
		if ( distanceToCamera < 500f )
		{
			return 0; // Full resolution
		}
		else if ( distanceToCamera < 1000f )
		{
			return 1; // Low resolution
		}
		else
		{
			return 2; // Medium resolution
		}
	}

	private List<Vector3> GenerateCurvatureLODPoints( float curvatureThreshold, int straightSpacing )
	{
		if ( splinePoints == null || splinePoints.Count < 3 )
			return splinePoints;

		List<Vector3> lodPoints = new();
		lodPoints.Add( splinePoints[0] );

		for ( int i = 1; i < splinePoints.Count - 1; i++ )
		{
			Vector3 prev = splinePoints[i - 1];
			Vector3 curr = splinePoints[i];
			Vector3 next = splinePoints[i + 1];

			Vector3 dir1 = (curr - prev).Normal;
			Vector3 dir2 = (next - curr).Normal;

			float dot = Vector3.Dot( dir1, dir2 );

			if ( dot < curvatureThreshold || (i % straightSpacing == 0) )
				lodPoints.Add( curr );
		}

		lodPoints.Add( splinePoints[^1] );
		return lodPoints;
	}

	private (float curvatureThreshold, int straightSpacing) GetLODSettings( int lodLevel )
	{
		switch ( lodLevel )
		{
			case 0: return (1f, 1);        // Full detail
			case 1: return (0.985f, 2);    // Medium
			case 2: return (0.975f, 6);    // Low
			default: return (0.985f, 6);   // fallback
		}
	}

	private float GetSidesMultiplier( int lodLevel )
	{
		switch ( lodLevel )
		{
			case 0:
				return 1.0f;  // Highest resolution (default sides)
			case 1:
				return 0.75f; // Reduce sides for medium LOD
			case 2:
				return 0.5f;  // Further reduce sides for far LOD
			default:
				return 0.25f; // Very low resolution at far distances
		}
	}

}
