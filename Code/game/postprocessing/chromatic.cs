namespace chaoscore;

[Category( "Post Processing" )]
[Icon( "lens_blur" )]

public sealed class Chromatic : PostProcess, Component.ExecuteInEditor
{
	[Description( "Multiplier on the screen pixel size" )]
	[Property, Range( 0f, 2f ), Step( 0.1f ), MakeDirty] float OffsetAmount { get; set; } = 1f;
	[Description( "Multiplier on the radial offset strength thing" )]
	[Property, Range( 0f, 2f ), Step( 0.1f ), MakeDirty] float Offset2_Amount { get; set; } = 1f;

	Sandbox.Rendering.CommandList Commands;
	protected override void OnEnabled()
	{
		Commands = new Sandbox.Rendering.CommandList( "Chromatic" );
		Camera.AddCommandList( Commands, Sandbox.Rendering.Stage.BeforePostProcess, 3500 );
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


		Commands.Attributes.Set( "Offset", OffsetAmount );
		Commands.Attributes.Set( "Offset2", Offset2_Amount );

		Commands.Attributes.GrabFrameTexture( "ColorBuffer", false );
		Commands.Blit( Material.FromShader( "chromatic" ) );
	}
}
