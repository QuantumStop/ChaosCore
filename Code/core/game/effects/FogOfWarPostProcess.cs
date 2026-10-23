namespace Core;

using Sandbox;
using Sandbox.Rendering;
using System;

[Title( "Fog Of War Post Process" )]
[Category( "Post Processing" )]
[Icon( "lens_blur" )]
public sealed class FogOfWarPostProcess : BasePostProcess<FogOfWarPostProcess>
{
	private static Material _shader = Material.FromShader( "fog_of_war.shader" );

	private SpatialDirector FindDirector()
	{
		if ( Scene is null ) return null;

		foreach ( var director in Scene.GetAllComponents<SpatialDirector>() )
		{
			if ( director.IsValid() && director.Active )
				return director;
		}

		return null;
	}

	protected override void OnEnabled() => _fog = FindDirector();

	private SpatialDirector _fog;

	public override void Render()
	{
		if ( !_shader.IsValid() ) return;

		if ( !_fog.IsValid() || !_fog.Active || !_fog.IsSupportedView || !_fog.FogEnabled || !_fog.VisibilityTexture.IsValid() ) return;

		var mapSize = new Vector2( MathF.Max( MathF.Abs( _fog.MapSize.x ), 1f ), MathF.Max( MathF.Abs( _fog.MapSize.y ), 1f ) );

		var farStart = MathF.Max( _fog.FarStart, 0f );
		var farEnd = MathF.Max( _fog.FarEnd, farStart + 1f );

		Attributes.Set( "FogVisibility", _fog.VisibilityTexture );
		Attributes.Set( "FogMapMin", _fog.MapCenter - mapSize * 0.5f );
		Attributes.Set( "FogMapSize", mapSize );
		Attributes.Set( "FogVisionPosition", _fog.VisionWorldPosition );

		var arcAngle = SpatialProbe.GetSampleAngle( new SpatialProbeSettings { SamplingMode = _fog.VisionMode, ArcAngle = _fog.VisionArcAngle } );

		Attributes.Set( "FogArcEnabled", arcAngle < 360f ? 1f : 0f );
		Attributes.Set( "FogArcDirection", _fog.VisionArcDirection );
		Attributes.Set( "FogArcCosine", MathF.Cos( arcAngle * MathF.PI / 360f ) - 0.000001f );
		Attributes.Set( "FogOutsideArcDim", Math.Clamp( _fog.OutsideArcDim, 0f, 1f ) );
		Attributes.Set( "FogArcHalfAngle", arcAngle * MathF.PI / 360f );
		Attributes.Set( "FogArcFeather", Math.Clamp( _fog.ArcFeather, 0f, 90f ) * MathF.PI / 180f );
		Attributes.Set( "FogArcLinearDim", _fog.ArcDimMode == SpatialDirectorArcDimMode.LinearDistance ? 1f : 0f );
		Attributes.Set( "FogArcNearDim", Math.Clamp( _fog.ArcNearDim, 0f, 1f ) );

		var arcDimStart = MathF.Max( _fog.ArcDimStart, 0f );

		Attributes.Set( "FogArcDimStart", arcDimStart );
		Attributes.Set( "FogArcDimEnd", MathF.Max( _fog.ArcDimEnd, arcDimStart + 1f ) );

		Attributes.Set( "FogArcUseDetail", _fog.ArcUseFogDetail && _fog.ArcFogDetailTexture.IsValid() ? 1f : 0f );
		Attributes.Set( "FogArcDetailStrength", Math.Clamp( _fog.ArcFogDetailStrength, 0f, 1f ) );
		Attributes.Set( "FogArcDetailWorldSize", MathF.Max( _fog.ArcFogDetailWorldSize, 1f ) );
		Attributes.Set( "FogArcDetailVelocity", _fog.ArcFogDetailVelocity );
		Attributes.Set( "FogArcDetailContrast", MathF.Max( _fog.ArcFogDetailContrast, 0.001f ) );

		if ( _fog.ArcUseFogDetail && _fog.ArcFogDetailTexture.IsValid() ) Attributes.Set( "FogArcDetailTexture", _fog.ArcFogDetailTexture );

		Attributes.Set( "FogOfWarColor", _fog.FogColor );
		Attributes.Set( "FogHeightRange", _fog.HeightRange );

		_fog.ApplyPatternAttributes( Attributes );

		Attributes.Set( "FogFarStart", farStart );
		Attributes.Set( "FogFarEnd", farEnd );
		Attributes.Set( "FogFarStrength", Math.Clamp( _fog.FarStrength, 0f, 1f ) );
		Attributes.Set( "FogFarOverlapStrength", Math.Clamp( _fog.FarOverlapStrength, 0f, 1f ) );
		Attributes.Set( "FogFarOverlapPower", MathF.Max( _fog.FarOverlapPower, 0.001f ) );


		Attributes.Set( "FogPeekEnabled", _fog.FogInteriorPeek && _fog.CutawayStrength > 0.001f && _fog.Target.IsValid() && _fog.Camera.IsValid() ? 1f : 0f );
		Attributes.Set( "FogPeekStrength", Math.Clamp( _fog.CutawayStrength, 0f, 1f ) );
		Attributes.Set( "FogPeekViewBlend", Math.Clamp( _fog.ViewBlend, 0f, 1f ) );

		Attributes.Set( "FogPeekStartWs", _fog.Target.IsValid() ? _fog.Target.WorldPosition + _fog.TargetOffset : Vector3.Zero );
		Attributes.Set( "FogPeekEndWs", _fog.CutawayEndWorld );
		Attributes.Set( "FogPeekHitWs", _fog.CutawayHitWorld );
		Attributes.Set( "FogPeekRightWs", _fog.Camera.IsValid() ? _fog.Camera.WorldRotation.Right.Normal : Vector3.Right );
		Attributes.Set( "FogPeekUpWs", _fog.Camera.IsValid() ? _fog.Camera.WorldRotation.Up.Normal : Vector3.Up );

		Attributes.Set( "FogPeekShape", (float)_fog.Shape );
		Attributes.Set( "FogPeekSize", new Vector2( MathF.Max( _fog.CutawaySize.x, 0.001f ), MathF.Max( _fog.CutawaySize.y, 0.001f ) ) );
		Attributes.Set( "FogPeekFeather", MathF.Max( _fog.OutsideToInsideFeather, 0.001f ) );
		Attributes.Set( "FogPeekBehindHit", MathF.Max( _fog.BehindHitPadding, 0f ) );

		Attributes.Set( "FogPeekColor", _fog.InteriorPeekFogColor );
		Attributes.Set( "FogPeekSoftness", MathF.Max( _fog.InteriorPeekFogSoftness, 0.001f ) );
		Attributes.Set( "FogPeekNoise", Math.Clamp( _fog.InteriorPeekFogNoise, 0f, 1f ) );

		Attributes.Set( "FogPeekAmount", Math.Clamp( _fog.InteriorPeekFogAmount, 0f, 1f ) );
		Attributes.Set( "FogInteriorRadius", MathF.Max( _fog.InteriorVisibleRadius, 0f ) );
		Attributes.Set( "FogInteriorFade", MathF.Max( _fog.InteriorFade, 0.001f ) );

		var hasDetail = _fog.FogDetailTexture.IsValid();

		Attributes.Set( "FogDetailEnabled", hasDetail ? 1f : 0f );
		Attributes.Set( "FogDetailStrength", Math.Clamp( _fog.FogDetailStrength, 0f, 1f ) );
		Attributes.Set( "FogDetailWorldSize", MathF.Max( _fog.FogDetailWorldSize, 1f ) );
		Attributes.Set( "FogDetailVelocity", _fog.FogDetailVelocity );
		Attributes.Set( "FogDetailContrast", MathF.Max( _fog.FogDetailContrast, 0.001f ) );

		if ( hasDetail ) Attributes.Set( "FogDetailTexture", _fog.FogDetailTexture );

		Attributes.Set( "FogCloudEnabled", _fog.FogClouds && _fog.FogCloudTexture.IsValid() ? 1f : 0f );
		Attributes.Set( "FogCloudColor", _fog.FogCloudColor );
		Attributes.Set( "FogCloudStrength", Math.Clamp( _fog.FogCloudStrength, 0f, 1f ) );
		Attributes.Set( "FogCloudWorldSize", MathF.Max( _fog.FogCloudWorldSize, 1f ) );
		Attributes.Set( "FogCloudVelocity", _fog.FogCloudVelocity );
		Attributes.Set( "FogCloudHeight", _fog.FogCloudHeight );
		Attributes.Set( "FogCloudContrast", MathF.Max( _fog.FogCloudContrast, 0.001f ) );

		if ( _fog.FogClouds && _fog.FogCloudTexture.IsValid() ) Attributes.Set( "FogCloudTexture", _fog.FogCloudTexture );

		Attributes.Set( "FogCameraPosition", _fog.Camera.IsValid() ? _fog.Camera.WorldPosition : Vector3.Zero );
		Attributes.Set( "FogCameraForward", _fog.Camera.IsValid() ? _fog.Camera.WorldRotation.Forward.Normal : Vector3.Forward );
		Attributes.Set( "FogViewBlend", Math.Clamp( _fog.ViewBlend, 0f, 1f ) );

		Attributes.Set( "SpatialDirectorDebug", (float)SpatialDirector.ShaderDebugMode );


		Blit( BlitMode.WithBackbuffer( _shader, Stage.AfterPostProcess, 4500, false ), "FogOfWar" );
	}
}
