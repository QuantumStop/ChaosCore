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
	#include "oklab.hlsl"
    Texture2D LookupTexture < Attribute( "LookupTexture" ); >;	
	SamplerState LookupTexture_sampler < Filter( POINT ); AddressU( CLAMP ); AddressV( CLAMP );   >;

	float ExpoBias		< Attribute( "TonemapBias" ); >;
	float Gamma			< Attribute( "GammaResult" ); >;
	float SatBlend		< Attribute( "SatBlend" ); >;
      
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
		0.010591, // cut
		5.555556, // a
		0.052272, // b
		0.247190, // c
		0.385537, // d
		5.367655, // e
		0.092809  // f
	};
	
	float3 LinearToLogC(float3 x)
	{
		return (x > LogC.cut) ? LogC.c * log10(LogC.a * x + LogC.b) + LogC.d : LogC.e * x + LogC.f;
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

	float3 IGN(float2 vScreenPos)
	{
		float3 dither = frac(52.9829189f * frac(dot(vScreenPos, float2(0.06711056f, 0.00583715f))));
		dither -= float3( 0.5, 0.5, 0.5 );
		return ( dither.rgb / 255.0 ) * 0.375;
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

	DynamicCombo( D_GAMMA, 0..2, Sys( PC ) );
	DynamicCombo( D_SAT, 0..1, Sys( PC ) );

	float3 TestColor(float2 position)
	{
		return pow(sin(position.x * 4.0 + float3(0.0, 1.0, 2.0) * 3.1415 * 2.0 / 3.0) * 0.5 + 0.5, float3(2.0, 2.0, 2.0)) * (exp(abs(position.y) * 4.0) - 1.0);
	}

	float3 TestColor2(float2 position)
	{
		float h = floor(1.0 + 18.0 * position.y) / 18.0 * 3.141592 * 2;
		float L = floor(position.x * 24.0) / (24.0 ) - 0.4;
		
		
		float3 color = cos(h + float3(0.0,1.0,2.0) * 3.141592 * 2.0 / 3.0);
		float maxRGB = max(color.r, max(color.g, color.b));
		float minRGB = min(color.r, min(color.g, color.b));
		
		color = exp(10.0 * L) * (color - minRGB) / (maxRGB - minRGB);

		return color;
	}

	float MakeMask(float3 color)
	{
		return pow(LRGBtoOKLCH(color).x, 2.2f);
	}

	float OklabSat			< Attribute( "OklabSat" ); Default(1.0); >;
	
    float4 MainPs( PixelInput i ) : SV_Target0
    {
        float4 initcolor = g_tColorBuffer.Sample( Sampler, i.vTexCoord ).rgba;
		float3 intermediateColor = initcolor.rgb;
		
		intermediateColor *= g_flToneMapScalarLinear;
		intermediateColor *= ExpoBias;

		intermediateColor = LinearToLogC(intermediateColor);			// convert to LogC
#if D_SAT
		float3 BW = MakeMask(intermediateColor);
		BW = pow(BW, SatBlend);

		float3 saturated = LRGBtoOKLCH(intermediateColor);
		saturated.y *= OklabSat;
		saturated = OKLCHtoLRGB(saturated);
	//	intermediateColor = BW;
		intermediateColor = lerp(intermediateColor, saturated, BW);
#endif
		apply_chart(intermediateColor, intermediateColor);				// apply LUT, sRGB baked in

		// incredibly cursed

#if D_GAMMA > 0
		intermediateColor = pow(intermediateColor, Gamma);			// bullshit the gamma from 2.4 to no gamma
#if D_GAMMA == 2
		intermediateColor = accurateSRGBToLinear(intermediateColor);
#endif
#endif

		intermediateColor.xyz += IGN( i.vPositionSs.xy );
		return float4(intermediateColor, initcolor.w);
    }
}