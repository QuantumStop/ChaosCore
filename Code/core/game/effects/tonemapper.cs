using System;

namespace Core;

[Category( "Post Processing" )]
[Icon( "exposure" )]

public class Tonemapper : BasePostProcess<Tonemapper>, Component.ExecuteInEditor
{
	public enum TonemappingMode
	{
		[Description( "AgX, LogC3" )]
		Saturated,
		[Description( "Saturated LogC4, LogC3" )]
		NeutralAlt,
		[Description( value: "LogC4 LUT, LogC3" )]
		Neutral,
		[Description( "Regular Rec709 with nothing, LogC3" )]
		Linear,
		[Description( "Any LUT with LogC3" )]
		Custom
	}

	[Property, Description( "Change this depending on the mini-game" )]
	public TonemappingMode Mode { get; set; } = TonemappingMode.Neutral;

	private float _gammaPre { get; set; } = 2.4f;
	private float _gammaPost { get; set; } = 2.2f;
	private float _bias { get; set; } = 1.333333f;
	private float _oklab { get; set; } = 1.25f;
	private float _satBlend { get; set; } = 0.6f;

	[Property, ShowIf( nameof( Mode ), TonemappingMode.Custom ), Range( 1, 3 ), Step( 0.1f )] float GammaIn { get; set; } = 2.4f;
	[Property, ShowIf( nameof( Mode ), TonemappingMode.Custom ), Range( 1, 3 ), Step( 0.1f )] float GammaOut { get; set; } = 2.2f;

	private Texture _lookupTexture { get; set; } = Texture.White;

	[Property, ShowIf( nameof( Mode ), TonemappingMode.Custom )]
	private Texture _customLUT { get; set; } = Texture.White;
	[Property, ShowIf( nameof( Mode ), TonemappingMode.Custom ), Range( 0.1f, 2 )]
	private float _exposureBias { get; set; } = 1.0f;
	/// <summary>
	/// How much to saturate using the oklab method (instead of HSV)
	/// </summary>
	[Property, ShowIf( nameof( Mode ), TonemappingMode.Custom ), Range( 0.0f, 2 )]
	private float _oklabSaturation { get; set; } = 2f;
	/// <summary>
	/// Instead of just applying saturation straight up, we blend based on OkLab brightness, which allows to saturate only certain parts of the image, instead of all of it, this adjusts contrast of the mask, with 0 meaning "just use the saturated image"
	/// </summary>
	[Property, ShowIf( nameof( Mode ), TonemappingMode.Custom ), Range( 0f, 2 )]
	private float _oklabLumaBlend { get; set; } = 0.8f;

	private float _gammaResult => CalculateGamma( _gammaPre, _gammaPost );

	public override void Render()
	{
		switch ( Mode )
		{
			case TonemappingMode.Linear:
				_bias = 1;
				Attributes.SetCombo( "D_GAMMA", 0 );
				Attributes.SetCombo( "D_SAT", 0 );
				_lookupTexture = Texture.Load( "materials/shaders/linear_srgb_rec709.vtex" );
				_oklab = 1;
				_satBlend = 0;
				break;
			case TonemappingMode.Saturated:
				_bias = 1.8f;
				Attributes.SetCombo( "D_GAMMA", 0 );
				Attributes.SetCombo( "D_SAT", 0 );
				_lookupTexture = Texture.Load( "materials/shaders/agx_logc3_rec709.vtex" );
				_oklab = 1;
				_satBlend = 0;
				break;
			case TonemappingMode.NeutralAlt:
				_bias = 1f;
				_oklab = 1.0f;
				_satBlend = 0;
				Attributes.SetCombo( "D_SAT", 0 );
				Attributes.SetCombo( "D_GAMMA", 0 ); // adjust gamma AND true SRGB
				_lookupTexture = Texture.Load( "materials/shaders/alt_logc4_rec709.vtex" );
				break;
			case TonemappingMode.Neutral:
				_bias = 1.3333333f;
				_oklab = 1.0f;
				_satBlend = 0;
				Attributes.SetCombo( "D_SAT", 0 );
				Attributes.SetCombo( "D_GAMMA", 0 ); // adjust gamma AND true SRGB
				_lookupTexture = Texture.Load( "materials/shaders/arri_logc4_rec709.vtex" );
				break;
			case TonemappingMode.Custom:
				_bias = _exposureBias;
				_gammaPre = GammaIn;
				_gammaPost = GammaOut;
				_oklab = _oklabSaturation;
				_satBlend = _oklabLumaBlend;
				Attributes.SetCombo( "D_SAT", 1 );  // use the oklch saturation
				Attributes.SetCombo( "D_GAMMA", 1 ); // in/out gamma
				_lookupTexture = _customLUT;
				break;
		}

		Attributes.Set( "TonemapBias", GetWeighted( x => x._bias ) );
		Attributes.Set( "GammaResult", GetWeighted( x => x._gammaResult ) );
		Attributes.Set( "OklabSat", GetWeighted( x => x._oklab ) );
		Attributes.Set( "SatBlend", GetWeighted( x => x._satBlend ) );
		Attributes.Set( "LookupTexture", _lookupTexture );

		var blit = BlitMode.WithBackbuffer( _shader, Sandbox.Rendering.Stage.Tonemapping, int.MaxValue, false );
		Blit( blit, "Ignis Tonemapper" );
	}

	private static Material _shader = Material.FromShader( "tonemapping.shader" );

	/// <summary>
	/// Calculate resulting gamma bias, instead of using two separate gammas and calculating each
	/// </summary>
	/// <param name="In">Original gamma of the LUT (if its something stupid like 2.4 instead of sRGB)</param>
	/// <param name="Out">The gamma we want to get</param>
	/// <returns></returns>
	private static float CalculateGamma( float In, float Out ) => Out / In;
}
