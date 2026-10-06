#ifndef SPATIAL_DIRECTOR_FOG_LAYERS_HLSL
#define SPATIAL_DIRECTOR_FOG_LAYERS_HLSL

Texture2D g_tFogDetailTexture 	< Attribute( "FogDetailTexture" ); >;

SamplerState g_sFogDetailSampler < Filter( LINEAR ); AddressU( WRAP ); AddressV( WRAP ); >;

float g_flFogDetailEnabled   	< Attribute( "FogDetailEnabled" ); >;
float g_flFogDetailStrength  	< Attribute( "FogDetailStrength" ); >;
float g_flFogDetailWorldSize 	< Attribute( "FogDetailWorldSize" ); >;
float2 g_vFogDetailVelocity  	< Attribute( "FogDetailVelocity" ); >;
float g_flFogDetailContrast  	< Attribute( "FogDetailContrast" ); >;

Texture2D g_tFogCloudTexture 	< Attribute( "FogCloudTexture" ); >;

SamplerState g_sFogCloudSampler < Filter( LINEAR ); AddressU( WRAP ); AddressV( WRAP ); >;

float g_flFogCloudEnabled   	< Attribute( "FogCloudEnabled" ); >;
float4 g_vFogCloudColor     	< Attribute( "FogCloudColor" ); >;
float g_flFogCloudStrength  	< Attribute( "FogCloudStrength" ); >;
float g_flFogCloudWorldSize 	< Attribute( "FogCloudWorldSize" ); >;
float2 g_vFogCloudVelocity  	< Attribute( "FogCloudVelocity" ); >;
float g_flFogCloudHeight    	< Attribute( "FogCloudHeight" ); >;
float g_flFogCloudContrast  	< Attribute( "FogCloudContrast" ); >;

float3 g_vFogCameraPosition 	< Attribute( "FogCameraPosition" ); >;
float3 g_vFogCameraForward  	< Attribute( "FogCameraForward" ); >;
float g_flFogViewBlend      	< Attribute( "FogViewBlend" ); >;

float SpatialDirector_FogContrast( float value, float contrast )
{
	return saturate( (value - 0.5f ) * max( contrast, 0.001f ) + 0.5f );
}

float2 SpatialDirector_ProjectToPlane( float3 positionWs, float planeHeight, float2 fallbackPosition )
{
	float3 rayDirection = normalize( g_vFogCameraForward );

	if ( abs( rayDirection.z ) <= 0.0001f ) 
		return fallbackPosition;

	float t = (planeHeight - positionWs.z ) / rayDirection.z;

	return (positionWs + rayDirection * t ).xy;
}

float2 SpatialDirector_WorldLayerUv( float2 worldPosition, float worldSize, float2 velocity )
{
	float safeSize = max( worldSize, 1.0f );

	return worldPosition / safeSize + velocity / safeSize * g_flTime;
}

float2 SpatialDirector_FogDetailPosition( float3 positionWs, float3 originWs )
{
	return SpatialDirector_ProjectToPlane( positionWs, originWs.z, originWs.xy );
}

float2 SpatialDirector_CloudPosition( float3 positionWs, float3 originWs )
{
	return SpatialDirector_ProjectToPlane( positionWs, originWs.z + g_flFogCloudHeight, originWs.xy );
}

float SpatialDirector_FogDetailSample( float3 positionWs, float3 originWs )
{
	if ( g_flFogDetailEnabled < 0.5f )
		return 0.5f;

	float2 worldPosition = SpatialDirector_FogDetailPosition( positionWs, originWs );

	float2 uv = SpatialDirector_WorldLayerUv( worldPosition, g_flFogDetailWorldSize, g_vFogDetailVelocity );

	float detail = g_tFogDetailTexture.SampleLevel( g_sFogDetailSampler, uv, 0 ).r;

	return SpatialDirector_FogContrast( detail, g_flFogDetailContrast );
}

float SpatialDirector_FogDetailOffset( float3 positionWs, float3 originWs, float mask )
{
	if ( mask <= 0.001f || g_flFogDetailEnabled < 0.5f ) 
		return 0.0f;

	float detail = SpatialDirector_FogDetailSample( positionWs, originWs );

	return (detail - 0.5f ) * 2.0f * g_flFogDetailStrength * mask;
}

float SpatialDirector_CloudMask( float3 positionWs, float3 originWs, float fogMask )
{
	if ( g_flFogCloudEnabled < 0.5f || fogMask <= 0.001f ) 
		return 0.0f;

	float outsideBlend = 1.0f - saturate( g_flFogViewBlend );

	if ( outsideBlend <= 0.001f ) 
		return 0.0f;

	float2 worldPosition = SpatialDirector_CloudPosition( positionWs, originWs );

	float2 uv = SpatialDirector_WorldLayerUv( worldPosition, g_flFogCloudWorldSize, g_vFogCloudVelocity );

	float cloud = g_tFogCloudTexture.SampleLevel( g_sFogCloudSampler, uv, 0 ).r;

	cloud = SpatialDirector_FogContrast( cloud, g_flFogCloudContrast );

	return saturate( cloud * g_flFogCloudStrength * fogMask * outsideBlend );
}

#endif
