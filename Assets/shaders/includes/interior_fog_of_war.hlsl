#ifndef INTERIOR_FOG_OF_WAR_HLSL
#define INTERIOR_FOG_OF_WAR_HLSL

float g_flSurfaceFogEnabled 		< Attribute( "SurfaceFogEnabled" ); >;

Texture2D g_tSurfaceFogVisibility 	< Attribute( "SurfaceFogVisibility" ); >;

SamplerState g_sSurfaceFogSampler 	< Filter( POINT ); AddressU( CLAMP ); AddressV( CLAMP ); >;

float2 g_vSurfaceFogMapMin      		< Attribute( "SurfaceFogMapMin" ); >;
float2 g_vSurfaceFogMapSize    	 		< Attribute( "SurfaceFogMapSize" ); >;
float2 g_vSurfaceFogHeightRange 		< Attribute( "SurfaceFogHeightRange" ); >;

float4 g_vSurfaceFogColor 				< Attribute( "SurfaceFogColor" ); >;

float g_flInteriorFogBaseDarkness      	< Attribute( "InteriorFogBaseDarkness" ); >;
float g_flInteriorFogUnexploredOpacity 	< Attribute( "InteriorFogUnexploredOpacity" ); >;
float g_flInteriorFogExploredOpacity   	< Attribute( "InteriorFogExploredOpacity" ); >;

// R = currently visible
// G = explored
// B = interior

float3 InteriorFog_State( float3 positionWs )
{
	if ( g_flSurfaceFogEnabled < 0.5f )
		return float3( 1.0f, 1.0f, 0.0f );

	if ( positionWs.z < g_vSurfaceFogHeightRange.x || positionWs.z > g_vSurfaceFogHeightRange.y )
	{
		return float3( 1.0f, 1.0f, 0.0f );
	}

	float2 uv = (positionWs.xy - g_vSurfaceFogMapMin ) / max( g_vSurfaceFogMapSize, float2( 1.0f, 1.0f ) );

	if ( any( uv < 0.0f ) || any( uv >= 1.0f ) ) return float3( 0.0f, 0.0f, 0.0f );

	return saturate( g_tSurfaceFogVisibility.SampleLevel( g_sSurfaceFogSampler, uv, 0 ).rgb );
}

float InteriorFog_Opacity( float3 positionWs )
{
	float3 state = InteriorFog_State( positionWs );

	float visible = state.r;
	float explored = state.g;
	float interior = state.b;

	if ( interior <= 0.0001f ) return 0.0f;

	float baseDarkness = saturate( g_flInteriorFogBaseDarkness );

	float hiddenOpacity = saturate( lerp( g_flInteriorFogUnexploredOpacity, g_flInteriorFogExploredOpacity, explored ) );

	float hiddenFog = (1.0f - visible ) * hiddenOpacity;

	float amount = 1.0f - (1.0f - baseDarkness ) * (1.0f - hiddenFog );

	return saturate( amount * interior );
}

#endif
