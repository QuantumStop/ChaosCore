HEADER
{
	Description = "World space distance fog";
}

MODES
{
	Default();
	Forward();
}

COMMON
{
	#include "postprocess/shared.hlsl"
	#include "includes/world_occlusion_rendering.hlsl"
}

struct VertexInput
{
	float3 vPositionOs : POSITION < Semantic( PosXyz ); >;
	float2 vTexCoord : TEXCOORD0 < Semantic( LowPrecisionUv ); >;
};

struct PixelInput
{
	float2 vTexCoord : TEXCOORD0;

	#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs : SV_Position;
	#endif
};

VS
{
	PixelInput MainVs( VertexInput i )
	{
		PixelInput o;

		o.vPositionPs = float4( i.vPositionOs.xyz, 1.0f );
		o.vPositionPs.z = 1.0f;
		o.vTexCoord = i.vTexCoord;

		return o;
	}
}

PS
{
	#include "postprocess/common.hlsl"
	#include "postprocess/functions.hlsl"
	#include "common/classes/Depth.hlsl"
	#include "includes/spatial_director_patterns.hlsl"
	#include "includes/spatial_director_fog_layers.hlsl"

	RenderState( DepthWriteEnable, false );
	RenderState( DepthEnable, false );

	Texture2D g_tColorBuffer < Attribute( "ColorBuffer" ); SrgbRead( true );>;

	SamplerState g_sSceneSampler < Filter( BILINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	Texture2D g_tFogVisibility < Attribute( "FogVisibility" ); SrgbRead( false ); >;

	SamplerState g_sFogSampler < Filter( POINT ); AddressU( CLAMP ); AddressV( CLAMP ); >;

	float2 g_vFogMapMin 			< Attribute( "FogMapMin" ); >;
	float2 g_vFogMapSize 			< Attribute( "FogMapSize" ); >;
	float3 g_vFogVisionPosition 	< Attribute( "FogVisionPosition" ); >;

	float4 g_vFogOfWarColor 		< Attribute( "FogOfWarColor" ); >;
	float2 g_vFogHeightRange 		< Attribute( "FogHeightRange" ); >;

	float g_flFogFarStart 			< Attribute( "FogFarStart" ); >;
	float g_flFogFarEnd 			< Attribute( "FogFarEnd" ); >;
	float g_flFogFarStrength 		< Attribute( "FogFarStrength" ); >;
	float g_flFogFarOverlapStrength < Attribute( "FogFarOverlapStrength" ); >;
	float g_flFogFarOverlapPower 	< Attribute( "FogFarOverlapPower" ); >;

	float g_flFogPeekEnabled 		< Attribute( "FogPeekEnabled" ); >;
	float g_flFogPeekStrength 		< Attribute( "FogPeekStrength" ); >;
	float g_flFogPeekViewBlend 		< Attribute( "FogPeekViewBlend" ); >;

	float3 g_vFogPeekStartWs 		< Attribute( "FogPeekStartWs" ); >;
	float3 g_vFogPeekEndWs 			< Attribute( "FogPeekEndWs" ); >;
	float3 g_vFogPeekHitWs 			< Attribute( "FogPeekHitWs" ); >;
	float3 g_vFogPeekRightWs 		< Attribute( "FogPeekRightWs" ); >;
	float3 g_vFogPeekUpWs 			< Attribute( "FogPeekUpWs" ); >;

	float g_flFogPeekShape 			< Attribute( "FogPeekShape" ); >;
	float2 g_vFogPeekSize 			< Attribute( "FogPeekSize" ); >;
	float g_flFogPeekFeather 		< Attribute( "FogPeekFeather" ); >;
	float g_flFogPeekBehindHit 		< Attribute( "FogPeekBehindHit" ); >;

	float4 g_vFogPeekColor 			< Attribute( "FogPeekColor" ); >;
	float g_flFogPeekSoftness 		< Attribute( "FogPeekSoftness" ); >;
	float g_flFogPeekNoise 			< Attribute( "FogPeekNoise" ); >;

	float g_flFogPeekAmount			< Attribute( "FogPeekAmount" ); >;
	float g_flFogInteriorRadius 	< Attribute( "FogInteriorRadius" ); >;
	float g_flFogInteriorFade 		< Attribute( "FogInteriorFade" ); >;

	float g_flSpatialDirectorDebug 	 < Attribute( "SpatialDirectorDebug" ); >;
	float g_flFogArcEnabled 		 < Attribute( "FogArcEnabled" ); >;
	float3 g_vFogArcDirection 		 < Attribute( "FogArcDirection" ); >;
	float g_flFogArcCosine 			 < Attribute( "FogArcCosine" ); >;
	float g_flFogOutsideArcDim 		 < Attribute( "FogOutsideArcDim" ); >;
	float g_flFogArcHalfAngle 		 < Attribute( "FogArcHalfAngle" ); >;
	float g_flFogArcFeather 		 < Attribute( "FogArcFeather" ); >;
	float g_flFogArcUseDetail 		 < Attribute( "FogArcUseDetail" ); >;
	float g_flFogArcLinearDim 		 < Attribute( "FogArcLinearDim" ); >;
	float g_flFogArcNearDim 		 < Attribute( "FogArcNearDim" ); >;
	float g_flFogArcDimStart 		 < Attribute( "FogArcDimStart" ); >;
	float g_flFogArcDimEnd 			 < Attribute( "FogArcDimEnd" ); >;
	Texture2D g_tFogArcDetailTexture < Attribute( "FogArcDetailTexture" ); >;
	float g_flFogArcDetailStrength   < Attribute( "FogArcDetailStrength" ); >;
	float g_flFogArcDetailWorldSize  < Attribute( "FogArcDetailWorldSize" ); >;
	float2 g_vFogArcDetailVelocity   < Attribute( "FogArcDetailVelocity" ); >;
	float g_flFogArcDetailContrast   < Attribute( "FogArcDetailContrast" ); >;


	float IsInsideFogMap( float2 uv )
	{
		return uv.x >= 0.0f && uv.y >= 0.0f && uv.x <= 1.0f 
			&& uv.y <= 1.0f ? 1.0f : 0.0f;
	}


	float FogPeekShapeMask( float2 localPosition )
	{
		float2 halfSize = max( abs( g_vFogPeekSize ), float2( 0.001f, 0.001f ) );
		float2 normalized = abs( localPosition ) / halfSize;
		float shapeDistance = length( normalized );

		if ( g_flFogPeekShape > 0.5f && g_flFogPeekShape < 1.5f )
			shapeDistance = max( normalized.x, normalized.y );

		float feather = g_flFogPeekFeather / max( min( halfSize.x, halfSize.y ), 0.001f );
		feather *= max( g_flFogPeekSoftness, 0.001f );

		return 1.0f - smoothstep( 1.0f, 1.0f + feather, shapeDistance );
	}


	float FogPeekMask( float3 positionWs )
	{
		if ( g_flFogPeekEnabled < 0.5f || g_flFogPeekStrength <= 0.001f )
			return 0.0f;

		float outsideBlend = 1.0f - saturate( g_flFogPeekViewBlend );

		if ( outsideBlend <= 0.001f )
			return 0.0f;

		float3 cameraToTarget = g_vFogPeekStartWs - g_vFogPeekEndWs;
		float targetDistance = length( cameraToTarget );

		if ( targetDistance <= 0.001f )
			return 0.0f;

		float3 axis = cameraToTarget / targetDistance;
		float along = dot( positionWs - g_vFogPeekEndWs, axis );
		float hitDistance = dot( g_vFogPeekHitWs - g_vFogPeekEndWs, axis );
		float depthFeather = max( g_flFogPeekFeather, 1.0f );

		float enterMask = smoothstep( hitDistance - depthFeather, hitDistance + max( g_flFogPeekBehindHit, 0.0f ), along );
		float leaveMask = 1.0f - smoothstep( targetDistance, targetDistance + depthFeather, along );

		float depthMask = saturate( enterMask * leaveMask );

		if ( depthMask <= 0.001f )
			return 0.0f;

		float3 axisPoint = g_vFogPeekEndWs + axis * along;
		float3 radialDelta = positionWs - axisPoint;

		float2 localPosition = float2( dot( radialDelta, g_vFogPeekRightWs ), dot( radialDelta, g_vFogPeekUpWs ) );

		float shapeMask = FogPeekShapeMask( localPosition );

		if ( shapeMask <= 0.001f )
			return 0.0f;

		if ( g_flFogPeekNoise > 0.0001f )
		{
			float noise = SpatialDirector_NoiseOffset( localPosition, g_vFogPeekSize );

			float edge = WorldOcclusion_EdgeFactor( shapeMask );

			shapeMask = saturate( shapeMask + noise * g_flFogPeekNoise * edge 
				/ max( min( g_vFogPeekSize.x, g_vFogPeekSize.y ), 1.0f ) );
		}

		return saturate( shapeMask * depthMask * g_flFogPeekStrength * outsideBlend );
	}


	float FogInteriorMask( float3 positionWs )
	{
		float peekMask = FogPeekMask( positionWs );

		if ( peekMask <= 0.001f )
			return 0.0f;

		float distanceFromTarget = length( positionWs - g_vFogPeekStartWs );

		float playerClear = 1.0f - smoothstep( max( g_flFogInteriorRadius, 0.0f ), max( g_flFogInteriorRadius, 0.0f ) 
			+ max( g_flFogInteriorFade, 0.001f ), distanceFromTarget );

		return saturate( peekMask * (1.0f - playerClear) );
	}


	float GetVisibilityDebug( float3 positionWs )
	{
		float2 mapSize = max( abs( g_vFogMapSize ), float2( 0.001f, 0.001f ) );
		float2 uv = (positionWs.xy - g_vFogMapMin) / mapSize;

		if ( IsInsideFogMap( uv ) < 0.5f )
			return 0.0f;

		return saturate( g_tFogVisibility.SampleLevel( g_sFogSampler, uv, 0 ).r );
	}


	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 screenUv = i.vTexCoord.xy;

		float4 sourceColor = g_tColorBuffer.SampleLevel( g_sSceneSampler, screenUv, 0 );

		if ( g_flSpatialDirectorDebug > 3.5f && g_flSpatialDirectorDebug < 5.5f )
			return sourceColor;

		float2 screenPosition = screenUv * g_vViewportSize;
		float3 positionWs = Depth::GetWorldPosition( screenPosition );

		float heightMask = positionWs.z >= g_vFogHeightRange.x 
			&& positionWs.z <= g_vFogHeightRange.y ? 1.0f : 0.0f;

		float playerDistance = distance( positionWs.xy, g_vFogVisionPosition.xy );

		if ( g_flCutawayUseNoise > 0.5f )
		{
			float noiseRange = max( g_flFogFarEnd, 1.0f );

			float noiseOffset = SpatialDirector_NoiseOffset( positionWs.xy - g_vFogVisionPosition.xy, 
				float2( noiseRange, noiseRange ) );

			playerDistance += noiseOffset;
		}

		float distanceMask = WorldOcclusion_DistanceMask( playerDistance, g_flFogFarStart, g_flFogFarEnd );

		if ( g_flCutawayDitherAmount > 0.0001f )
		{
			float dither = WorldOcclusion_DitherThreshold( screenPosition / max( g_flCutawayDitherScale, 0.001f ) );

			distanceMask = WorldOcclusion_ApplySoftDither( distanceMask, dither, g_flCutawayDitherAmount );
		}

		float fogAmount = saturate( distanceMask * g_flFogFarStrength * heightMask );
		float overlap = WorldOcclusion_ShapeOverlap( distanceMask, g_flFogFarOverlapPower );

		fogAmount = saturate( fogAmount + overlap * g_flFogFarOverlapStrength * (1.0f - fogAmount) * heightMask );

		fogAmount = saturate( fogAmount + SpatialDirector_FogDetailOffset( positionWs, g_vFogPeekStartWs, fogAmount ) );

		float4 foggedColor = WorldOcclusion_ApplyColor( sourceColor, g_vFogOfWarColor, fogAmount );

		float interiorMask = FogInteriorMask( positionWs );

		if ( interiorMask > 0.0001f )
		{
			float interiorDarkness = saturate( g_flFogPeekAmount + SpatialDirector_FogDetailOffset( positionWs, 
				g_vFogPeekStartWs, interiorMask ) );

			float4 interiorColor = WorldOcclusion_ApplyColor( sourceColor, g_vFogPeekColor, interiorDarkness );

			foggedColor = lerp( foggedColor, interiorColor, interiorMask );
		}

		float cloudBaseMask = saturate( max( fogAmount, interiorMask ) );
		float cloudMask = SpatialDirector_CloudMask( positionWs, g_vFogPeekStartWs, cloudBaseMask );

		if ( cloudMask > 0.0001f )
		{
			foggedColor.rgb = lerp( foggedColor.rgb, g_vFogCloudColor.rgb, cloudMask * saturate( g_vFogCloudColor.a ) );
		}

		// Enforce the current facing independently of the slower visibility texture update.
		float2 arcOffset = positionWs.xy - g_vFogVisionPosition.xy;
		float arcDistance = length( arcOffset );
		float arcVisible = g_flFogArcEnabled < 0.5f || arcDistance <= 0.01f 
			|| dot( arcOffset / max( arcDistance, 0.0001f ), g_vFogArcDirection.xy ) >= g_flFogArcCosine ? 1.0f : 0.0f;
			
		// Feather only the visual dimming, culling retains the exact arc boundary.
		float angleFromFacing = acos( clamp( dot( arcOffset / max( arcDistance, 0.0001f ), g_vFogArcDirection.xy ), -1.0f, 1.0f ) );
		float remainingAngle = max( 3.14159265f - g_flFogArcHalfAngle, 0.0001f );
		
		float feather = min( max( g_flFogArcFeather, max( fwidth( angleFromFacing ), 0.0001f ) ), remainingAngle );
		float outsideArcMask = smoothstep( g_flFogArcHalfAngle, g_flFogArcHalfAngle + feather, angleFromFacing );
		outsideArcMask *= g_flFogArcEnabled > 0.5f && arcDistance > 0.01f ? 1.0f : 0.0f;

		// Distance uses unmodified XY distance, independent of far fog noise.
		float dimDensity = saturate( g_flFogOutsideArcDim );
		if ( g_flFogArcLinearDim > 0.5f )
		{
			float distanceBlend = saturate( (arcDistance - g_flFogArcDimStart) / max( g_flFogArcDimEnd - g_flFogArcDimStart, 1.0f ) );
			dimDensity = lerp( saturate( g_flFogArcNearDim ), dimDensity, distanceBlend );
		}

		// Separate texture and controls from the main Fog Detail layer.
		if ( g_flFogArcUseDetail > 0.5f )
		{
			float2 detailPosition = SpatialDirector_FogDetailPosition( positionWs, g_vFogVisionPosition );
			float2 detailUv = SpatialDirector_WorldLayerUv( detailPosition, g_flFogArcDetailWorldSize, g_vFogArcDetailVelocity );
			float detail = g_tFogArcDetailTexture.SampleLevel( g_sFogDetailSampler, detailUv, 0 ).r;
			
			detail = SpatialDirector_FogContrast( detail, g_flFogArcDetailContrast );
			dimDensity = saturate( dimDensity + (detail - 0.5f) * 2.0f * g_flFogArcDetailStrength * dimDensity );
		}
		
		float outsideArcDim = outsideArcMask * heightMask * dimDensity;
		foggedColor.rgb *= 1.0f - outsideArcDim;

		if ( g_flSpatialDirectorDebug > 2.5f && g_flSpatialDirectorDebug < 3.5f )
		{
			return float4( GetVisibilityDebug( positionWs ) * arcVisible, distanceMask, interiorMask, 1.0f );
		}

		return foggedColor;
	}
}
