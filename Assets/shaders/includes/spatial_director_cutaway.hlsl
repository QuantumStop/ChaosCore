#ifndef SPATIAL_DIRECTOR_CUTAWAY_HLSL
#define SPATIAL_DIRECTOR_CUTAWAY_HLSL

float g_flCutawayEnabled    		 < Attribute( "CutawayEnabled" ); >;
float g_flCutawayStrength   		 < Attribute( "CutawayStrength" ); >;
float g_flCutawaySeeThrough 		 < Attribute( "CutawaySeeThrough" ); >;
float g_flCutawayViewBlend 	 		 < Attribute( "CutawayViewBlend" ); >;

float3 g_vCutawayStartWs 			 < Attribute( "CutawayStartWs" ); >;
float3 g_vCutawayEndWs   			 < Attribute( "CutawayEndWs" ); >;
float3 g_vCutawayHitWs   			 < Attribute( "CutawayHitWs" ); >;
float3 g_vCutawayRightWs 			 < Attribute( "CutawayRightWs" ); >;
float3 g_vCutawayUpWs    			 < Attribute( "CutawayUpWs" ); >;

float g_flCutawayShape                < Attribute( "CutawayShape" ); >;
float2 g_vCutawaySize                 < Attribute( "CutawaySize" ); >;
float g_flCutawayFeather              < Attribute( "CutawayFeather" ); >;
float g_flCutawayBehindHitPadding     < Attribute( "CutawayBehindHitPadding" ); >;
float g_flCutawayNearTarget           < Attribute( "CutawayNearTarget" ); >;
float g_flCutawayFloorThreshold       < Attribute( "CutawayFloorThreshold" ); >;
float g_flCutawayFloorHeightTolerance < Attribute( "CutawayFloorHeightTolerance" ); >;
float g_flCutawayPlayerFloorZ         < Attribute( "CutawayPlayerFloorZ" ); >;
float g_flCutawayPlayerTopZ           < Attribute( "CutawayPlayerTopZ" ); >;
float g_flCutawayExterior             < Attribute( "CutawayExterior" ); >;

float g_flInteriorDimEnabled    	  < Attribute( "InteriorDimEnabled" ); >;
float g_flInteriorVisibleRadius 	  < Attribute( "InteriorVisibleRadius" ); >;
float g_flInteriorFade          	  < Attribute( "InteriorFade" ); >;
float g_flInteriorDimAmount     	  < Attribute( "InteriorDimAmount" ); >;
float g_flInteriorExteriorDim   	  < Attribute( "InteriorExteriorDim" ); >;

float g_flInteriorPeekEnabled   	  < Attribute( "InteriorPeekEnabled" ); >;
float g_flInteriorPeekSizeScale    	  < Attribute( "InteriorPeekSizeScale" ); >;
float g_flInteriorPeekSeeThrough 	  < Attribute( "InteriorPeekSeeThrough" ); >;
float g_flInteriorPeekReveal     	  < Attribute( "InteriorPeekReveal" ); >;
float g_flInteriorPeekBlock      	  < Attribute( "InteriorPeekBlock" ); >;

float g_flInteriorPeekFogEnabled  	  < Attribute( "InteriorPeekFogEnabled" ); >;
float g_flInteriorInsideFogEnabled 	  < Attribute( "InteriorInsideFogEnabled" ); >;
float4 g_vInteriorPeekFogColor    	  < Attribute( "InteriorPeekFogColor" ); >;
float g_flInteriorPeekFogAmount   	  < Attribute( "InteriorPeekFogAmount" ); >;
float g_flInteriorPeekFogSoftness 	  < Attribute( "InteriorPeekFogSoftness" ); >;
float g_flInteriorPeekFogNoise    	  < Attribute( "InteriorPeekFogNoise" ); >;

float g_flCutawayIgnore < Attribute( "CutawayIgnore" ); >;
float g_flInteriorPeekWallThickness < Attribute( "InteriorPeekWallThickness" ); >;

Texture2D g_tCutawayCookie < Attribute( "CutawayCookie" ); >;

float g_flCutawayUseCookie      < Attribute( "CutawayUseCookie" ); >;
float g_flCutawayCookieStrength < Attribute( "CutawayCookieStrength" ); >;
float2 g_vCutawayCookieScale    < Attribute( "CutawayCookieScale" ); >;
float2 g_vCutawayCookieOffset   < Attribute( "CutawayCookieOffset" ); >;
float g_flCutawayCookieTiling   < Attribute( "CutawayCookieTiling" ); >;

float g_flSpatialDirectorDebug < Attribute( "SpatialDirectorDebug" ); >;

SamplerState g_sCutawayCookieSampler < Filter( LINEAR ); AddressU( CLAMP ); AddressV( CLAMP ); >;

float CutawayIgnoreFromPaint( float4 paint )
{
	return paint.r > 0.85f && paint.g < 0.15f && paint.b < 0.15f ? 1.0f : 0.0f;
}

float InteriorFromPaint( float4 paint )
{
	return paint.g > 0.85f && paint.r < 0.15f && paint.b < 0.15f ? 1.0f : 0.0f;
}

float InteriorShellFromPaint( float4 paint )
{
	return paint.b > 0.85f && paint.r < 0.15f && paint.g < 0.15f ? 1.0f : 0.0f;
}

float CutawayFloorMask( float3 positionWs, float3 normalWs )
{
	float up = saturate( dot( normalize( normalWs ), float3( 0.0f, 0.0f, 1.0f ) ) );

	float orientationMask = smoothstep(
		saturate( g_flCutawayFloorThreshold ),
		min( saturate( g_flCutawayFloorThreshold ) + 0.15f, 1.0f ),
		up
	);

	float tolerance = max( g_flCutawayFloorHeightTolerance, 0.001f );

	// Protect floors below the feet at any distance, including during jumps and on stairs.
	// Tolerance only extends protection above the feet, while higher roofs remain cuttable.
	float heightAboveFeet = positionWs.z - g_flCutawayPlayerFloorZ;

	float heightMask = 1.0f - smoothstep( tolerance, tolerance + 8.0f, heightAboveFeet );

	return orientationMask * heightMask;
}

float CutawayCeilingMask( float3 positionWs, float3 normalWs )
{
	float down = saturate( dot( normalize( normalWs ), float3( 0.0f, 0.0f, -1.0f ) ) );

	float abovePlayer = positionWs.z >= g_flCutawayPlayerTopZ ? 1.0f : 0.0f;

	return down * abovePlayer;
}

float3 CutawayLocalPosition( float3 positionWs, out float segmentLength )
{
	float3 segment = g_vCutawayEndWs - g_vCutawayStartWs;
	segmentLength = length( segment );

	float3 direction = segmentLength > 0.0001f ? segment / segmentLength : float3( 0.0f, 0.0f, 0.0f );

	float nearTarget = max( g_flCutawayNearTarget, 0.0f );
	float behindHitPadding = max( g_flCutawayBehindHitPadding, 0.0f );

	float hitDistance = dot( g_vCutawayHitWs - g_vCutawayStartWs, direction );

	// Padding must not push the clear core past the obstruction on the target ray.
	float obstructionDistance = clamp( hitDistance, 0.0f, segmentLength );
	float startDistance = min( max( nearTarget, obstructionDistance - behindHitPadding ), obstructionDistance );

	float endDistance = max( startDistance, segmentLength );

	float interiorEndDistance = max( startDistance, hitDistance + behindHitPadding + max( g_flCutawayFeather, 0.0f ) );

	endDistance = lerp( endDistance, min( endDistance, interiorEndDistance ), saturate( g_flCutawayViewBlend ) );

	float3 capsuleStart = g_vCutawayStartWs + direction * startDistance;
	float3 capsuleEnd = g_vCutawayStartWs + direction * endDistance;
	float3 capsuleSegment = capsuleEnd - capsuleStart;

	float capsuleLengthSq = dot( capsuleSegment, capsuleSegment );

	float t = capsuleLengthSq > 0.0001f ? saturate( dot( positionWs - capsuleStart, capsuleSegment ) / capsuleLengthSq ) : 0.0f;

	float3 closest = capsuleStart + capsuleSegment * t;
	float3 offset = positionWs - closest;
	float3 targetOffset = positionWs - g_vCutawayStartWs;

	float3 capsuleLocal = float3( dot( offset, g_vCutawayRightWs ), dot( offset, g_vCutawayUpWs ), dot( offset, direction ) );

	float3 targetLocal = float3( dot( targetOffset, g_vCutawayRightWs ), dot( targetOffset, g_vCutawayUpWs ), capsuleLocal.z );

	return lerp( capsuleLocal, targetLocal, saturate( g_flCutawayViewBlend ) );
}

float CutawaySignedDistance( float3 localPosition, float2 sizeScale )
{
	float2 halfSize = max( abs( g_vCutawaySize * sizeScale ), float2( 0.001f, 0.001f ) );

	float capRadius = max( min( halfSize.x, halfSize.y ), 0.001f );

	if ( g_flCutawayShape >= 0.5f )
	{
		float3 d = abs( localPosition ) - float3( halfSize, capRadius );

		return length( max( d, 0.0f ) ) + min( max( d.x, max( d.y, d.z ) ), 0.0f );
	}

	float3 normalized = float3( localPosition.x / halfSize.x, localPosition.y / halfSize.y, localPosition.z / capRadius );

	return (length( normalized ) - 1.0f ) * capRadius;
}

float2 CutawayCookieUv( float3 localPosition )
{
	float2 halfSize = max( abs( g_vCutawaySize ), float2( 0.001f, 0.001f ) );

	float2 uv = localPosition.xy / halfSize * 0.5f + 0.5f;

	uv = (uv - 0.5f ) / max( abs( g_vCutawayCookieScale ), float2( 0.001f, 0.001f ) ) + 0.5f;

	uv += g_vCutawayCookieOffset;

	return uv;
}

float CutawayUvInside01( float2 uv )
{
	return all( uv >= 0.0f ) && all( uv <= 1.0f ) ? 1.0f : 0.0f;
}

float CutawayCookieMask( float3 localPosition )
{
	if ( g_flCutawayUseCookie < 0.5f )
		return 1.0f;

	float cookie = g_tCutawayCookie.Sample( g_sCutawayCookieSampler, CutawayCookieUv( localPosition ) ).r;

	return lerp( 1.0f, cookie, saturate( g_flCutawayCookieStrength ) );
}

float CutawayCookieShapeMask( float3 localPosition, float feather, float noiseOffset )
{
	if ( g_flCutawayUseCookie < 0.5f )
		return 0.0f;

	float2 halfSize = max( abs( g_vCutawaySize ), float2( 0.001f, 0.001f ) );

	float2 uv = CutawayCookieUv( localPosition );

	if ( g_flCutawayCookieTiling >= 0.5f ) uv = frac( uv );
	else if ( CutawayUvInside01( uv ) < 0.5f )
		return 0.0f;

	float cookie = g_tCutawayCookie.Sample( g_sCutawayCookieSampler, uv ).r;

	float edgeWidth = saturate( feather / max( min( halfSize.x, halfSize.y ), 0.001f ) );

	float noiseThreshold = noiseOffset / max( min( halfSize.x, halfSize.y ), 0.001f );

	float threshold = saturate( 0.5f + noiseThreshold );

	return smoothstep( threshold - edgeWidth, threshold + edgeWidth, cookie ) * saturate( g_flCutawayCookieStrength );
}

float CutawayNoiseOffset( float3 localPosition )
{
	return SpatialDirector_NoiseOffset( localPosition.xy, g_vCutawaySize );
}

float CutawayNoise( float2 screenPosition )
{
	float scale = max( g_flCutawayDitherScale, 0.001f );
	float2 p = floor( screenPosition / scale );

	return frac( 52.9829189f * frac( dot( p, float2( 0.06711056f, 0.00583715f ) ) ) );
}

// Shared XY opening: depth validity is applied separately for shell and interior.
float CutawayOpeningMask( float3 localPosition, float2 sizeScale, bool shellDepth )
{
	float3 shapePosition = localPosition;
	if ( !shellDepth ) shapePosition.z = 0.0f;
	float noiseOffset = CutawayNoiseOffset( localPosition );
	float distance = CutawaySignedDistance( shapePosition, sizeScale ) + noiseOffset;
	float feather = max( max( g_flCutawayFeather, fwidth( distance ) ), 0.001f );
	float volumeMask = 1.0f - smoothstep( 0.0f, feather, distance );

	// Scale the cookie with the same opening as the shell, including outside peeking.
	float3 cookiePosition = localPosition;
	cookiePosition.xy /= max( abs( sizeScale ), float2( 0.001f, 0.001f ) );
	if ( g_flCutawayShape >= 1.5f )
		return volumeMask * CutawayCookieShapeMask( cookiePosition, feather, noiseOffset );
	return volumeMask * CutawayCookieMask( cookiePosition );
}

float2 InteriorOpeningScale()
{
	float peek = saturate( g_flInteriorPeekEnabled ) * (1.0f - saturate( g_flCutawayViewBlend ));
	return lerp( float2( 1.0f, 1.0f ), float2( g_flInteriorPeekSizeScale, g_flInteriorPeekSizeScale ), peek );
}

float InteriorDepthMask( float3 positionWs )
{
	float3 segment = g_vCutawayEndWs - g_vCutawayStartWs;
	float segmentLength = length( segment );
	if ( segmentLength <= 0.001f ) return 0.0f;
	float3 direction = segment / segmentLength;
	float depth = dot( positionWs - g_vCutawayHitWs, direction );
	// Admit the camera-side faces of thicker walls without changing the facade opening.
	depth -= max( g_flInteriorPeekWallThickness, 0.0f );
	float softness = max( max( g_flCutawayFeather * max( g_flInteriorPeekFogSoftness, 0.001f ), fwidth( depth ) ), 0.001f );
	return 1.0f - smoothstep( -softness, softness, depth );
}

// Optional atmosphere, independent of the camera opening. XY avoids circles on vertical walls.
float InteriorDistanceMask( float3 positionWs )
{
	float distance = length( positionWs.xy - g_vCutawayStartWs.xy );
	float radius = max( g_flInteriorVisibleRadius, 0.0f );
	float fade = max( max( g_flInteriorFade, fwidth( distance ) ), 0.001f );
	return smoothstep( radius, radius + fade, distance );
}

float InteriorPeekVolumeMask( float3 positionWs )
{
	if ( g_flCutawayEnabled < 0.5f || g_flInteriorPeekEnabled < 0.5f ) return 0.0f;
	float segmentLength;
	float3 localPosition = CutawayLocalPosition( positionWs, segmentLength );
	float opening = CutawayOpeningMask( localPosition, InteriorOpeningScale(), false );
	
	// Interior receiving surfaces can lie behind the target. Their peek and fog
	// use InteriorDepthMask rather than the shell's rounded depth extent.
	return saturate( opening * (1.0f - saturate( g_flCutawayViewBlend )) * g_flCutawayStrength );
}

float InteriorPeekMask( float3 positionWs )
{
	// Peek reveal and fog follow the same opening and depth gate.
	return InteriorPeekVolumeMask( positionWs ) * InteriorDepthMask( positionWs );
}

void ApplyInteriorPeekReveal( float3 positionWs, float2 screenPosition )
{
	// Show the whole receiving surface when inspecting masks, rather than its dither survivors.
	if ( g_flSpatialDirectorDebug > 3.5f && g_flSpatialDirectorDebug < 5.5f ) return;

	if ( g_flInteriorPeekBlock >= 0.5f ) return;

	float reveal = saturate( g_flInteriorPeekReveal );

	if ( reveal >= 0.999f ) return;

	float peekMask = InteriorPeekMask( positionWs );

	if ( peekMask <= 0.001f ) return;

	float hidden = peekMask * (1.0f - reveal );

	if ( WorldOcclusion_ShouldClip( hidden, screenPosition / max( g_flCutawayDitherScale, 0.001f ), g_flCutawayDitherAmount ) )
	{
		clip( -1.0f );
	}
}

void ApplyCutaway( float3 positionWs, float3 normalWs, float2 screenPosition, float4 vertexPaint, bool cutawaySurface, bool cutawayVertexMask )
{
	if (!cutawaySurface )
		return;

	if (g_flCutawayIgnore > 0.5f || g_flCutawayEnabled < 0.5f )
		return;

	if (cutawayVertexMask && CutawayIgnoreFromPaint( vertexPaint ) > 0.5f )
		return;

	// Floors/terrain still participate in dimming and fog,
	// but we don't destructively clip holes through upward facing surfaces.
	if (CutawayFloorMask( positionWs, normalWs ) > 0.95f )
		return;

	if (InteriorFromPaint( vertexPaint ) > 0.5f )
	{
		// Interior visibility has its own peek controls. Facade clipping must not
		// erase receiving walls simply because they fall within the shell opening.
		ApplyInteriorPeekReveal( positionWs, screenPosition );
		return;
	}

	float strength = saturate( g_flCutawayStrength );

	if (strength <= 0.0001f ) return;

	float nearTarget = max( g_flCutawayNearTarget, 0.0f );

	if (length( g_vCutawayEndWs - g_vCutawayStartWs ) <= nearTarget ) return;

	float interiorPeek = InteriorShellFromPaint( vertexPaint ) * saturate( g_flInteriorPeekEnabled ) * (1.0f - saturate( g_flCutawayViewBlend ));

	float2 cutawaySizeScale = lerp( float2( 1.0f, 1.0f ), float2( g_flInteriorPeekSizeScale, g_flInteriorPeekSizeScale ), interiorPeek );

	// Peek Cut may reduce shell transparency, but cannot override the main reveal control.
	float seeThrough = lerp( g_flCutawaySeeThrough, min( g_flCutawaySeeThrough, g_flInteriorPeekSeeThrough ), interiorPeek );

	float segmentLength;
	float3 localPosition = CutawayLocalPosition( positionWs, segmentLength );
	float opening = CutawayOpeningMask( localPosition, cutawaySizeScale, true );
	float cutAmount = saturate( opening * strength * saturate( seeThrough ) );

	if (WorldOcclusion_ShouldClip( cutAmount, screenPosition / max( g_flCutawayDitherScale, 0.001f ), g_flCutawayDitherAmount ))
	{
		clip( -1.0f );
	}
}

float InteriorDimMask( float3 positionWs, float4 vertexPaint )
{
	if ( g_flInteriorDimEnabled < 0.5f )
		return 0.0f;

	if ( InteriorFromPaint( vertexPaint ) < 0.5f )
		return 0.0f;

	// Outside interiors stay dim, distance shading applies when inside.
	float distanceMask = lerp( 1.0f, InteriorDistanceMask( positionWs ), saturate( g_flCutawayViewBlend ) );
	return distanceMask * saturate( g_flInteriorDimAmount );
}

float InteriorExteriorDimMask( float4 vertexPaint )
{
	if (g_flInteriorExteriorDim <= 0.001f ) return 0.0f;

	if (CutawayIgnoreFromPaint( vertexPaint ) > 0.5f )
		return 0.0f;

	if (InteriorFromPaint( vertexPaint ) > 0.5f )
		return 0.0f;

	return saturate( g_flInteriorExteriorDim * g_flCutawayViewBlend );
}

float InteriorPeekFogMask( float3 positionWs, float2 screenPosition, float4 vertexPaint )
{
	if ( g_flInteriorPeekFogEnabled < 0.5f || g_flInteriorPeekEnabled < 0.5f )
	{
		return 0.0f;
	}

	if ( InteriorFromPaint( vertexPaint ) < 0.5f )
		return 0.0f;

	float fogMask = InteriorPeekMask( positionWs );

	if ( g_flInteriorPeekFogNoise > 0.001f )
	{
		float noise = CutawayNoise( screenPosition );

		fogMask *= lerp( 1.0f, saturate( noise + 0.35f ), saturate( g_flInteriorPeekFogNoise ) );
	}

	return saturate( fogMask );
}

// Inside fog uses painted interior surfaces, independent of the outside peek corridor.
float InteriorInsideFogMask( float3 positionWs, float2 screenPosition, float4 vertexPaint )
{
	if ( g_flInteriorInsideFogEnabled < 0.5f || InteriorFromPaint( vertexPaint ) < 0.5f )
		return 0.0f;

	float insideBlend = saturate( g_flCutawayViewBlend );
	if ( insideBlend <= 0.001f )
		return 0.0f;

	float fogMask = InteriorDistanceMask( positionWs );

	if ( g_flInteriorPeekFogNoise > 0.001f )
		fogMask *= lerp( 1.0f, saturate( CutawayNoise( screenPosition ) + 0.35f ), saturate( g_flInteriorPeekFogNoise ) );

	return saturate( pow( saturate( fogMask ), 1.0f / max( g_flInteriorPeekFogSoftness, 0.001f ) ) ) * insideBlend;
}

#endif
