namespace Core;

using Sandbox;
using System;

public sealed partial class SpatialDirector
{
	private void UpdateShaderCutaway()
	{
		if ( Scene is null || Target is null || Camera is null )
			return;

		var start = Target.WorldPosition + TargetOffset;
		var hasCookie = CutawayCookie is not null && CutawayCookie.IsValid;

		CutawayEndWorld = GetCutawayRayEnd( start );

		var seeThrough = OutsideToInsideSeeThrough + (InsideToOutsideSeeThrough - OutsideToInsideSeeThrough) * ViewBlend;
		var feather = OutsideToInsideFeather + (InsideToOutsideFeather - OutsideToInsideFeather) * ViewBlend;

		seeThrough = Math.Clamp( seeThrough, 0f, 1f );

		var cutawaySize = new Vector2(
			MathF.Max( CutawaySize.x, 0.001f ),
			MathF.Max( CutawaySize.y, 0.001f )
		);

		Scene.RenderAttributes.Set( "CutawayEnabled", CutawayStrength > 0.001f ? 1f : 0f );
		Scene.RenderAttributes.Set( "CutawayStrength", CutawayStrength );
		Scene.RenderAttributes.Set( "CutawaySeeThrough", seeThrough );
		Scene.RenderAttributes.Set( "CutawayViewBlend", ViewBlend );

		Scene.RenderAttributes.Set( "CutawayStartWs", start );
		Scene.RenderAttributes.Set( "CutawayEndWs", CutawayEndWorld );
		Scene.RenderAttributes.Set( "CutawayHitWs", CutawayHitWorld );
		Scene.RenderAttributes.Set( "CutawayRightWs", Camera.WorldRotation.Right.Normal );
		Scene.RenderAttributes.Set( "CutawayUpWs", Camera.WorldRotation.Up.Normal );

		Scene.RenderAttributes.Set( "CutawayShape", (float)Shape );
		Scene.RenderAttributes.Set( "CutawaySize", cutawaySize );
		Scene.RenderAttributes.Set( "CutawayFeather", feather );
		Scene.RenderAttributes.Set( "CutawayBehindHitPadding", MathF.Max( BehindHitPadding, 0f ) );
		Scene.RenderAttributes.Set( "CutawayNearTarget", MathF.Max( NearTargetPadding, 0f ) );
		Scene.RenderAttributes.Set( "CutawayFloorThreshold", Math.Clamp( CutawayFloorThreshold, 0f, 1f ) );
		Scene.RenderAttributes.Set( "CutawayFloorHeightTolerance", MathF.Max( CutawayFloorHeightTolerance, 0f ) );
		Scene.RenderAttributes.Set( "CutawayPlayerFloorZ", PlayerFloorZ );
		Scene.RenderAttributes.Set( "CutawayPlayerTopZ", PlayerTopZ );

		Scene.RenderAttributes.Set( "InteriorDimEnabled", DimDistantInterior ? 1f : 0f );
		Scene.RenderAttributes.Set( "InteriorVisibleRadius", MathF.Max( InteriorVisibleRadius, 0f ) );
		Scene.RenderAttributes.Set( "InteriorFade", MathF.Max( InteriorFade, 0f ) );
		Scene.RenderAttributes.Set( "InteriorDimAmount", Math.Clamp( InteriorDimAmount, 0f, 1f ) );
		Scene.RenderAttributes.Set( "InteriorExteriorDim", Math.Clamp( InteriorExteriorDim, 0f, 1f ) );

		Scene.RenderAttributes.Set( "InteriorPeekEnabled", LimitExteriorInteriorPeek ? 1f : 0f );
		Scene.RenderAttributes.Set( "InteriorPeekSizeScale", Math.Clamp( ExteriorInteriorPeekSizeScale, 0.05f, 1f ) );
		Scene.RenderAttributes.Set( "InteriorPeekSeeThrough", Math.Clamp( ExteriorInteriorPeekSeeThrough, 0f, 1f ) );
		Scene.RenderAttributes.Set( "InteriorPeekReveal", Math.Clamp( InteriorPeekReveal, 0f, 1f ) );
		Scene.RenderAttributes.Set( "InteriorPeekWallThickness", MathF.Max( InteriorPeekWallThickness, 0f ) );
		Scene.RenderAttributes.Set( "InteriorPeekBlock", BlockInteriorPeek ? 1f : 0f );

		Scene.RenderAttributes.Set( "InteriorInsideFogEnabled", FogInteriorInside ? 1f : 0f );
		Scene.RenderAttributes.Set( "InteriorPeekFogEnabled", FogInteriorPeek ? 1f : 0f );
		Scene.RenderAttributes.Set( "InteriorPeekFogColor", InteriorPeekFogColor );
		Scene.RenderAttributes.Set( "InteriorPeekFogAmount", Math.Clamp( InteriorPeekFogAmount, 0f, 1f ) );
		Scene.RenderAttributes.Set( "InteriorPeekFogSoftness", MathF.Max( InteriorPeekFogSoftness, 0.001f ) );
		Scene.RenderAttributes.Set( "InteriorPeekFogNoise", Math.Clamp( InteriorPeekFogNoise, 0f, 1f ) );

		Scene.RenderAttributes.Set( "CutawayUseCookie", hasCookie ? 1f : 0f );
		Scene.RenderAttributes.Set( "CutawayCookieStrength", CutawayCookieStrength );
		Scene.RenderAttributes.Set( "CutawayCookieScale", CutawayCookieScale );
		Scene.RenderAttributes.Set( "CutawayCookieOffset", CutawayCookieOffset + new Vector2( 0f, _cookieScroll ) );
		Scene.RenderAttributes.Set( "CutawayCookieTiling", CutawayCookieTiling ? 1f : 0f );
		Scene.RenderAttributes.Set( "SpatialDirectorDebug", (float)ShaderDebugMode );

		if ( hasCookie )
			Scene.RenderAttributes.Set( "CutawayCookie", CutawayCookie );

		ApplyPatternAttributes( Scene.RenderAttributes );
	}

	private void SetShaderCutawayDisabled()
	{
		if ( Scene is null )
			return;

		Scene.RenderAttributes.Set( "CutawayEnabled", 0f );
		Scene.RenderAttributes.Set( "CutawayStrength", 0f );
		Scene.RenderAttributes.Set( "InteriorInsideFogEnabled", 0f );
		Scene.RenderAttributes.Set( "InteriorDimEnabled", 0f );
		Scene.RenderAttributes.Set( "InteriorPeekFogEnabled", 0f );
		Scene.RenderAttributes.Set( "InteriorPeekEnabled", 0f );
		Scene.RenderAttributes.Set( "CutawayViewBlend", 0f );
		Scene.RenderAttributes.Set( "SpatialDirectorDebug", (float)ShaderDebugMode );
	}
}
