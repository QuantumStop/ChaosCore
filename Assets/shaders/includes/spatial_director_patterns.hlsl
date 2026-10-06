#ifndef SPATIAL_DIRECTOR_PATTERNS_HLSL
#define SPATIAL_DIRECTOR_PATTERNS_HLSL

Texture2D g_tCutawayNoise 			< Attribute( "CutawayNoise" ); >;
SamplerState g_sCutawayNoiseSampler < Filter( LINEAR ); AddressU( WRAP ); AddressV( WRAP ); >;

float g_flCutawayUseNoise      	< Attribute( "CutawayUseNoise" ); >;
float g_flCutawayNoiseStrength 	< Attribute( "CutawayNoiseStrength" ); >;
float2 g_vCutawayNoiseTiling   	< Attribute( "CutawayNoiseTiling" ); >;
float2 g_vCutawayNoiseOffset   	< Attribute( "CutawayNoiseOffset" ); >;
float g_flCutawayDitherAmount  	< Attribute( "CutawayDitherAmount" ); >;
float g_flCutawayDitherScale   	< Attribute( "CutawayDitherScale" ); >;

// Both effects use the cutaway controls, including world-unit strength and scrolling.
float SpatialDirector_NoiseOffset( float2 localPosition, float2 halfSize )
{
	if ( g_flCutawayUseNoise < 0.5f || g_flCutawayNoiseStrength <= 0.0001f ) return 0.0f;
	float2 uv = localPosition / (max( abs( halfSize ), float2( 0.001f, 0.001f ) ) * 2.0f );
	uv = uv * g_vCutawayNoiseTiling + g_vCutawayNoiseOffset;
	float noise = g_tCutawayNoise.SampleLevel( g_sCutawayNoiseSampler, uv, 0 ).r;
	return (noise * 2.0f - 1.0f ) * g_flCutawayNoiseStrength;
}

#endif
