namespace Core;

using Sandbox;

public enum SpatialDirectorArcDimMode
{
	Uniform,
	[Title( "Linear Distance" )]
	LinearDistance
}

public sealed partial class SpatialDirector
{
	[Property, Feature( "Fog Of War" ), Group( "Arc Dimming" ), Title( "Mode" ), ShowIf( nameof( VisionMode ), SpatialProbeSamplingMode.Arc )]
	public SpatialDirectorArcDimMode ArcDimMode { get; set; } = SpatialDirectorArcDimMode.Uniform;

	private bool ShowArcDimDistance => VisionMode == SpatialProbeSamplingMode.Arc && ArcDimMode == SpatialDirectorArcDimMode.LinearDistance;

	[Property, Feature( "Fog Of War" ), Group( "Arc Dimming" ), Title( "Near Dim" ), Range( 0f, 1f ), ShowIf( nameof( ShowArcDimDistance ), true )]
	public float ArcNearDim { get; set; } = 0f;

	[Property, Feature( "Fog Of War" ), Group( "Arc Dimming" ), Title( "Far Dim" ), Range( 0f, 1f ), ShowIf( nameof( VisionMode ), SpatialProbeSamplingMode.Arc )]
	[Description( "Uniform dim amount, or the far dim amount in Linear Distance mode. 0 preserves brightness; 1 is black. Does not change culling." )]
	public float OutsideArcDim { get; set; } = 0.65f;

	[Property, Feature( "Fog Of War" ), Group( "Arc Dimming" ), Title( "Start Distance" ), Range( 0f, 8192f ), ShowIf( nameof( ShowArcDimDistance ), true )]
	public float ArcDimStart { get; set; } = 64f;

	[Property, Feature( "Fog Of War" ), Group( "Arc Dimming" ), Title( "End Distance" ), Range( 0f, 8192f ), ShowIf( nameof( ShowArcDimDistance ), true )]
	public float ArcDimEnd { get; set; } = 640f;


	[Property, Feature( "Fog Of War" ), Group( "Arc Dimming" ), Title( "Arc Feather" ), Range( 0f, 90f ), ShowIf( nameof( VisionMode ), SpatialProbeSamplingMode.Arc )]
	[Description( "Visual transition width in degrees outside the arc. Does not widen renderer visibility." )]
	public float ArcFeather { get; set; } = 12f;

	[Property, Feature( "Fog Of War" ), Group( "Arc Fog Detail" ), Title( "Use Fog Detail" ), ShowIf( nameof( VisionMode ), SpatialProbeSamplingMode.Arc )]
	public bool ArcUseFogDetail { get; set; } = true;

	private bool ShowArcFogDetail => VisionMode == SpatialProbeSamplingMode.Arc && ArcUseFogDetail;

	[Property, Feature( "Fog Of War" ), Group( "Arc Fog Detail" ), Title( "Texture" ), ShowIf( nameof( ShowArcFogDetail ), true )]
	public Texture ArcFogDetailTexture { get; set; }

	[Property, Feature( "Fog Of War" ), Group( "Arc Fog Detail" ), Title( "Strength" ), ShowIf( nameof( ShowArcFogDetail ), true ), Range( 0f, 1f )]
	public float ArcFogDetailStrength { get; set; } = 0.2f;

	[Property, Feature( "Fog Of War" ), Group( "Arc Fog Detail" ), Title( "World Size" ), ShowIf( nameof( ShowArcFogDetail ), true ), Range( 32f, 4096f )]
	public float ArcFogDetailWorldSize { get; set; } = 512f;

	[Property, Feature( "Fog Of War" ), Group( "Arc Fog Detail" ), Title( "Velocity" ), ShowIf( nameof( ShowArcFogDetail ), true )]
	public Vector2 ArcFogDetailVelocity { get; set; } = new( 12f, 6f );

	[Property, Feature( "Fog Of War" ), Group( "Arc Fog Detail" ), Title( "Contrast" ), ShowIf( nameof( ShowArcFogDetail ), true ), Range( 0.1f, 8f )]
	public float ArcFogDetailContrast { get; set; } = 1.5f;

}
