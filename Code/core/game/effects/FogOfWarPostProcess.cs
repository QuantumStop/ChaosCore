namespace Core;

using Sandbox;
using Sandbox.Rendering;
using System;

[Title( "Fog Of War Post Process" )]
[Category( "Post Processing" )]
[Icon( "lens_blur" )]
public sealed class FogOfWarPostProcess : BasePostProcess<FogOfWarPostProcess>
{
	private Material _shader;

	private SpatialDirector FindDirector()
	{
		if ( Scene is null )
			return null;

		foreach ( var director in Scene.GetAllComponents<SpatialDirector>() )
		{
			if ( director.IsValid() && director.Active )
				return director;
		}

		return null;
	}

	public override void Render()
	{
		var fog = FindDirector();

		if ( !fog.IsValid() || !fog.Active || !fog.IsSupportedView || !fog.FogEnabled || fog.VisibilityTexture is null )
			return;

		_shader ??= Material.FromShader( "fog_of_war.shader" );

		if ( !_shader.IsValid() )
			return;

		var mapSize = new Vector2( MathF.Max( MathF.Abs( fog.MapSize.x ), 1f ), 
			MathF.Max( MathF.Abs( fog.MapSize.y ), 1f ) );

		var mapMin = fog.MapCenter - mapSize * 0.5f;
		var peekStart = fog.Target.IsValid() ? fog.Target.WorldPosition + fog.TargetOffset : Vector3.Zero;
		var peekRight = fog.Camera.IsValid() ? fog.Camera.WorldRotation.Right.Normal : Vector3.Right;
		var peekUp = fog.Camera.IsValid() ? fog.Camera.WorldRotation.Up.Normal : Vector3.Up;

		var peekSize = new Vector2( MathF.Max( fog.CutawaySize.x, 0.001f ), 
			MathF.Max( fog.CutawaySize.y, 0.001f ) );

		var farStart = MathF.Max( fog.FarStart, 0f );
		var farEnd = MathF.Max( fog.FarEnd, farStart + 1f );

		Attributes.Set( "FogVisibility", fog.VisibilityTexture );
		Attributes.Set( "FogMapMin", mapMin );
		Attributes.Set( "FogMapSize", mapSize );
		Attributes.Set( "FogVisionPosition", fog.VisionWorldPosition );
		
		var arcAngle = SpatialProbe.GetSampleAngle( new SpatialProbeSettings { SamplingMode = fog.VisionMode, ArcAngle = fog.VisionArcAngle } );
		
		Attributes.Set( "FogArcEnabled", arcAngle < 360f ? 1f : 0f );
		Attributes.Set( "FogArcDirection", fog.VisionArcDirection );
		Attributes.Set( "FogArcCosine", MathF.Cos( arcAngle * MathF.PI / 360f ) - 0.000001f );
		Attributes.Set( "FogOutsideArcDim", Math.Clamp( fog.OutsideArcDim, 0f, 1f ) );
		Attributes.Set( "FogArcHalfAngle", arcAngle * MathF.PI / 360f );
		Attributes.Set( "FogArcFeather", Math.Clamp( fog.ArcFeather, 0f, 90f ) * MathF.PI / 180f );
		Attributes.Set( "FogArcLinearDim", fog.ArcDimMode == SpatialDirectorArcDimMode.LinearDistance ? 1f : 0f );
		Attributes.Set( "FogArcNearDim", Math.Clamp( fog.ArcNearDim, 0f, 1f ) );
		
		var arcDimStart = MathF.Max( fog.ArcDimStart, 0f );
		
		Attributes.Set( "FogArcDimStart", arcDimStart );
		Attributes.Set( "FogArcDimEnd", MathF.Max( fog.ArcDimEnd, arcDimStart + 1f ) );
		
		var hasArcDetail = fog.ArcUseFogDetail && fog.ArcFogDetailTexture.IsValid();
		
		Attributes.Set( "FogArcUseDetail", hasArcDetail ? 1f : 0f );
		Attributes.Set( "FogArcDetailStrength", Math.Clamp( fog.ArcFogDetailStrength, 0f, 1f ) );
		Attributes.Set( "FogArcDetailWorldSize", MathF.Max( fog.ArcFogDetailWorldSize, 1f ) );
		Attributes.Set( "FogArcDetailVelocity", fog.ArcFogDetailVelocity );
		Attributes.Set( "FogArcDetailContrast", MathF.Max( fog.ArcFogDetailContrast, 0.001f ) );
		
		if ( hasArcDetail )
			Attributes.Set( "FogArcDetailTexture", fog.ArcFogDetailTexture );

		Attributes.Set( "FogOfWarColor", fog.FogColor );
		Attributes.Set( "FogHeightRange", fog.HeightRange );

		fog.ApplyPatternAttributes( Attributes );

		Attributes.Set( "FogFarStart", farStart );
		Attributes.Set( "FogFarEnd", farEnd );
		Attributes.Set( "FogFarStrength", Math.Clamp( fog.FarStrength, 0f, 1f ) );
		Attributes.Set( "FogFarOverlapStrength", Math.Clamp( fog.FarOverlapStrength, 0f, 1f ) );
		Attributes.Set( "FogFarOverlapPower", MathF.Max( fog.FarOverlapPower, 0.001f ) );

		var peekEnabled = fog.FogInteriorPeek && fog.CutawayStrength > 0.001f 
			&& fog.Target.IsValid() && fog.Camera.IsValid();

		Attributes.Set( "FogPeekEnabled", peekEnabled ? 1f : 0f );
		Attributes.Set( "FogPeekStrength", Math.Clamp( fog.CutawayStrength, 0f, 1f ) );
		Attributes.Set( "FogPeekViewBlend", Math.Clamp( fog.ViewBlend, 0f, 1f ) );

		Attributes.Set( "FogPeekStartWs", peekStart );
		Attributes.Set( "FogPeekEndWs", fog.CutawayEndWorld );
		Attributes.Set( "FogPeekHitWs", fog.CutawayHitWorld );
		Attributes.Set( "FogPeekRightWs", peekRight );
		Attributes.Set( "FogPeekUpWs", peekUp );

		Attributes.Set( "FogPeekShape", (float)fog.Shape );
		Attributes.Set( "FogPeekSize", peekSize );
		Attributes.Set( "FogPeekFeather", MathF.Max( fog.OutsideToInsideFeather, 0.001f ) );
		Attributes.Set( "FogPeekBehindHit", MathF.Max( fog.BehindHitPadding, 0f ) );

		Attributes.Set( "FogPeekColor", fog.InteriorPeekFogColor );
		Attributes.Set( "FogPeekSoftness", MathF.Max( fog.InteriorPeekFogSoftness, 0.001f ) );
		Attributes.Set( "FogPeekNoise", Math.Clamp( fog.InteriorPeekFogNoise, 0f, 1f ) );

		Attributes.Set( "FogPeekAmount", Math.Clamp( fog.InteriorPeekFogAmount, 0f, 1f ) );
		Attributes.Set( "FogInteriorRadius", MathF.Max( fog.InteriorVisibleRadius, 0f ) );
		Attributes.Set( "FogInteriorFade", MathF.Max( fog.InteriorFade, 0.001f ) );

		var hasDetail = fog.FogDetailTexture.IsValid();
		var hasClouds = fog.FogClouds && fog.FogCloudTexture.IsValid();

		Attributes.Set( "FogDetailEnabled", hasDetail ? 1f : 0f );
		Attributes.Set( "FogDetailStrength", Math.Clamp( fog.FogDetailStrength, 0f, 1f ) );
		Attributes.Set( "FogDetailWorldSize", MathF.Max( fog.FogDetailWorldSize, 1f ) );
		Attributes.Set( "FogDetailVelocity", fog.FogDetailVelocity );
		Attributes.Set( "FogDetailContrast", MathF.Max( fog.FogDetailContrast, 0.001f ) );

		if ( hasDetail )
			Attributes.Set( "FogDetailTexture", fog.FogDetailTexture );

		Attributes.Set( "FogCloudEnabled", hasClouds ? 1f : 0f );
		Attributes.Set( "FogCloudColor", fog.FogCloudColor );
		Attributes.Set( "FogCloudStrength", Math.Clamp( fog.FogCloudStrength, 0f, 1f ) );
		Attributes.Set( "FogCloudWorldSize", MathF.Max( fog.FogCloudWorldSize, 1f ) );
		Attributes.Set( "FogCloudVelocity", fog.FogCloudVelocity );
		Attributes.Set( "FogCloudHeight", fog.FogCloudHeight );
		Attributes.Set( "FogCloudContrast", MathF.Max( fog.FogCloudContrast, 0.001f ) );

		if ( hasClouds )
			Attributes.Set( "FogCloudTexture", fog.FogCloudTexture );

		Attributes.Set( "FogCameraPosition", fog.Camera.IsValid() ? fog.Camera.WorldPosition : Vector3.Zero );
		Attributes.Set( "FogCameraForward", fog.Camera.IsValid() ? fog.Camera.WorldRotation.Forward.Normal : Vector3.Forward );
		Attributes.Set( "FogViewBlend", Math.Clamp( fog.ViewBlend, 0f, 1f ) );

		Attributes.Set( "SpatialDirectorDebug", (float)SpatialDirector.ShaderDebugMode );

		var blit = BlitMode.WithBackbuffer( _shader, Stage.AfterPostProcess, 4500, false );

		Blit( blit, "FogOfWar" );
	}
}
