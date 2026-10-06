namespace Core;

using Sandbox;

public sealed partial class SpatialDirector
{
	[Property, Group( "Vision" ), Range( 1f, 360f ), ShowIf( nameof( VisionMode ), SpatialProbeSamplingMode.Arc )]
	[Description( "Full width of the player facing arc. Restricts probe sampling, fog visibility and renderer culling." )]
	public float VisionArcAngle { get; set; } = 120f;

	public Vector3 VisionArcDirection => SpatialProbe.NormalizeArcDirection( Controller.IsValid() 
		? Controller.EyeAngles.WithPitch( 0f ).Forward : Target.IsValid() ? Target.WorldRotation.Forward : Vector3.Forward );

	private bool IsWithinVisionArc( Vector3 point, Vector3 origin ) => VisionMode != SpatialProbeSamplingMode.Arc 
		|| SpatialProbe.IsWithinArc( point - origin, VisionArcDirection, VisionArcAngle );

	[Property, Group( "Interior Probe" )]
	public bool UseProbe { get; set; } = true;

	[Property, Group( "Interior Probe" ), Range( 8f, 256f )]
	public float CeilingProbeDistance { get; set; } = 72f;

	[Property, Group( "Interior Probe" ), Range( 0f, 128f )]
	public float ProbeRadius { get; set; } = 32f;

	[Property, Group( "Interior Probe" ), Range( 0f, 128f )]
	public float ProbeDepthTolerance { get; set; } = 32f;

	private readonly SpatialProbe _spatialProbe = new();

	public SpatialProbeResult SpatialResult => _spatialProbe.Result;

	/// <summary>
	/// Samples shared visibility and enclosure data for cutaway, fog, and DSP.
	/// </summary>
	private void UpdateSpatialProbe()
	{
		if ( Scene is null || !Target.IsValid() )
			return;

		var settings = _spatialProbe.Settings;

		settings.OverheadEnabled = UseProbe;
		settings.OverheadDistance = CeilingProbeDistance;
		settings.OverheadRadius = ProbeRadius;
		settings.OverheadDepthTolerance = ProbeDepthTolerance;

		settings.RadialEnabled = FogEnabled || DspEnabled;
		settings.RadialDistance = VisionRadius;
		settings.RadialRays = VisionRays;
		settings.SamplingMode = VisionMode;
		settings.ArcAngle = VisionArcAngle;
		settings.ArcDirection = VisionArcDirection;

		settings.WorldOnly = WorldOnlyOcclusion;

		_spatialProbe.Settings = settings;

		var force = FastSpatialProbeDebug;

		_spatialProbe.Update( new SpatialProbeContext( Scene, VisionWorldPosition, Target.GameObject ), force );

		IsTargetInside = settings.OverheadEnabled && _spatialProbe.Result.IsCovered;
	}
}
