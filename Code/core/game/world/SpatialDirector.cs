namespace Core;

using Sandbox;
using System;

public enum SpatialDirectorViewMode
{
	[Title( "Orthographic" )]
	Orthographic,
	[Title( "Perspective" )]
	Perspective
}

[PresetTarget( "SpatialDirector", Name = "Spatial Director", Icon = "travel_explore", Category = "Rendering", IncludeAll = true )]

[Description( "Controls world cutaway, interior dimming, fog of war, and renderer visibility." )]
[Title( "Spatial Director" )]
[Category( "Rendering" )]
[Icon( "travel_explore" )]
public sealed partial class SpatialDirector : BaseEntity
{
	[InfoBox( """
		Spatial Director is an experimental system that provides a set of features for controlling 
		and utilizing visibility in the world. 

		Supports cutaway, interior dimming, fog of war, and renderer visibility.
		Uses a spatial probe that can be used to query visibility, occlusion in the world
		and DSP effects.
		
		Only Orthographic view is properly supported right now. 
	""" )]

	[Property, Title( "View Mode" ), Order( -100 )] public SpatialDirectorViewMode ViewMode { get; set; } = SpatialDirectorViewMode.Orthographic;
	[Property, PresetSelector] public GenericPresetResource Preset { get; set; }

	[Property, Group( "Vision" )]
	public SpatialProbeSamplingMode VisionMode { get; set; } = SpatialProbeSamplingMode.Radial;

	[Property, Group( "Vision" ), Range( 32f, 4096f )]
	public float VisionRadius { get; set; } = 640f;

	[Property, Group( "Vision" )]
	public Vector3 VisionOffset { get; set; } = new( 0f, 0f, 0f );

	[Property, Group( "Vision" ), Range( 8, 256 )]
	public int VisionRays { get; set; } = 64;

	[Property, Group( "Vision" ), Range( 0f, 64f )]
	public float VisibilityPadding { get; set; } = 8f;

	[Property, Group( "Vision" ), Range( 0.016f, 0.5f )]
	public float UpdateInterval { get; set; } = 0.066f;

	[Property, Group( "Vision" )]
	public bool WorldOnlyOcclusion { get; set; } = true;

	public bool IsSupportedView => ViewMode == SpatialDirectorViewMode.Orthographic && Controller is { IsIsometricCamera: true };

	protected override void OnStart()
	{
		base.OnStart();

		EnsureResources();
		ResolveLocalReferences();
		UpdateIgnoredRenderers();
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		EnsureResources();
		_updateAccumulator = MathF.Max( UpdateInterval, 0.016f );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		SetCutawayInactive();
		RestoreRendererVisibility();
		DisableDspImmediately();
	}

	protected override void OnDestroy()
	{
		SetCutawayInactive();
		RestoreRendererVisibility();
		DisableDspImmediately();

		_visibilityTexture = null;
		_visible = null;
		_visibilityScratch = null;
		_pixels = null;
		_spatialProbe.Reset();
	}

	protected override void OnPreRender()
	{
		if ( Scene is null || !ResolveLocalReferences() || !IsSupportedView )
		{
			SetCutawayInactive();
			RestoreRendererVisibility();
			DisableDspImmediately();
			_updateAccumulator = MathF.Max( UpdateInterval, 0.016f );
			return;
		}

		UpdateTextureAnimation();
		UpdateSpatialProbe();
		UpdateCutaway();
		UpdateDsp();

		Scene.RenderAttributes.Set( "SpatialDirectorDebug", (float)ShaderDebugMode );

		if ( DebugMode == 1 )
			_spatialProbe.DrawDebugText( new Vector2( 20f, 20f ), Camera );
		else
			_spatialProbe.DrawDebug( new Vector2( 20f, 20f ), force: DebugMode >= 2, camera: Camera );

		if ( !FogEnabled )
		{
			RestoreRendererVisibility();
			UpdateDebug();
			_updateAccumulator = MathF.Max( UpdateInterval, 0.016f );
			return;
		}

		EnsureResources();

		_updateAccumulator += WorldTime.Delta;

		var interval = MathF.Max( UpdateInterval, 0.016f );

		if ( _updateAccumulator >= interval )
		{
			_updateAccumulator %= interval;

			UpdateVisibility();
			UploadVisibilityTexture();
		}

		// Dynamic objects are hidden immediately instead of waiting for
		// the visibility texture update.
		UpdateRendererVisibility();
		UpdateDebug();
	}

	private bool ResolveLocalReferences()
	{
		var player = BasePlayer.Local;

		if ( !player.IsValid() || !player.Controller.IsValid() )
		{
			Target = null;
			Camera = null;
			return false;
		}

		// The scene decides which camera is active, so we're not bounding it to our local player explicitly.
		// TODO: Would be nice to support any pawn!
		Target = player;
		Camera = Scene.Camera;

		return Camera.IsValid() && Target.IsValid();
	}

	private void EnsureResources()
	{
		var resolution = Math.Clamp( Resolution, 64, 1024 );

		if ( _visibilityTexture is null || _allocatedResolution != resolution )
		{
			_allocatedResolution = resolution;

			var count = resolution * resolution;

			_visible = new byte[count];
			_visibilityScratch = new bool[count];
			_pixels = new Color32[count];

			_visibilityTexture = Texture.Create( resolution, resolution )
				.WithDynamicUsage()
				.Finish();

			UploadVisibilityTexture();
		}
	}

	private void UploadVisibilityTexture()
	{
		if ( _visibilityTexture is null || _pixels is null )
			return;

		for ( var i = 0; i < _pixels.Length; i++ )
			_pixels[i] = new Color32( _visible[i], 0, 0, 255 );

		_visibilityTexture.Update( _pixels );
	}

}
