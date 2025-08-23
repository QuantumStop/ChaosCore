namespace Core;

[Category( "Post Processing" )]
[Icon( "exposure" )]

public class Tonemapper : PostProcess, Component.ExecuteInEditor
{
	public enum TonemappingMode
	{
		[Description( "Default tonemapper, LogC" )]
		Default,
		[Description( "Rec709, LogC" )]
		Linear,
		[Description( "Any LUT with LogC" )]
		Custom
	}

	[MakeDirty, Property, Description( "Change this depending on the mini-game" )]
	public TonemappingMode Mode { get; set; } = TonemappingMode.Default;

	float GammaPre { get; set; } = 2.4f;
	float GammaPost { get; set; } = 2.2f;
	float Bias { get; set; } = 1.333333f;
	[MakeDirty, Property, ShowIf( nameof( Mode ), TonemappingMode.Default ), Range( -1, 2 ), Step( 0.1f )] float Vibrance { get; set; } = 1;

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
		Camera.AddCommandList( Commands, Sandbox.Rendering.Stage.BeforePostProcess, int.MaxValue );
		OnDirty();
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

		if ( Mode == TonemappingMode.Default )
		{
			Bias = 1.3333333f;
			GammaPre = 1 / 2.4f;
			Commands.Attributes.SetCombo( "D_ARRI", 1 );
			LookupTexture = Texture.Load( "materials/shaders/arri_logc4_rec709.vtex" );
		}
		else if ( Mode == TonemappingMode.Linear )
		{
			Bias = 1.0f;
			GammaPre = 1.0f;
			GammaPost = 1.0f;
			Commands.Attributes.SetCombo( "D_ARRI", 0 );
			LookupTexture = Texture.Load( "materials/shaders/rec601_linear_clip_knee.vtex" );
		}
		else if ( Mode == TonemappingMode.Custom )
		{
			Bias = ExposureBias;
			GammaPre = 1 / GammaIn;
			GammaPost = GammaOut;
			Commands.Attributes.SetCombo( "D_ARRI", 0 );
			LookupTexture = CustomLUT;
		}

		Commands.Attributes.Set( "TonemapBias", Bias );
		Commands.Attributes.Set( "Vibrance", Vibrance );
		Commands.Attributes.Set( "GammaIn", GammaPre );
		Commands.Attributes.Set( "GammaOut", GammaPost );
		Commands.Attributes.Set( "LookupTexture", LookupTexture );

		Commands.Attributes.GrabFrameTexture( "ColorBuffer", false );
		Commands.Blit( Material.FromShader( "tonemapping" ) );
	}
}
