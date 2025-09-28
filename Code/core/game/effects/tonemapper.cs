namespace Core;

[Category( "Post Processing" )]
[Icon( "exposure" )]

public class Tonemapper : PostProcess, Component.ExecuteInEditor
{
	public enum TonemappingMode
	{
		[Description( "AgX, LogC3" )]
		Saturated,
		[Description( value: "Arri Wide Gamut 4/LogC4 LUT, LogC3" )]
		Neutral,
		[Description( "Regular Rec709 with nothing, LogC3" )]
		Linear,
		[Description( "Any LUT with LogC3" )]
		Custom
	}

	[MakeDirty, Property, Description( "Change this depending on the mini-game" )]
	public TonemappingMode Mode { get; set; } = TonemappingMode.Neutral;

	float GammaPre { get; set; } = 2.4f;
	float GammaPost { get; set; } = 2.2f;
	float Bias { get; set; } = 1.333333f;

	[MakeDirty, Property, ShowIf( nameof( Mode ), TonemappingMode.Custom ), Range( 1, 3 ), Step( 0.1f )] float GammaIn { get; set; } = 2.4f;
	[MakeDirty, Property, ShowIf( nameof( Mode ), TonemappingMode.Custom ), Range( 1, 3 ), Step( 0.1f )] float GammaOut { get; set; } = 2.2f;

	Texture LookupTexture { get; set; } = Texture.White;

	[MakeDirty, Property, ShowIf( nameof( Mode ), TonemappingMode.Custom )]
	Texture CustomLUT { get; set; } = Texture.White;
	[MakeDirty, Property, ShowIf( nameof( Mode ), TonemappingMode.Custom ), Range( 0.1f, 2 )]
	float ExposureBias { get; set; } = 1.0f;


	Sandbox.Rendering.CommandList Commands;
	protected override void OnEnabled()
	{
		Commands = new Sandbox.Rendering.CommandList( "Tonemapper" );
		Camera.AddCommandList( Commands, Sandbox.Rendering.Stage.Tonemapping, int.MaxValue ); // since there is now a separate stage for tonemapping it doesnt matter which order the comlist uses
		Rebuild(); // doesnt matter if its calling OnDirty or straight Rebuild, as
	}

	protected override void OnDisabled()
	{
		Camera.RemoveCommandList( Commands );
		Commands = null;
	}

	protected override void OnDirty()
	{
		Rebuild();
	}

	void Rebuild()
	{
		if ( Commands is null )
			return;

		Commands.Reset();

		switch ( Mode )
		{
			case TonemappingMode.Saturated:
				Bias = 1.5f;
				Commands.Attributes.SetCombo( "D_ARRI", 2 );
				LookupTexture = Texture.Load( "materials/shaders/agx_logc3_rec709.vtex" );
				break;
			case TonemappingMode.Neutral:
				Bias = 1.3333333f;
				GammaPre = 1 / 2.4f;
				Commands.Attributes.SetCombo( "D_ARRI", 1 );
				LookupTexture = Texture.Load( "materials/shaders/arri_logc4_rec709.vtex" );
				break;
			case TonemappingMode.Linear:
				Bias = 1.0f;
				GammaPre = 1.0f;
				GammaPost = 1.0f;
				Commands.Attributes.SetCombo( "D_ARRI", 0 );
				LookupTexture = Texture.Load( "materials/shaders/rec601_linear_clip_knee.vtex" );
				break;
			case TonemappingMode.Custom:
				Bias = ExposureBias;
				GammaPre = 1 / GammaIn;
				GammaPost = GammaOut;
				Commands.Attributes.SetCombo( "D_ARRI", 0 );
				LookupTexture = CustomLUT;
				break;

		}

		Commands.Attributes.Set( "TonemapBias", Bias );
		Commands.Attributes.Set( "GammaIn", GammaPre );
		Commands.Attributes.Set( "GammaOut", GammaPost );
		Commands.Attributes.Set( "LookupTexture", LookupTexture );

		Commands.Attributes.GrabFrameTexture( "ColorBuffer", false );
		Commands.Blit( Material.FromShader( "tonemapping" ) );
	}
}
