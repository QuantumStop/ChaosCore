HEADER
{
	Description = "Template Shader for S&box";
}

MODES
{
	Forward();
	Depth( S_MODE_DEPTH );
}

FEATURES
{
    #include "common/features.hlsl"
}

COMMON
{
	#include "common/shared.hlsl"
	#define CUSTOM_MATERIAL_INPUTS
    #ifndef S_ALPHA_TEST
	#define S_ALPHA_TEST 0
	#endif
	#ifndef S_TRANSLUCENT
	#define S_TRANSLUCENT 1
	#endif
    #define S_RENDER_BACKFACES 1
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );
		return FinalizeVertex( o );
	}
}

//=========================================================================================================================

PS
{
    #include "sbox_pixel.fxc"
    #include "common/classes/Depth.hlsl"

	StaticCombo( S_MODE_DEPTH, 0..1, Sys(ALL) );

//	Spherical Gaussian UE4 Schlick but it kinda wide
	float3 FresnelCool(float U, float x)
	{
		return pow(pow(2, x), (-5.55473f * U - 6.98316) * U);
	}

	float DepthFade(float FadeDistance, float SceneDepth, float PixelDepth)
	{
		return saturate((SceneDepth - PixelDepth) / max(FadeDistance, 0.00001));
	}

	float ConvertW(float depth)
	{
		return (pow(g_flViewportMaxZ+1, depth) - 1);
	}

	// this adds translucency
	RenderState(AlphaToCoverageEnable, false)
	RenderState(IndependentBlendEnable, true)
	RenderState(BlendEnable, true)
	RenderState(SrcBlend, ONE)
	RenderState(DstBlend, INV_SRC_ALPHA)
	RenderState(BlendOp, ADD)
	RenderState(SrcBlendAlpha, ONE)
	RenderState(DstBlendAlpha, INV_SRC_ALPHA)
	RenderState(BlendOpAlpha, ADD)

    RenderState( DepthWriteEnable, false );
    RenderState( DepthEnable, true );

	float 	g_flFresnelWidth								< UiGroup("Appearance,10/2"); Default1( 1.0 ); Range1( 0, 2 ); >;
	float 	g_flFadeDistance								< UiGroup("Appearance,10/3"); Default1( 0.5 ); Range1( 0, 1 ); >;

	float g_flIntersectionSharpness < UiGroup("Appearance,10/4"); Default1( 0.2 ); Range1( 0.01, 1 ); >;
	
	float g_flBorderDistanceFromSphereCenter < UiGroup("Appearance,10/5"); Default1( 3 ); Range1( 0.01, 10 ); >;
	SamplerState borderSampler < Filter(MIN_MAG_LINEAR_MIP_POINT); AddressU(CLAMP); AddressV(CLAMP); AddressW(CLAMP); ComparisonFunc(NEVER); MaxAniso(1); >;

	float3 uncharted2_tonemap_partial(float3 x)
	{
		float F = 0.30f;
		return ( ( x * ( 0.15f * x + 0.10f * 0.50f ) + 0.20f * 0.02f ) / ( x * ( 0.15f * x + 0.50f ) + 0.20f * 0.30f ) ) - 0.02f / 0.30f;
	}


	float3 uncharted2_filmic(float3 v)
	{
		float exposure_bias = 2.0f;
		float3 curr = uncharted2_tonemap_partial(v * exposure_bias);

		float3 W = float3(11.2f, 11.2f, 11.2f);
		float3 white_scale = float3(1.0f, 1.0f, 1.0f) / uncharted2_tonemap_partial( W );
		return curr * white_scale;
	}


	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float3 f3ViewDir = normalize(i.vPositionWithOffsetWs.xyz);
		float3 f3SurfNormal = i.vNormalWs.xyz;
		
		float3 vCameraPos = g_vCameraPositionWs;
		float3 vPositionWs = (i.vPositionWithOffsetWs.xyz + g_vCameraPositionWs.xyz);
		
		float f1NdV = saturate(dot(-f3ViewDir, f3SurfNormal));			
		
		float fresnelFull = saturate(f1NdV).x;		

		float3 posDiff = (vCameraPos - vPositionWs) / 39.37;
		float depth = Depth::GetNormalized(i.vPositionSs.xy) * 39.37;
		float notSure = -abs(dot(posDiff, -g_vCameraDirWs)) + (1 / depth);

		float clampedDepth = saturate(notSure * g_flIntersectionSharpness);
		
		float something = pow(dot(posDiff * rsqrt(dot(posDiff, posDiff)), i.vNormalWs), 2);
		float clampedSomething = saturate(something * 3);

		float varDiff = something - clampedSomething;
		float sampleArg = (varDiff * clampedDepth + clampedSomething) * g_flBorderDistanceFromSphereCenter * clampedDepth;
		
		float sampledIntensity = fresnelFull;

		float overlay = 1 / sampleArg;
		float alpha = sampledIntensity + overlay;
//		alpha = uncharted2_filmic(alpha);
	
		float baseColor = alpha;

		float3 outColor = baseColor * i.vVertexColor.rgb * 0.1f;

		return float4(outColor, 0);
	}
}
