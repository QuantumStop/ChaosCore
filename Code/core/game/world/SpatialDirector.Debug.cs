namespace Core;

using Sandbox;
using System;

public sealed partial class SpatialDirector
{
	[ConVar( "spatialdirector_debug", Help = "Spatial director debug. 0=off, 1=text, 2=text+world." )]
	public static int DebugMode { get; set; } = 0;

	[ConVar( "spatialdirector_debug_shader", Help = "Shader debug. 0=off, 1=surface classes, 2=cutaway masks, 3=fog masks, 4=interior shading, 5=depth gate." )]
	public static int ShaderDebugMode { get; set; } = 0;

	[Property, Feature( "Debug" ), ReadOnly]
	public CameraComponent Camera { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly]
	public BasePawn Target { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly]
	public PlayerController Controller
	{
		get
		{
			if ( Target is not BasePlayer player )
				return null;

			return player.Controller as PlayerController;
		}
	}

	[Property, Feature( "Debug" ), ReadOnly]
	public Vector3 VisionWorldPosition
	{
		get
		{
			if ( !Target.IsValid() )
				return Vector3.Zero;

			var controller = Controller;

			if ( controller.IsValid() )
				return Target.WorldPosition + Vector3.Up * controller.HeadHeight + VisionOffset;

			return Target.WorldPosition + VisionOffset;
		}
	}

	[Property, Feature( "Debug" ), ReadOnly]
	public float PlayerFloorZ
	{
		get
		{
			if ( !Target.IsValid() )
				return 0f;

			return Target.WorldPosition.z;
		}
	}

	[Property, Feature( "Debug" ), ReadOnly]
	public float PlayerHeight
	{
		get
		{
			var controller = Controller;

			if ( controller.IsValid() )
				return controller.Height;

			return 0f;
		}
	}

	[Property, Feature( "Debug" ), ReadOnly]
	public float PlayerTopZ
	{
		get
		{
			if ( !Target.IsValid() )
				return 0f;

			return PlayerFloorZ + PlayerHeight;
		}
	}

	[Property, Feature( "Debug" ), ReadOnly]
	public bool TargetTraceBlocked { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly]
	public float CutawayStrength { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly]
	public bool IsTargetInside { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly]
	public float ViewBlend { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly]
	public Vector3 CutawayHitNormalWorld { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly]
	public Vector3 InteriorProbeHitNormalWorld { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly]
	public Vector3 CutawayEndWorld { get; private set; }

	[Property, Feature( "Debug" ), ReadOnly]
	public Vector3 CutawayHitWorld { get; private set; }

	/// <summary>
	/// Refresh the spatial probe every frame. Useful for precise debugging.
	/// </summary>
	[Property, Feature( "Debug" )]
	public bool FastSpatialProbeDebug { get; set; } = false;

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
	
		if ( visible )
			_rendererCullingVisible++;
		else
			_rendererCullingHidden++;
		
		if ( outOfRange )
			_rendererCullingOutOfRange++;

		if ( DebugMode < 2 )
			return;

		var color = visible ? Color.Green : outOfRange ? Color.Yellow : Color.Red;
		var reason = visible ? "visible" : outOfRange ? "outside range/arc"
			: renderer.GameObject.IsStatic ? "hidden fog cells" : "blocked LOS";

		var debug = DebugOverlaySystem.Current;
		debug.Box( renderer.Bounds, color, 0f, overlay: true );
		debug.Text( renderer.Bounds.Center, $"{reason}", color: color, duration: 0f, overlay: true );
	}

	private static void DrawRendererSampleDebug( Vector3 origin, Vector3 point, Color color )
	{
		if ( DebugMode < 2 )
			return;

		var debug = DebugOverlaySystem.Current;
		debug.Line( new Line( origin, point ), color, 0f, overlay: true );
		debug.Sphere( new Sphere( point, 2f ), color, 0f, overlay: true );
	}

	private void UpdateDebug()
	{
		if ( DebugMode <= 0 )
			return;

		DrawDebugText();

		if ( DebugMode >= 2 )
			DrawDebugWorld();
	}

	private void DrawDebugText()
	{
		if ( !Camera.IsValid() )
			return;

		float x = Screen.Width - 330f;
		float y = 20f;
		float lineHeight = 0.85f;

		var scope = new TextRendering.Scope
		{
			FontName = "RobotoMono",
			FontSize = 11f,
			FontWeight = 500,
			TextColor = Color.White,
			LineHeight = lineHeight
		};

		scope.Outline.Enabled = true;
		scope.Outline.Color = Color.Black;
		scope.Outline.Size = 3.25f;

		scope.Text += "SPATIAL DIRECTOR DEBUG\n";
		scope.Text += "\n";

		// Cutaway
		{
			scope.Text += "Cutaway:\n";
			scope.Text += $"   Running:       	{ShouldRun()}\n";
			scope.Text += $"   Strength:      	{CutawayStrength:F2}\n";
			scope.Text += $"   Blocked:       	{TargetTraceBlocked}\n";
			scope.Text += $"   Inside:        	{IsTargetInside}\n";
			scope.Text += $"   ViewBlend:     	{ViewBlend:F2}\n";
			scope.Text += $"   Hit:           	{CutawayHitWorld}\n";
			scope.Text += $"   HitNormal:     	{CutawayHitNormalWorld}\n";
			scope.Text += "   Trigger:       Single view ray\n";
			scope.Text += "\n";
		}

		// Visibility
		{
			scope.Text += "Visibility:\n";
			scope.Text += $"   Sampling: {VisionMode} / {VisionArcAngle:F0} deg\n";
			scope.Text += $"   Enabled:       	{FogEnabled}\n";
			scope.Text += $"   Resolution:    	{_allocatedResolution}\n";
			scope.Text += $"   Rays:          	{_spatialProbe.Result.RadialSamples}\n";
			scope.Text += $"   Radius:        	{VisionRadius:F1}\n";
			scope.Text += $"   Padding:       	{VisibilityPadding:F1}\n";
			scope.Text += $"   WorldOnly:     	{WorldOnlyOcclusion}\n";
			scope.Text += $"   Texture:       	{_visibilityTexture is not null}\n";
			scope.Text += "\n";
		}

		// Culling
		{
			scope.Text += "Renderer Culling:\n";
			scope.Text += $"   Active:        	{FogEnabled && CullHiddenModelRenderers}\n";
			scope.Text += $"   Bounds/Static: 	{UseBoundsForVisibility}/{CullStaticRenderers}\n";
			scope.Text += $"   Visible/Hidden:	{_rendererCullingVisible}/{_rendererCullingHidden}\n";
			scope.Text += $"   Outside vision:  	{_rendererCullingOutOfRange}\n";
			scope.Text += $"   LOS traces:    	{_rendererCullingTraces}\n";
			scope.Text += "\n";
		}

		// Distance dimming
		{
			scope.Text += "Distance Fog:\n";
			scope.Text += $"   Start:         	{FarStart:F1}\n";
			scope.Text += $"   End:           	{FarEnd:F1}\n";
			scope.Text += $"   Strength:      	{FarStrength:F2}\n";
			scope.Text += $"   Overlap:       	{FarOverlapStrength:F2}\n";
			scope.Text += $"   OverlapPower:  	{FarOverlapPower:F2}\n";
			scope.Text += "\n";
		}

		// Interior
		{
			scope.Text += "Interior:\n";
			scope.Text += $"   DimEnabled:    	{DimDistantInterior}\n";
			scope.Text += $"   Dim:           	{InteriorDimAmount:F2}\n";
			scope.Text += $"   Radius:        	{InteriorVisibleRadius:F1}\n";
			scope.Text += $"   Fade:          	{InteriorFade:F1}\n";
			scope.Text += $"   Inside:        	{IsTargetInside}\n";
			scope.Text += $"   ProbeHits:     	{_spatialProbe.Result.OverheadHits}/{_spatialProbe.Result.OverheadSamples}\n";
			scope.Text += $"   ProbeCoverage: 	{_spatialProbe.Result.OverheadCoverage:F2}\n";
			scope.Text += $"   Compatible:    	{_spatialProbe.Result.CompatibleOverheadHits}\n";
			scope.Text += $"   CeilingDepth:  	{_spatialProbe.Result.CeilingDepth:F1}\n";
			scope.Text += $"   Enclosure:     	{_spatialProbe.Result.Enclosure:F2}\n";
			scope.Text += $"   Peek:          	{LimitExteriorInteriorPeek}\n";
			scope.Text += $"   PeekReveal:    	{InteriorPeekReveal:F2}\n";
			scope.Text += $"   PeekThickness: {InteriorPeekWallThickness:F1}\n";
			scope.Text += $"   PeekFog:       	{FogInteriorPeek}\n";
			scope.Text += $"   PeekFogAmount: 	{InteriorPeekFogAmount:F2}\n";
			scope.Text += "\n";
		}

		scope.Text += "Shader Debug:\n";
		scope.Text += $"   Current Mode:    {ShaderDebugMode}\n";
		scope.Text += "\n";
		scope.Text += "   1 = Paint classes\n";
		scope.Text += "   2 = Cut / Peek / Dim\n";
		scope.Text += "   3 = LOS / Distance / Peek\n";
		scope.Text += "   4 = Interior shading\n";
		scope.Text += "   5 = Interior depth gate\n";

		// Make sure the text is drawn on top of everything else here
		using var painter = Camera.BeginOverlay();
		painter.TextStyle = new TextStyle( scope.FontName, scope.FontSize, scope.TextColor )
		{
			FontWeight = scope.FontWeight,
			LineHeight = scope.LineHeight,
			Alignment = TextFlag.LeftTop
		}.WithOutline( Color.Black, 3.25f );

		painter.Text( scope.Text, new Rect( x, y, 330f, Screen.Height - y ) );
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
