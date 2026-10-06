HEADER
{
	Description = "Lit world material with occlusion cutout";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	Forward();
	Depth( S_MODE_DEPTH );
}

COMMON
{
	#include "common/shared.hlsl"
}

struct VertexInput
{
	float4 vColorPaintValues : TEXCOORD5 < Semantic( VertexPaintTintColor ); >;

	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	float4 vPaintValues : TEXCOORD15;

	#include "common/pixelinput.hlsl"
};

VS
{
	#include "common/vertex.hlsl"

	PixelInput MainVs( VertexInput i )
	{
		PixelInput o = ProcessVertex( i );

		o.vPaintValues = i.vColorPaintValues;

		return FinalizeVertex( o );
	}
}

PS
{
	#include "common/pixel.hlsl"
	#include "includes/world_occlusion_rendering.hlsl"
	#include "includes/spatial_director_patterns.hlsl"
	#include "includes/spatial_director_cutaway.hlsl"
	#include "common/utils/Material.CommonInputs.hlsl"

	bool g_bCutawaySurface < Default( 1 ); UiGroup( "Cutaway" ); >;
	bool g_bCutawayVertexMask < Default( 0 ); UiGroup( "Cutaway" ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		Material m = Material::Init( i );

		float4 color = Tex2DS( g_tColor, TextureFiltering, i.vTextureCoords.xy );
		float4 normal = Tex2DS( g_tNormal, TextureFiltering, i.vTextureCoords.xy );
		float4 rma = Tex2DS( g_tRma, TextureFiltering, i.vTextureCoords.xy );

		m.Albedo = color.rgb * g_flTintColor;
		m.Opacity = color.a;

		m.Normal = TransformNormal( DecodeNormal( normal.rgb ), i.vNormalWs, i.vTangentUWs, i.vTangentVWs );

		m.Roughness = rma.r;
		m.Metalness = rma.g;
		m.AmbientOcclusion = rma.b;
		m.Emission = 0;
		m.Transmission = 0;
		m.TintMask = 1;

		m.WorldTangentU = i.vTangentUWs;
		m.WorldTangentV = i.vTangentVWs;
		m.TextureCoords = i.vTextureCoords.xy;

		ApplyCutaway( m.WorldPosition, i.vNormalWs, i.vPositionSs.xy, i.vPaintValues, g_bCutawaySurface, g_bCutawayVertexMask );

		float4 shaded = ShadingModelStandard::Shade( m );
		float exteriorDim = InteriorExteriorDimMask( i.vPaintValues );
		float interiorDim = InteriorDimMask( m.WorldPosition, i.vPaintValues );
		float interiorPeekFog = InteriorPeekFogMask( m.WorldPosition, i.vPositionSs.xy, i.vPaintValues );
		float insideFog = InteriorInsideFogMask( m.WorldPosition, i.vPositionSs.xy, i.vPaintValues );

		// Blend outside/inside fog masks once. Peek fog must never restore undimmed lighting.
		float fog = saturate( interiorPeekFog + insideFog ) * saturate( g_flInteriorPeekFogAmount );
		float dim = saturate( max( interiorDim, exteriorDim ) );
		shaded.rgb = shaded.rgb * (1.0f - dim) * (1.0f - fog) + g_vInteriorPeekFogColor.rgb * fog;

		// Our mask work retain normal shell clipping so interior surfaces remain inspectable.
		if ( g_flSpatialDirectorDebug > 3.5f && g_flSpatialDirectorDebug < 5.5f )
		{
			float mask = 1.0f - (1.0f - dim) * (1.0f - fog);
			if ( g_flSpatialDirectorDebug > 4.5f ) mask = InteriorDepthMask( m.WorldPosition );
			return float4( mask, mask, mask, 1.0f );
		}
		
		if ( g_flSpatialDirectorDebug > 0.5f && g_flSpatialDirectorDebug < 1.5f )
        {
            float ignore = CutawayIgnoreFromPaint(i.vPaintValues);
            float interior = InteriorFromPaint(i.vPaintValues);
            float shell = InteriorShellFromPaint(i.vPaintValues);

            float floor = CutawayFloorMask( m.WorldPosition, i.vNormalWs );

            float ceiling = CutawayCeilingMask( m.WorldPosition, i.vNormalWs );

            if (ignore > 0.5f)
                return float4(1.0f, 0.0f, 0.0f, 1.0f);

            if (floor > 0.5f)
                return float4(1.0f, 1.0f, 0.0f, 1.0f);

            if (ceiling > 0.5f)
                return float4(1.0f, 0.0f, 1.0f, 1.0f);

            if (interior > 0.5f)
                return float4(0.0f, 1.0f, 0.0f, 1.0f);

            if (shell > 0.5f)
                return float4(0.0f, 0.3f, 1.0f, 1.0f);

            return float4(0.25f, 0.25f, 0.25f, 1.0f);
		}

		if ( g_flSpatialDirectorDebug > 1.5f && g_flSpatialDirectorDebug < 2.5f )
		{
			float segmentLength;
			float3 localPosition = CutawayLocalPosition( m.WorldPosition, segmentLength );

			float2 sizeScale = InteriorShellFromPaint( i.vPaintValues ) > 0.5f ? InteriorOpeningScale() : float2( 1.0f, 1.0f );
			float cutMask = CutawayOpeningMask( localPosition, sizeScale, true );

			float peekMask = InteriorPeekMask( m.WorldPosition );

			return float4( cutMask, peekMask, max( interiorDim, exteriorDim ), 1.0f );
		}

		return shaded;
	}
}
