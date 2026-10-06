namespace Core;

using Sandbox;
using System;

public sealed partial class SpatialDirector
{
	/// <summary>
	/// Shared tiling noise used to distort cutaway and fog boundaries.
	/// </summary>
	[Property, Feature( "Noise" )]
	public Texture CutawayNoise { get; set; }

	/// <summary>
	/// Maximum boundary displacement in world units, shared by cutaway and fog.
	/// </summary>
	[Property, Feature( "Noise" ), Range( 0f, 128f )]
	public float CutawayNoiseStrength { get; set; } = 0f;

	[Property, Feature( "Noise" )]
	public Vector2 CutawayNoiseTiling { get; set; } = new( 2f, 2f );

	[Property, Feature( "Noise" )]
	public Vector2 CutawayNoiseOffset { get; set; } = Vector2.Zero;

	/// <summary>
	/// Vertical noise scroll speed, zero disables scrolling.
	/// </summary>
	[Property, Feature( "Noise" ), Range( -4f, 4f )]
	public float CutawayNoiseScrollSpeed { get; set; } = 0f;


	[Property, Feature( "Dither" ), Range( 0f, 1f )]
	public float CutawayDitherAmount { get; set; } = 1f;

	[Property, Feature( "Dither" ), Range( 0.25f, 8f )]
	public float CutawayDitherScale { get; set; } = 1f;

	internal void ApplyPatternAttributes( RenderAttributes attributes )
	{
		var hasNoise = CutawayNoise is not null && CutawayNoise.IsValid && CutawayNoiseStrength > 0f;
		
		attributes.Set( "CutawayUseNoise", hasNoise ? 1f : 0f );
		attributes.Set( "CutawayNoiseStrength", MathF.Max( CutawayNoiseStrength, 0f ) );
		attributes.Set( "CutawayNoiseTiling", CutawayNoiseTiling );
		attributes.Set( "CutawayNoiseOffset", CutawayNoiseOffset + new Vector2( 0f, _noiseScroll ) );
		attributes.Set( "CutawayDitherAmount", Math.Clamp( CutawayDitherAmount, 0f, 1f ) );
		attributes.Set( "CutawayDitherScale", MathF.Max( CutawayDitherScale, 0.001f ) );
		
		if ( hasNoise )
			attributes.Set( "CutawayNoise", CutawayNoise );
	}
}
