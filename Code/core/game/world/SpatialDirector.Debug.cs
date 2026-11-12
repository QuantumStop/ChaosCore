namespace Core;

using Sandbox;
using System;

public sealed partial class SpatialDirector
{
	[ConVar( "debug_spatialdirector", Help = "Spatial director debug. 0 = Off, 1 = Text, 2 = Text + World." )]
	public static int DebugMode { get; set; } = 0;

	[ConVar( "debug_spatialdirector_shader", Help = "Shader debug. 0 = Off, 1 = Surface classes, 2 = Cutaway masks, 3 = Fog masks, 4 = Interior shading, 5 = Depth gate." )]
	public static int ShaderDebugMode { get; set; } = 0;

	[Property, Feature( "Debug" ), ReadOnly] public CameraComponent Camera { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly] public BasePawn Target { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly] public PlayerController Controller => Target is not BasePlayer player ? null : player.Controller;

	[Property, Feature( "Debug" ), ReadOnly]
	public Vector3 VisionWorldPosition => !Target.IsValid() ? Vector3.Zero : Controller.IsValid()
				? Target.WorldPosition + Vector3.Up * Controller.HeadHeight + VisionOffset
				: Target.WorldPosition + VisionOffset;

	[Property, Feature( "Debug" ), ReadOnly] public float PlayerFloorZ => !Target.IsValid() ? 0f : Target.WorldPosition.z;

	[Property, Feature( "Debug" ), ReadOnly] public float PlayerHeight => Controller.IsValid() ? Controller.Height : 0f;

	[Property, Feature( "Debug" ), ReadOnly] public float PlayerTopZ => !Target.IsValid() ? 0f : PlayerFloorZ + PlayerHeight;

	[Property, Feature( "Debug" ), ReadOnly] public bool TargetTraceBlocked { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly] public float CutawayStrength { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly] public bool IsTargetInside { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly] public float ViewBlend { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly] public Vector3 CutawayHitNormalWorld { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly] public Vector3 InteriorProbeHitNormalWorld { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly] public Vector3 CutawayEndWorld { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly] public Vector3 CutawayHitWorld { get; private set; }

	/// <summary>
	/// Refresh the spatial probe every frame. Useful for precise debugging.
	/// </summary>
	[Property, Feature( "Debug" )] public bool FastSpatialProbeDebug { get; set; } = false;

	public Texture VisibilityTexture => _visibilityTexture;

	private int _rendererCullingVisible;
	private int _rendererCullingHidden;
	private int _rendererCullingOutOfRange;
	private int _rendererCullingTraces;
	private int _rendererSamplesInRange;

	private void ResetRendererCullingDebug()
	{
		_rendererCullingVisible = 0;
		_rendererCullingHidden = 0;
		_rendererCullingOutOfRange = 0;
		_rendererCullingTraces = 0;
		_rendererSamplesInRange = 0;
	}

	private void RecordRendererCullingDebug( ModelRenderer renderer, bool visible )
	{
		var outOfRange = !visible && !renderer.GameObject.IsStatic && _rendererSamplesInRange == 0;

		if ( visible ) _rendererCullingVisible++;
		else _rendererCullingHidden++;

		if ( outOfRange ) _rendererCullingOutOfRange++;

		if ( DebugMode < 2 ) return;

		var color = visible ? Color.Green : outOfRange ? Color.Yellow : Color.Red;
		var reason = visible ? "visible" : outOfRange ? "outside range/arc"
			: renderer.GameObject.IsStatic ? "hidden fog cells" : "blocked LOS";

		var debug = DebugOverlaySystem.Current;
		debug.Box( renderer.Bounds, color, 0f, overlay: true );
		debug.Text( renderer.Bounds.Center, $"{reason}", color: color, duration: 0f, overlay: true );
	}

	private static void DrawRendererSampleDebug( Vector3 origin, Vector3 point, Color color )
	{
		if ( DebugMode < 2 ) return;

		var debug = DebugOverlaySystem.Current;
		debug.Line( new Line( origin, point ), color, 0f, overlay: true );
		debug.Sphere( new Sphere( point, 2f ), color, 0f, overlay: true );
	}

	private void UpdateDebug()
	{
		if ( DebugMode <= 0 )
			return;

		if ( DebugMode >= 2 )
			DrawDebugWorld();
	}

	public DebugTextBlock GetDebugText()
	{
		var block = new DebugTextBlock();

		if ( DebugMode <= 0 || !Active || !IsSupportedView || !Camera.IsValid() ) return null;

		block.Header( "SPATIAL DIRECTOR DEBUG" );
		block.Line( "Director", GameObject.Name, Color.White );
		block.Space();

		block.Header( "Cutaway" );
		block.Line( "Running", $"{ShouldRun()}", Color.Cyan );
		block.Line( "Strength", $"{CutawayStrength:0.00}", Color.Green );
		block.Line( "Blocked", $"{TargetTraceBlocked}", Color.Cyan );
		block.Line( "Inside", $"{IsTargetInside}", Color.Cyan );
		block.Line( "View blend", $"{ViewBlend:0.00}", Color.Green );
		block.Line( "Hit position", $"{CutawayHitWorld.x:0}, {CutawayHitWorld.y:0}, {CutawayHitWorld.z:0}", Color.Orange );
		block.Line( "Hit normal", $"{CutawayHitNormalWorld.x:0.00}, {CutawayHitNormalWorld.y:0.00}, {CutawayHitNormalWorld.z:0.00}", Color.Orange );
		block.Line( "Trigger", "Single view ray", Color.White );
		block.Space();

		block.Header( "Visibility" );
		block.Line( "Sampling / Arc", $"{VisionMode} / {VisionArcAngle:0} deg", Color.White );
		block.Line( "Enabled", $"{FogEnabled}", Color.Cyan );
		block.Line( "Resolution", $"{_allocatedResolution}", Color.Green );
		block.Line( "Rays", $"{_spatialProbe.Result.RadialSamples}", Color.Green );
		block.Line( "Radius", $"{VisionRadius:0.0}u", Color.Orange );
		block.Line( "Padding", $"{VisibilityPadding:0.0}u", Color.Orange );
		block.Line( "World only", $"{WorldOnlyOcclusion}", Color.Cyan );
		block.Line( "Texture ready", $"{_visibilityTexture is not null}", Color.Cyan );
		block.Space();

		block.Header( "Renderer Culling" );
		block.Line( "Active", $"{FogEnabled && CullHiddenModelRenderers}", Color.Cyan );
		block.Line( "Bounds / Static", $"{UseBoundsForVisibility} / {CullStaticRenderers}", Color.Cyan );
		block.Line( "Visible / Hidden", $"{_rendererCullingVisible} / {_rendererCullingHidden}", Color.Green );
		block.Line( "Outside vision", $"{_rendererCullingOutOfRange}", Color.Orange );
		block.Line( "LOS traces", $"{_rendererCullingTraces}", Color.Orange );
		block.Space();

		block.Header( "Distance Fog" );
		block.Line( "Start / End", $"{FarStart:0.0} / {FarEnd:0.0}u", Color.Orange );
		block.Line( "Strength", $"{FarStrength:0.00}", Color.Green );
		block.Line( "Overlap", $"{FarOverlapStrength:0.00}", Color.Green );
		block.Line( "Overlap power", $"{FarOverlapPower:0.00}", Color.Green );
		block.Space();

		block.Header( "Interior" );
		block.Line( "Dimming enabled", $"{DimDistantInterior}", Color.Cyan );
		block.Line( "Dim amount", $"{InteriorDimAmount:0.00}", Color.Green );
		block.Line( "Radius / Fade", $"{InteriorVisibleRadius:0.0} / {InteriorFade:0.0}u", Color.Orange );
		block.Line( "Inside", $"{IsTargetInside}", Color.Cyan );
		block.Line( "Probe hits", $"{_spatialProbe.Result.OverheadHits} / {_spatialProbe.Result.OverheadSamples}", Color.Green );
		block.Line( "Probe coverage", $"{_spatialProbe.Result.OverheadCoverage:0.00}", Color.Green );
		block.Line( "Compatible hits", $"{_spatialProbe.Result.CompatibleOverheadHits}", Color.Green );
		block.Line( "Ceiling depth", $"{_spatialProbe.Result.CeilingDepth:0.0}u", Color.Orange );
		block.Line( "Enclosure", $"{_spatialProbe.Result.Enclosure:0.00}", Color.Green );
		block.Line( "Peek enabled", $"{LimitExteriorInteriorPeek}", Color.Cyan );
		block.Line( "Peek reveal", $"{InteriorPeekReveal:0.00}", Color.Green );
		block.Line( "Wall thickness", $"{InteriorPeekWallThickness:0.0}u", Color.Orange );
		block.Line( "Peek fog", $"{FogInteriorPeek}", Color.Cyan );
		block.Line( "Peek fog amount", $"{InteriorPeekFogAmount:0.00}", Color.Green );
		block.Space();

		block.Header( "Shader Debug" );
		block.Line( "Current mode", $"{ShaderDebugMode}", Color.Cyan );
		block.Line( "1", "Paint classes", Color.White );
		block.Line( "2", "Cut / Peek / Dim", Color.White );
		block.Line( "3", "LOS / Distance / Peek", Color.White );
		block.Line( "4", "Interior shading", Color.White );
		block.Line( "5", "Interior depth gate", Color.White );

		return block;
	}

	private void DrawDebugWorld()
	{
		if ( !Target.IsValid() )
			return;

		var debug = DebugOverlaySystem.Current;
		var target = Target.WorldPosition + TargetOffset;
		var visionOrigin = VisionWorldPosition;

		debug.Sphere( new Sphere( target, 8f ), Color.Green, 0f, overlay: true );
		debug.Sphere( new Sphere( visionOrigin, 5f ), Color.Cyan, 0f, overlay: true );

		if ( Camera.IsValid() )
			debug.Line( new Line( target, CutawayEndWorld ), Color.Cyan, 0f, overlay: true );

		if ( CutawayHitWorld != Vector3.Zero )
		{
			debug.Sphere( new Sphere( CutawayHitWorld, 7f ), Color.Red, 0f, overlay: true );
			debug.Normal( CutawayHitWorld, CutawayHitNormalWorld * 48f, Color.Yellow, 0f, overlay: true );
		}
	}

}
