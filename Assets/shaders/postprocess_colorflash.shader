HEADER
{
    Description = "";
}

MODES
{
    Default();
	Forward();
}

COMMON
{
    #include "postprocess/shared.hlsl"
	#define CUSTOM_MATERIAL_INPUTS
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
    #include "postprocess/common.hlsl"

    RenderState( DepthWriteEnable, false );
    RenderState( DepthEnable, false );

	Texture2D g_tColorBuffer   < Attribute( "ColorBuffer" );  	SrgbRead( true ); >;
	SamplerState Sampler < Filter(POINT); AddressU(WRAP); AddressV(WRAP); >;

	float3 g_flTintColor < Attribute( "screen_flash_color" ); >;

	float g_flStrength < Attribute( "screen_flash_strength" ); >;

    float4 MainPs( PixelInput i ) : SV_Target0 
	{
	
		float3 mainColor = g_tColorBuffer.Sample( Sampler, i.vTexCoord ).rgb;

		if (g_flStrength == 0)
			return float4(mainColor, 1.0f);
		
		float3 greyscale = (mainColor.x + mainColor.y + mainColor.z) * 0.333333;
		float3 outColor = greyscale * greyscale;
		outColor -= outColor * g_flTintColor;
		outColor += (mainColor * g_flTintColor) * 2;
		outColor = (outColor * outColor) + (outColor * 0.1);

		float2 uv = i.vTexCoord;
		uv *= 1.0f - uv.yx;
		float vignette = (uv.x*uv.y) * 24.0f;
		vignette = pow(vignette, 0.75f);
		outColor *= vignette;
		outColor += g_flTintColor * greyscale * greyscale * saturate(1 - vignette);
		outColor *= g_flTintColor * 0.5 + outColor * 0.5;
		outColor += g_flTintColor * outColor;
		outColor += g_flTintColor * 0.05;

        return float4(lerp(mainColor, outColor, g_flStrength), 1.0f);
    }
}
