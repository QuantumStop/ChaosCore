#ifndef WORLD_OCCLUSION_RENDERING_HLSL
#define WORLD_OCCLUSION_RENDERING_HLSL

struct WorldOcclusionRenderParams
{
	float Strength;
	float Feather;
	float Bias;

	// 1 = unchanged
	// >1 = tighter
	// <1 = broader
	float Contrast;

	float DitherStrength;

	float NoiseScale;
	float NoiseStrength;

	// Extra overlap contribution
	float OverlapStrength;

	// Overlap shaping exponent
	float OverlapPower;

	float DistanceStart;
	float DistanceEnd;
	float DistanceStrength;
};

float WorldOcclusion_SafePow( float value, float power )
{
	value = saturate( value );

	power = max( power, 0.001f );

	return pow( value, power );
}

float WorldOcclusion_Remap01( float value, float minValue, float maxValue )
{
	float range = max( maxValue - minValue, 0.0001f );

	return saturate( (value - minValue ) / range );
}

float WorldOcclusion_AdjustContrast( float value, float contrast )
{
	value = saturate( value );

	contrast = max( contrast, 0.001f );

	return saturate( (value - 0.5f ) * contrast + 0.5f );
}

// Hash without Sine
// David Hoskins
// https://www.shadertoy.com/view/4djSRW

float WorldOcclusion_Hash12( float2 p )
{
	float3 p3 = frac( float3( p.x, p.y, p.x ) * 0.1031f );

	p3 += dot( p3, p3.yzx + 33.33f );

	return frac( (p3.x + p3.y ) * p3.z );
}

// Value noise

float WorldOcclusion_ValueNoise( float2 p )
{
	float2 cell = floor( p );

	float2 local = frac( p );

	local = local * local * (3.0f - 2.0f * local );

	float a = WorldOcclusion_Hash12( cell );

	float b = WorldOcclusion_Hash12( cell + float2( 1.0f, 0.0f ) );

	float c = WorldOcclusion_Hash12( cell + float2( 0.0f, 1.0f ) );

	float d = WorldOcclusion_Hash12( cell + float2( 1.0f, 1.0f ) );

	float ab = lerp( a, b, local.x );

	float cd = lerp( c, d, local.x );

	return lerp( ab, cd, local.y );
}

// Cheap fallback world-space procedural noise.
//
// This is intentionally still XY-based because it is only a fallback.

float WorldOcclusion_GetNoise( float3 worldPosition, float noiseScale )
{
	if ( noiseScale <= 0.0001f ) return 0.5f;

	return WorldOcclusion_ValueNoise( worldPosition.xy * noiseScale );
}

// Triplanar projection

float3 WorldOcclusion_TriplanarWeights( float3 normal, float sharpness )
{
	float3 weights = pow( abs( normal ), max( sharpness, 1.0f ) );

	float sum = weights.x + weights.y + weights.z;

	return weights / max( sum, 0.0001f );
}

// Returns UVs for each world axis
//
// X-facing surface:
//     YZ projection
//
// Y-facing surface:
//     XZ projection
//
// Z-facing surface:
//     XY projection

void WorldOcclusion_GetTriplanarUVs( float3 worldPosition, float worldSize, float2 offset, out float2 uvX, out float2 uvY, out float2 uvZ )
{
	float scale = 1.0f / max( worldSize, 0.001f );

	float3 p = worldPosition * scale;

	uvX = p.zy + offset;

	uvY = p.xz + offset;

	uvZ = p.xy + offset;
}

// Generic grayscale triplanar sample

float WorldOcclusion_TriplanarSample( Texture2D textureInput, SamplerState samplerInput, float3 worldPosition, 
	float3 worldNormal, float worldSize, float sharpness, float2 offset )
{
	float2 uvX;
	float2 uvY;
	float2 uvZ;

	WorldOcclusion_GetTriplanarUVs( worldPosition, worldSize, offset, uvX, uvY, uvZ );

	float3 weights = WorldOcclusion_TriplanarWeights( worldNormal, sharpness );

	float sampleX = textureInput.SampleLevel( samplerInput, uvX, 0 ).r;

	float sampleY = textureInput.SampleLevel( samplerInput, uvY, 0 ).r;

	float sampleZ = textureInput.SampleLevel( samplerInput, uvZ, 0 ).r;

	return sampleX * weights.x + sampleY * weights.y + sampleZ * weights.z;
}

// RGB triplanar sample

float3 WorldOcclusion_TriplanarSampleRGB( Texture2D textureInput, SamplerState samplerInput, float3 worldPosition, 
	float3 worldNormal, float worldSize, float sharpness, float2 offset )
{
	float2 uvX;
	float2 uvY;
	float2 uvZ;

	WorldOcclusion_GetTriplanarUVs( worldPosition, worldSize, offset, uvX, uvY, uvZ );

	float3 weights = WorldOcclusion_TriplanarWeights( worldNormal, sharpness );

	float3 sampleX = textureInput.SampleLevel( samplerInput, uvX, 0 ).rgb;

	float3 sampleY = textureInput.SampleLevel( samplerInput, uvY, 0 ).rgb;

	float3 sampleZ = textureInput.SampleLevel( samplerInput, uvZ, 0 ).rgb;

	return sampleX * weights.x + sampleY * weights.y + sampleZ * weights.z;
}

// Bayer 4x4 ordered dithering matrix

float WorldOcclusion_Bayer4x4( int2 pixel )
{
	int x = pixel.x & 3;

	int y = pixel.y & 3;

	static const float Bayer[16] =
	{
		0.0f  / 16.0f,
		8.0f  / 16.0f,
		2.0f  / 16.0f,
		10.0f / 16.0f,

		12.0f / 16.0f,
		4.0f  / 16.0f,
		14.0f / 16.0f,
		6.0f  / 16.0f,

		3.0f  / 16.0f,
		11.0f / 16.0f,
		1.0f  / 16.0f,
		9.0f  / 16.0f,

		15.0f / 16.0f,
		7.0f  / 16.0f,
		13.0f / 16.0f,
		5.0f  / 16.0f
	};

	return Bayer[ y * 4 + x ];
}

float WorldOcclusion_DitherThreshold( float2 screenPosition )
{
	return WorldOcclusion_Bayer4x4( int2( screenPosition ) );
}

// Textured dither

float WorldOcclusion_ScreenTextureDither( Texture2D textureInput, SamplerState samplerInput, float2 screenUv, float tiling )
{
	return textureInput.SampleLevel( samplerInput, screenUv * max( tiling, 0.001f ), 0 ).r;
}

float WorldOcclusion_WorldTextureDither(
	Texture2D textureInput,
	SamplerState samplerInput,
	float3 worldPosition,
	float3 worldNormal,
	float worldSize,
	float sharpness,
	float2 offset )
{
	return WorldOcclusion_TriplanarSample( textureInput, samplerInput, worldPosition, worldNormal, worldSize, sharpness, offset );
}

// Allows smooth control between:
//
//   0 = screen-space dither
//   1 = world-space triplanar dither

float WorldOcclusion_BlendDither( float screenDither, float worldDither, float worldBlend )
{
	return lerp( screenDither, worldDither, saturate( worldBlend ) );
}

// Mask edge helpers:

// 0 at completely clear / completely hidden.
// 1 around the 0.5 transition.

float WorldOcclusion_EdgeFactor( float mask )
{
	mask = saturate( mask );

	return saturate( 1.0f - abs( mask * 2.0f - 1.0f ) );
}

// Apply noise primarily at the edge of an occlusion mask.
//
// Keeps fully hidden/visible surfaces stable.

float WorldOcclusion_ApplyEdgeNoise( float mask, float noise, float strength )
{
	float edge = WorldOcclusion_EdgeFactor( mask );

	float centeredNoise = noise - 0.5f;

	mask += centeredNoise * strength * edge;

	return saturate( mask );
}

// Similar idea for non clipping dither.
//
// Useful for FoW post processing.

float WorldOcclusion_ApplySoftDither( float mask, float dither, float strength )
{
	strength = saturate( strength );

	if ( strength <= 0.0001f ) return mask;

	float edge = WorldOcclusion_EdgeFactor( mask );

	float centeredDither = dither - 0.5f;

	mask += centeredDither * strength * edge;

	return saturate( mask );
}

// Distance

float WorldOcclusion_DistanceMask( float distance, float startDistance, float endDistance )
{
	return WorldOcclusion_Remap01( distance, startDistance, endDistance );
}

// Overlap

float WorldOcclusion_ShapeOverlap( float overlap, float power )
{
	return WorldOcclusion_SafePow( overlap, max( power, 0.001f ) );
}

// Reinforce an existing mask.
//
// A zero source mask stays zero.
//
// That prevents distance / overlap from generating occlusion where the
// visibility says the area is fully clear.

float WorldOcclusion_ReinforceMask( float mask, float reinforcement, float strength )
{
	mask = saturate( mask );

	reinforcement = saturate( reinforcement );

	strength = saturate( strength );

	float reinforced = saturate( mask + reinforcement * strength * (1.0f - mask ) );

	return lerp( mask, reinforced, mask );
}

// Generic mask

float WorldOcclusion_PresentMask( float sourceMask, float overlap, float worldDistance, float3 worldPosition, WorldOcclusionRenderParams p )
{
	float mask = saturate( sourceMask );

	mask = saturate( mask - p.Bias );
	mask = WorldOcclusion_SafePow( mask, max( p.Contrast, 0.001f ) );

	if ( p.NoiseStrength > 0.0001f )
	{
		float noise = WorldOcclusion_GetNoise( worldPosition, p.NoiseScale );

		mask = WorldOcclusion_ApplyEdgeNoise( mask, noise, p.NoiseStrength );
	}

	if ( p.Feather > 0.0001f )
	{
		float halfFeather = p.Feather * 0.5f;

		mask = smoothstep( 0.5f - halfFeather, 0.5f + halfFeather, mask );
	}

	if ( p.OverlapStrength > 0.0001f )
	{
		float shapedOverlap = WorldOcclusion_ShapeOverlap( overlap, p.OverlapPower );

		mask = WorldOcclusion_ReinforceMask( mask, shapedOverlap, p.OverlapStrength );
	}

	if ( p.DistanceStrength > 0.0001f )
	{
		float distanceMask = WorldOcclusion_DistanceMask( worldDistance, p.DistanceStart, p.DistanceEnd );

		mask = WorldOcclusion_ReinforceMask( mask, distanceMask, p.DistanceStrength );
	}

	mask *= saturate( p.Strength );

	return saturate( mask );
}

// Opaque dither clip.
//
// Used by world cutaway surfaces.

bool WorldOcclusion_ShouldClip( float occlusion, float2 screenPosition, float ditherStrength )
{
	occlusion = saturate( occlusion );

	ditherStrength = saturate( ditherStrength );

	if ( ditherStrength <= 0.0001f )
	{
		return occlusion >= 0.5f;
	}

	float ditherThreshold = WorldOcclusion_DitherThreshold( screenPosition );

	float threshold = lerp( 0.5f, ditherThreshold, ditherStrength );

	return occlusion > threshold;
}

bool WorldOcclusion_ShouldClipCustom( float occlusion, float ditherThreshold, float ditherStrength )
{
	occlusion = saturate( occlusion );

	ditherThreshold = saturate( ditherThreshold );

	ditherStrength = saturate( ditherStrength );

	float threshold = lerp( 0.5f, ditherThreshold, ditherStrength );

	return occlusion > threshold;
}

void WorldOcclusion_Clip( float occlusion, float2 screenPosition, float ditherStrength )
{
	if ( WorldOcclusion_ShouldClip( occlusion, screenPosition, ditherStrength ) )
	{
		clip( -1 );
	}
}

void WorldOcclusion_ClipCustom( float occlusion, float ditherThreshold, float ditherStrength )
{
	if ( WorldOcclusion_ShouldClipCustom( occlusion, ditherThreshold, ditherStrength ) )
	{
		clip( -1 );
	}
}

// Color application

float3 WorldOcclusion_ApplyColor( float3 sourceColor, float3 occlusionColor, float occlusion )
{
	return lerp( sourceColor, occlusionColor, saturate( occlusion ) );
}

float4 WorldOcclusion_ApplyColor( float4 sourceColor, float4 occlusionColor, float occlusion )
{
	float amount = saturate( occlusion * occlusionColor.a );

	float4 result = sourceColor;

	result.rgb = lerp( sourceColor.rgb, occlusionColor.rgb, amount );

	return result;
}

#endif
