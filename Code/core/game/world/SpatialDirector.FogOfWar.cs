namespace Core;

using Sandbox;
using System.Collections.Generic;

public sealed partial class SpatialDirector
{
	[Property, FeatureEnabled( "Fog Of War" ), Order( 0 )]
	public bool FogEnabled { get; set; } = true;

	// Map

	[Property, Feature( "Fog Of War" )]
	public Vector2 MapCenter { get; set; } = Vector2.Zero;

	[Property, Feature( "Fog Of War" )]
	public Vector2 MapSize { get; set; } = new( 4096f, 4096f );

	[Property, Feature( "Fog Of War" ), Range( 64, 1024 )]
	public int Resolution { get; set; } = 256;

	// Rendering

	[Space( 11 )]

	[Property, Feature( "Fog Of War" )]
	public Color FogColor { get; set; } = Color.Black;

	[Property, Feature( "Fog Of War" )]
	public Vector2 HeightRange { get; set; } = new( -4096f, 4096f );

	// Distance Fog

	[Space( 11 )]

	[Property, Feature( "Fog Of War" ), Range( 0f, 8192f )]
	public float FarStart { get; set; } = 900f;

	[Property, Feature( "Fog Of War" ), Range( 0f, 8192f )]
	public float FarEnd { get; set; } = 1800f;

	[Property, Feature( "Fog Of War" ), Range( 0f, 1f )]
	public float FarStrength { get; set; } = 0.35f;

	[Property, Feature( "Fog Of War" ), Range( 0f, 1f )]
	public float FarOverlapStrength { get; set; } = 0.20f;

	[Property, Feature( "Fog Of War" ), Range( 0.1f, 8f )]
	public float FarOverlapPower { get; set; } = 2.0f;

	// Cloud Layer

	[Property, Feature( "Fog Of War" ), Group( "Cloud Layer" )]
	public bool FogClouds { get; set; } = false;

	[Property, Feature( "Fog Of War" ), Group( "Cloud Layer" )]
	public Texture FogCloudTexture { get; set; }

	[Property, Feature( "Fog Of War" ), Group( "Cloud Layer" )]
	public Color FogCloudColor { get; set; } = new Color( 0f, 0f, 0f, 0.25f );

	[Property, Feature( "Fog Of War" ), Group( "Cloud Layer" ), Range( 0f, 1f )]
	public float FogCloudStrength { get; set; } = 0.2f;

	[Property, Feature( "Fog Of War" ), Group( "Cloud Layer" ), Range( 64f, 8192f )]
	public float FogCloudWorldSize { get; set; } = 1400f;

	[Property, Feature( "Fog Of War" ), Group( "Cloud Layer" )]
	public Vector2 FogCloudVelocity { get; set; } = new( 8f, 3f );

	[Property, Feature( "Fog Of War" ), Group( "Cloud Layer" ), Range( -512f, 2048f )]
	public float FogCloudHeight { get; set; } = 96f;

	[Property, Feature( "Fog Of War" ), Group( "Cloud Layer" ), Range( 0.1f, 8f )]
	public float FogCloudContrast { get; set; } = 2f;

	// Fog Detail

	[Property, Feature( "Fog Of War" ), Group( "Fog Detail" )]
	public Texture FogDetailTexture { get; set; }

	[Property, Feature( "Fog Of War" ), Group( "Fog Detail" ), Range( 0f, 1f )]
	public float FogDetailStrength { get; set; } = 0.2f;

	[Property, Feature( "Fog Of War" ), Group( "Fog Detail" ), Range( 32f, 4096f )]
	public float FogDetailWorldSize { get; set; } = 512f;

	[Property, Feature( "Fog Of War" ), Group( "Fog Detail" )]
	public Vector2 FogDetailVelocity { get; set; } = new( 12f, 6f );

	[Property, Feature( "Fog Of War" ), Group( "Fog Detail" ), Range( 0.1f, 8f )]
	public float FogDetailContrast { get; set; } = 1.5f;

	private Texture _visibilityTexture;

	private byte[] _visible;
	private bool[] _visibilityScratch;
	private Color32[] _pixels;

	private float _updateAccumulator;

	private int _allocatedResolution;
	private readonly Dictionary<ModelRenderer, bool> _rendererOriginalVisibility = new();
}
