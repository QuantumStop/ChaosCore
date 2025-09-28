MODES
{
    VrForward();
}

COMMON
{
	#include "postprocess/shared.hlsl"
}

struct VertexInput
{
    float3 vPositionOs : POSITION;
    float2 vTexCoord : TEXCOORD0;
};

struct PixelInput
{
    float2 vTexCoord : TEXCOORD0;

	#if ( PROGRAM == VFX_PROGRAM_VS )
		float4 vPositionPs : SV_Position;
	#endif

	#if ( ( PROGRAM == VFX_PROGRAM_PS ) )
		float4 vPositionSs : SV_Position;
	#endif
};

VS
{
    PixelInput MainVs( VertexInput i )
    {
        PixelInput o;
        
        o.vPositionPs = float4( i.vPositionOs.xy, 0.0f, 1.0f );
        o.vTexCoord = i.vTexCoord;
        return o;
    }
}

PS
{	
    Texture2D LookupTexture < Attribute( "LookupTexture" ); >;	
	SamplerState LookupTexture_sampler < Filter( POINT ); AddressU( CLAMP ); AddressV( CLAMP );   >;

	float ExpoBias		< Attribute( "TonemapBias" ); >;
	float GammaIn		< Attribute( "GammaIn" ); >;
	float GammaOut		< Attribute( "GammaOut" ); >;
      
	Texture2D g_tColorBuffer   < Attribute( "ColorBuffer" );  	SrgbRead( true ); >;
	SamplerState Sampler < Filter(POINT); AddressU(WRAP); AddressV(WRAP); >;
	
	#define scalemin 1
	
	void apply_chart(in float3 image, out float3 gradedCol)
	{
		const float2 iResolution = float2(4096, 64);	// float2(4096, 64); resolution of the LUT
		
		const float3 scale = float3(iResolution.y - scalemin, iResolution.y - scalemin, iResolution.y - scalemin) / iResolution.y;
		const float3 bias = float3(0.5, 0.5, 0.0) / iResolution.y;
		
		float3 lookup = saturate(image) * scale + bias;
	 
		float slice = lookup.z * iResolution.y;	
		float sliceFrac = frac(slice);	
		float sliceIdx = slice - sliceFrac;
		
		lookup.x = (lookup.x + sliceIdx) / iResolution.y;
		lookup.xy /= iResolution.xy;
		lookup.xy *= float2(iResolution.x, iResolution.y);
		
		float3 col0 = Tex2DLevelS( LookupTexture, LookupTexture_sampler, lookup.xy, 0 ).xyz;
		
		// slice interpolation
		lookup.x += 1.0 / iResolution.y * iResolution.x / iResolution.x;
		float3 col1 = Tex2DLevelS( LookupTexture, LookupTexture_sampler, lookup.xy, 0 ).xyz;
		gradedCol = col0 + (col1 - col0) * sliceFrac;
	}
// unity	
	struct ParamsLogC
	{
		float cut;
		float a;
		float b;
		float c; 
		float d; 
		float e; 
		float f;
	};
	 
	static const ParamsLogC LogC =
	{
		0.011361, // cut
		5.555556, // a
		0.047996, // b
		0.244161, // c
		0.386036, // d
		5.301883, // e
		0.092819  // f
	};
	
	float3 LinearToLogC(float3 x)
	{
		return LogC.c * log10(LogC.a * x + LogC.b) + LogC.d;
	}
	
	float3 LogCToLinear(float3 x)
	{
		return (pow(10.0, (x - LogC.d) / LogC.c) - LogC.b) / LogC.a;
	}

// idk	
	float3 FastSRGB(float3 c)
	{
		return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878);
	}

	float3 ScreenSpaceOrderedDither( float2 vScreenPos )
	{
		float3 vDither = dot( float2( 171.0, 231.0 ), vScreenPos.xy).xxx;
		vDither.rgb = frac( vDither.rgb / float3( 103.0, 71.0, 97.0 ) ) - float3( 0.5, 0.5, 0.5 );
		return ( vDither.rgb / 255.0 ) * 0.375;
	}

// frostbite
	float3 accurateSRGBToLinear (float3 sRGBCol )
	{
		float3 linearRGBLo = sRGBCol / 12.92;
		float3 linearRGBHi = pow (( sRGBCol + 0.055) / 1.055 , 2.4);
		float3 linearRGB = ( sRGBCol <= 0.04045) ? linearRGBLo : linearRGBHi;
		return linearRGB;
	}

	float3 vibrance(in float3 c, in float v) 
	{
    	float luma = dot(float3(0.212656f, 0.715158f, 0.072186f), c.rgb);
    	return luma.xxx + v.xxx * (c - luma.xxx);
	}

	RenderState( DepthWriteEnable, false );
	RenderState( DepthEnable, false );	

	DynamicCombo( D_ARRI, 0..2, Sys( PC ) );

	float3 TestColor(float2 position)
	{
		return pow(sin(position.x * 4.0 + float3(0.0, 1.0, 2.0) * 3.1415 * 2.0 / 3.0) * 0.5 + 0.5, float3(2.0, 2.0, 2.0)) * (exp(abs(position.y) * 4.0) - 1.0);
	}
	
    float4 MainPs( PixelInput i ) : SV_Target0
    {
        float4 initcolor = g_tColorBuffer.Sample( Sampler, i.vTexCoord ).rgba;
		float3 intermediateColor = initcolor.rgb;
		intermediateColor *= ExpoBias;

		intermediateColor = LinearToLogC(intermediateColor);			// convert to LogC
		apply_chart(intermediateColor, intermediateColor);				// apply LUT, sRGB baked in

		// incredibly cursed

#if D_ARRI != 2
		intermediateColor = pow(intermediateColor, GammaIn);			// bullshit the gamma from 2.4 to no gamma
#endif
#if D_ARRI == 0
		intermediateColor = pow(intermediateColor, GammaOut);			// bullshit the gamma from none to 2.2 (cant precompute SRGB yet + not all tonemaps need bullshitting)
#elif D_ARRI == 1
		intermediateColor = accurateSRGBToLinear(intermediateColor);
#endif

		intermediateColor.xyz += ScreenSpaceOrderedDither( i.vPositionSs.xy ); // i dont think it actually solves anything but it is funny to have it + i had to zoom in in photoshop to actually see it		
		return float4(intermediateColor, initcolor.w);
    }
}