MODES
{
    Forward();
}

COMMON
{
	#include "postprocess/shared.hlsl"
}

struct VertexInput
{
    #include "includes/pp_vsi.hlsl"
};

struct PixelInput
{
	#include "includes/pp_psi.hlsl"
};

VS
{
	#include "includes/pp_vs.hlsl"
}

PS
{
    #include "procedural.hlsl"
    #include "common/utils/normal.hlsl"

	Texture2D g_tColorBuffer   < Attribute( "ColorBuffer" );  	SrgbRead( true ); >;
	SamplerState Sampler < Filter(ANISOTROPIC); AddressU(MIRROR); AddressV(MIRROR); >;

	RenderState( DepthWriteEnable, false );
	RenderState( DepthEnable, false );

	float3 blend_rnm(float3 n1, float3 n2)
	{
		float3 t = n1.xyz * float3(2, 2, 2) + float3(-1, -1, 0);
		float3 u = n2.xyz * float3(-2, -2, 2) + float3(1, 1, -1);
		float3 r = t * dot(t, u) - u * t.z;
		return (r);
	}

	Texture2D g_tNormalA   < Attribute( "NormalA" );  	SrgbRead( false ); >;
	Texture2D g_tNormalB   < Attribute( "NormalB" );  	SrgbRead( false ); >;
	Texture2D g_tNormalC   < Attribute( "NormalC" );  	SrgbRead( false ); >;
	Texture2D g_tNormalD   < Attribute( "NormalD" );  	SrgbRead( false ); >;
	Texture2D g_tNoiseAll   < Attribute( "NoiseAll" );  SrgbRead( false ); >;
	
    float4 MainPs( PixelInput i ) : SV_Target0
    {
			
		float2 f2NoiseA = g_tNoiseAll.Sample( g_sAniso, i.vTexCoord.xy * 0.5 ).xx 	* 0.25f;	// fill both axis with the same value
		float2 f2NoiseB = g_tNoiseAll.Sample( g_sAniso, i.vTexCoord.xy * 0.1 ).yy 	* 0.25f;	// fill both axis with the same value
		float2 f2NoiseC = g_tNoiseAll.Sample( g_sAniso, i.vTexCoord.xy * 0.1 ).zz 	* 5.55f;	// fill both axis with the same value
		float2 f2NoiseD = g_tNoiseAll.Sample( g_sAniso, i.vTexCoord.xy * 0.1 ).ww 	* 2.1f;	// fill both axis with the same value

		float3 f3NormalA = g_tNormalA.Sample( g_sAniso, i.vTexCoord.xy + (g_flTime * 0.2) + f2NoiseA).xyz;
		float3 f3NormalB = g_tNormalB.Sample( g_sAniso, i.vTexCoord.xy - (g_flTime * 0.1) - f2NoiseB).xyz;
		float3 f3NormalC = g_tNormalC.Sample( g_sAniso, i.vTexCoord.xy + (g_flTime * 0.5) + f2NoiseC ).xyz;
		float3 f3NormalD = g_tNormalD.Sample( g_sAniso, i.vTexCoord.xy - (g_flTime * 0.1) - f2NoiseD).xyz;
																		
		float3 f3NormalFinalA 	= PackNormal3D(blend_rnm(f3NormalA, f3NormalB));
		float3 f3NormalFinalB	= PackNormal3D(blend_rnm(f3NormalC, f3NormalD));
		float3 f3NormalFinalC	= lerp(float3(1, 1, 1), PackNormal3D(blend_rnm(f3NormalFinalA, f3NormalFinalB)), 0.01f);

		float4 initcolor = g_tColorBuffer.SampleLevel( Sampler, i.vTexCoord * f3NormalFinalC.xy, 1 ).rgba;
		float3 intermediateColor = initcolor.rgb * float3(0.7, 0.83, 1);

		return float4(intermediateColor, initcolor.w);
    }
}