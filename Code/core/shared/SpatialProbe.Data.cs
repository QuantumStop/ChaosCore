namespace Core;

using Sandbox;

public enum SpatialProbeSamplingMode
{
	[Icon( "adjust" )]
	Radial,
	[Icon( "wifi" )]
	Arc
}

public struct SpatialProbeSettings
{
	public float UpdateInterval { get; set; }

	public bool OverheadEnabled { get; set; }
	public float OverheadDistance { get; set; }
	public float OverheadRadius { get; set; }
	public float OverheadDepthTolerance { get; set; }
	public int OverheadRingSamples { get; set; }
	public int MinimumCompatibleOverheadHits { get; set; }
	public int MinimumOverheadSectors { get; set; }

	public bool RadialEnabled { get; set; }
	public float RadialDistance { get; set; }
	public int RadialRays { get; set; }
	
	public SpatialProbeSamplingMode SamplingMode { get; set; }

	public float ArcAngle { get; set; }
	
	public Vector3 ArcDirection { get; set; }

	public bool WorldOnly { get; set; }
	public string WorldTag { get; set; }

	public float EnclosureOverheadWeight { get; set; }
	public float EnclosureRadialWeight { get; set; }

	public static SpatialProbeSettings Default => new()
	{
		UpdateInterval = 0.05f,

		OverheadEnabled = true,
		OverheadDistance = 150f,
		OverheadRadius = 36f,
		OverheadDepthTolerance = 32f,
		OverheadRingSamples = 8,
		MinimumCompatibleOverheadHits = 3,
		MinimumOverheadSectors = 3,

		RadialEnabled = true,
		RadialDistance = 640f,
		RadialRays = 64,
		SamplingMode = SpatialProbeSamplingMode.Radial,
		ArcAngle = 120f,
		ArcDirection = Vector3.Forward,

		WorldOnly = true,
		WorldTag = "world",

		EnclosureOverheadWeight = 0.5f,
		EnclosureRadialWeight = 0.5f
	};
}

public readonly struct SpatialProbeContext( Scene scene, Vector3 origin, GameObject ignoreHierarchy = null )
{
	public Scene Scene { get; } = scene;
	public Vector3 Origin { get; } = origin;
	public GameObject IgnoreHierarchy { get; } = ignoreHierarchy;
}

public struct SpatialProbeResult
{
	public bool HasSample { get; internal set; }
	public Vector3 Origin { get; internal set; }
	public SpatialProbeSamplingMode SamplingMode { get; internal set; }
	public float SampleAngle { get; internal set; }
	public Vector3 SampleDirection { get; internal set; }

	public bool IsCovered { get; internal set; }
	public int OverheadSamples { get; internal set; }
	public int OverheadHits { get; internal set; }
	public int CompatibleOverheadHits { get; internal set; }
	public int OverheadSectors { get; internal set; }
	public bool OppositeOverhead { get; internal set; }
	public float OverheadCoverage { get; internal set; }
	public float CompatibleOverheadCoverage { get; internal set; }
	public float CeilingDepth { get; internal set; }
	public float AverageCeilingDepth { get; internal set; }
	public Vector3 CeilingNormal { get; internal set; }

	public int RadialSamples { get; internal set; }
	public int RadialHits { get; internal set; }
	public float RadialHitCoverage { get; internal set; }
	public float NearestWallDistance { get; internal set; }
	public float FurthestOpenDistance { get; internal set; }
	public float AverageOpenDistance { get; internal set; }
	public float Openness { get; internal set; }
	public float Enclosure { get; internal set; }
	public Vector3 OpenDirection { get; internal set; }
}

public readonly struct SpatialProbeOverheadSample
{
	public Vector3 Start { get; }
	public Vector3 End { get; }
	public Vector3 Normal { get; }
	public float Depth { get; }
	public bool Hit { get; }
	public bool Compatible { get; }

	internal SpatialProbeOverheadSample( Vector3 start, Vector3 end, Vector3 normal, float depth, bool hit, bool compatible )
	{
		Start = start;
		End = end;
		Normal = normal;
		Depth = depth;
		Hit = hit;
		Compatible = compatible;
	}

	internal SpatialProbeOverheadSample WithCompatible( bool compatible ) => new( Start, End, Normal, Depth, Hit, compatible );
}

public readonly struct SpatialProbeRadialSample
{
	public Vector3 Direction { get; }
	public Vector3 End { get; }
	public float Distance { get; }
	public bool Hit { get; }

	internal SpatialProbeRadialSample( Vector3 direction, Vector3 end, float distance, bool hit )
	{
		Direction = direction;
		End = end;
		Distance = distance;
		Hit = hit;
	}
}
