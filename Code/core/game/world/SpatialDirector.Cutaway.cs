namespace Core;

using Sandbox;
using System;

public enum CutawayShapeMode
{
	[Icon( "circle" )]
	Ellipse,
	[Icon( "square" )]
	Box,
	[Title( "Cookie" ), Icon( "cookie" )]
	Custom
}

public enum CutawayRunMode
{
	[Icon( "visibility_off" )]
	Disabled,
	[Icon( "visibility" )]
	Enabled,
	[Title( "Only In Isometric" ), Icon( "layers" )]
	IsometricOnly
}

public sealed partial class SpatialDirector
{
	[Property, Feature( "Culling" ), Order( 0 )]
	public CutawayRunMode Mode { get; set; } = CutawayRunMode.IsometricOnly;

	[Property, Feature( "Culling" ), Order( 1 )]
	public Vector3 TargetOffset { get; set; } = new( 0f, 0f, 65f );

	[Property, Feature( "Culling" ), Order( 2 )]
	public bool UseTargetOcclusionTrace { get; set; } = true;

	[Property, Feature( "Culling" ), Order( 3 )]
	public CutawayShapeMode Shape { get; set; } = CutawayShapeMode.Ellipse;

	/// <summary>
	/// Half size of the cut cross section in world units.
	/// </summary>
	[Property, Feature( "Culling" ), Order( 4 )]
	public Vector2 CutawaySize { get; set; } = new( 72f, 72f );

	/// <summary>
	/// Extends the cut slightly behind the first obstruction hit.
	/// </summary>
	[Property, Feature( "Culling" ), Range( 0f, 256f ), Order( 6 )]
	public float BehindHitPadding { get; set; } = 24f;

	/// <summary>
	/// Offsets the start of the rounded cut corridor toward the camera.
	/// Clamped at the obstruction hit so padding cannot obscure the target sightline.
	/// </summary>
	[Property, Feature( "Culling" ), Range( 0f, 256f ), Order( 7 )]
	public float NearTargetPadding { get; set; } = 4f;

	// Edge Blend

	[Property, Feature( "Culling" ), Group( "Edge Blend" ), Range( 0f, 256f )]
	public float OutsideToInsideFeather { get; set; } = 20f;

	[Property, Feature( "Culling" ), Group( "Edge Blend" ), Range( 0f, 1f )]
	public float OutsideToInsideSeeThrough { get; set; } = 0.9f;

	[Property, Feature( "Culling" ), Group( "Edge Blend" ), Range( 0f, 256f )]
	public float InsideToOutsideFeather { get; set; } = 28f;

	[Property, Feature( "Culling" ), Group( "Edge Blend" ), Range( 0f, 1f )]
	public float InsideToOutsideSeeThrough { get; set; } = 0.65f;

	// Interior
	
	[Property, Title( "Dim" ), Feature( "Interior" ), Range( 0f, 1f )]
	public float InteriorDimAmount { get; set; } = 0.92f;

	[Property, Title( "Outside Dim" ), Feature( "Interior" ), Range( 0f, 1f )]
	public float InteriorExteriorDim { get; set; } = 0.5f;

	[Property, Title( "Dim Far" ), Feature( "Interior" )]
	public bool DimDistantInterior { get; set; } = true;

	// Peeking

	[Property, Title( "Peek Limit" ), Feature( "Interior" ), Group( "Peeking" )]
	public bool LimitExteriorInteriorPeek { get; set; } = true;

	[Property, Title( "Peek Size" ), Feature( "Interior" ), Group( "Peeking" ), Range( 0.05f, 1f )]
	public float ExteriorInteriorPeekSizeScale { get; set; } = 0.45f;

	[Property, Title( "Peek Cut" ), Feature( "Interior" ), Group( "Peeking" ), Range( 0f, 1f )]
	public float ExteriorInteriorPeekSeeThrough { get; set; } = 0.35f;

	[Property, Title( "Peek Interior" ), Feature( "Interior" ), Group( "Peeking" ), Range( 0f, 1f )]
	public float InteriorPeekReveal { get; set; } = 0f;

	/// <summary>
	/// Extra depth past the obstruction hit, toward the camera, included in outside interior peeking.
	/// Measured along the viewing ray in world units, not the wall normal. Zero preserves the original depth gate.
	/// </summary>
	[Property, Title( "Peek Wall Thickness" ), Feature( "Interior" ), Group( "Peeking" ), Range( 0f, 512f ), ShowIf( nameof( LimitExteriorInteriorPeek ), true )]
	public float InteriorPeekWallThickness { get; set; } = 0f;

	[Property, Title( "Block Peek" ), Feature( "Interior" ), Group( "Peeking" )]
	public bool BlockInteriorPeek { get; set; } = true;

	// Fog

	/// <summary>
	/// Show interior fog when the target is inside, fades out when the target moves outside.
	/// </summary>
	[Property, Title( "Show Interior Fog" ), Feature( "Interior" ), Group( "Fog" )]
	public bool FogInteriorInside { get; set; } = false;

	/// <summary>
	/// Fog interiors revealed by the cutaway while the target is outside, fades out when the target moves inside.
	/// </summary>
	[Property, Title( "Outside Peek Fog" ), Feature( "Interior" ), Group( "Fog" )]
	public bool FogInteriorPeek { get; set; } = false;

	[Property, Title( "Color" ), Feature( "Interior" ), Group( "Fog" )]
	public Color InteriorPeekFogColor { get; set; } = Color.Black;

	[Property, Title( "Amount" ), Feature( "Interior" ), Group( "Fog" ), Range( 0f, 1f )]
	public float InteriorPeekFogAmount { get; set; } = 0.95f;

	[Property, Title( "Radius" ), Feature( "Interior" ), Group( "Fog" ),  Range( 32f, 2048f )]
	public float InteriorVisibleRadius { get; set; } = 320f;

	[Property, Title( "Softness" ), Feature( "Interior" ), Group( "Fog" ), Range( 0.25f, 4f )]
	public float InteriorPeekFogSoftness { get; set; } = 1f;

	[Property, Title( "Fade" ), Feature( "Interior" ), Group( "Fog" ), Range( 0f, 1024f )]
	public float InteriorFade { get; set; } = 96f;	

	[Property, Title( "Noise" ), Feature( "Interior" ), Group( "Fog" ), Range( 0f, 1f )]
	public float InteriorPeekFogNoise { get; set; } = 0.25f;

	// Floor Protection

	[Property, Title( "Floor Cut Threshold" ), Feature( "Culling" ), Group( "Floor Protection" ), Range( 0f, 1f )]
	public float CutawayFloorThreshold { get; set; } = 0.7f;

	/// <summary>
	/// Protect upward facing surfaces this far above the target's feet/lower portion. 
	/// Floors below the feet remain protected at any height difference.
	/// </summary>
	[Property, Title( "Floor Height Tolerance" ), Feature( "Culling" ), Group( "Floor Protection" ), Range( 0f, 128f )]
	public float CutawayFloorHeightTolerance { get; set; } = 24f;

	// Transitions

	[Property, Feature( "Culling" ), Group( "Transitions" )]
	public Curve FadeInCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 1f, 1f ) )
	{
		TimeRange = new Vector2( 0f, 0.18f )
	};

	[Property, Feature( "Culling" ), Group( "Transitions" )]
	public Curve FadeOutCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 1f, 1f ) )
	{
		TimeRange = new Vector2( 0f, 0.22f )
	};

	[Property, Feature( "Culling" ), Group( "Transitions" )]
	public Curve HitFollowCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 1f, 1f ) )
	{
		TimeRange = new Vector2( 0f, 0.12f )
	};

	[Property, Feature( "Culling" ), Group( "Transitions" )]
	public Curve ViewTransitionCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 1f, 1f ) )
	{
		TimeRange = new Vector2( 0f, 0.2f )
	};


	/// <summary>
	/// An optional mask.
	/// Used as the shape when Cookie option is picked as a shape.
	/// </summary>
	[Property, Feature( "Cookie" )]
	public Texture CutawayCookie { get; set; }

	[Property, Feature( "Cookie" ), Range( 0f, 1f )]
	public float CutawayCookieStrength { get; set; } = 1f;

	[Property, Feature( "Cookie" )]
	public Vector2 CutawayCookieScale { get; set; } = Vector2.One;

	[Property, Feature( "Cookie" )]
	public Vector2 CutawayCookieOffset { get; set; } = Vector2.Zero;

	[Property, Feature( "Cookie" )]
	public bool CutawayCookieTiling { get; set; } = true;

	/// <summary>
	/// Vertical cookie scroll speed, zero disables scrolling.
	/// </summary>
	[Property, Feature( "Cookie" ), Range( -4f, 4f )]
	public float CutawayCookieScrollSpeed { get; set; } = 0f;	


	private float _traceAccumulator;
	private float _occlusionGraceRemaining;

	private Vector3 _targetHitWorld;
	private Vector3 _hitFollowStartWorld;
	private bool _hasHitPosition;
	private float _hitFollowTime;

	private bool _wasBlocked;
	private float _fadeTime;
	private float _fadeStartStrength;
	private float _fadeTargetStrength;

	private bool _wasTargetInside;
	private float _viewTime;
	private float _viewStartBlend;
	private float _viewTargetBlend;

	private float _cookieScroll;
	private float _noiseScroll;


	/// <summary>
	/// Updates the cutaway trace, transition curves, and shader attributes.
	/// </summary>
	private void UpdateCutaway()
	{
		if ( !ShouldRun() )
		{
			SetCutawayInactive();
			return;
		}

		_occlusionGraceRemaining = MathF.Max( _occlusionGraceRemaining - WorldTime.Delta, 0f );

		_traceAccumulator += WorldTime.Delta;
		if ( _traceAccumulator >= 0.066f )
		{
			_traceAccumulator = 0f;
			UpdateOcclusion();
		}

		UpdateHitFollow();
		UpdateFade();
		UpdateViewTransition();
		UpdateShaderCutaway();
	}

	private bool ShouldRun()
	{
		if ( !IsSupportedView )
			return false;

		return Mode switch
		{
			CutawayRunMode.Enabled => true,
			CutawayRunMode.IsometricOnly => BasePlayer.Local?.Controller is PlayerController { IsIsometricCamera: true },
			_ => false
		};
	}

	/// <summary>
	/// Clears transient state and disables the shader cutaway.
	/// </summary>
	private void SetCutawayInactive()
	{
		TargetTraceBlocked = false;
		CutawayStrength = 0f;
		IsTargetInside = false;
		ViewBlend = 0f;

		_traceAccumulator = 0f;
		_occlusionGraceRemaining = 0f;

		_hasHitPosition = false;
		_hitFollowTime = 0f;

		_fadeTime = 0f;
		_fadeStartStrength = 0f;
		_fadeTargetStrength = 0f;
		_wasBlocked = false;

		_viewTime = 0f;
		_viewStartBlend = 0f;
		_viewTargetBlend = 0f;
		_wasTargetInside = false;

		_cookieScroll = 0f;

		CutawayHitNormalWorld = Vector3.Zero;
		InteriorProbeHitNormalWorld = Vector3.Zero;

		SetShaderCutawayDisabled();
	}
}
