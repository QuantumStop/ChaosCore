namespace chaoscore;

[Category( "Post Processing" )]
[Icon( "invert_colors" )]

public sealed class Colorblind : PostProcess, Component.ExecuteInEditor
{

	public enum BlindMode
	{
		Off,
		/// <summary>
		/// Reduced sensitivity to red
		/// </summary>
		Protanopia,
		/// <summary>
		/// Reduced sensitivity to green
		/// </summary>
		Deuteranopia,
		/// <summary>
		/// Reduced sensitivity to blue
		/// </summary>
		Tritanopia
	}

	[Property]
	[MakeDirty]
	public BlindMode Mode { get; set; } = BlindMode.Off;

	Sandbox.Rendering.CommandList Commands;
	protected override void OnEnabled()
	{
		Commands = new Sandbox.Rendering.CommandList( "Colorblind" );
		Camera.AddCommandList( Commands, Sandbox.Rendering.Stage.AfterPostProcess, 4000 );
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

		Commands.Attributes.SetCombo( "D_MODE", Mode );
		Commands.Attributes.GrabFrameTexture( "ColorBuffer", false );
		Commands.Blit( Material.FromShader( "colorblind" ) );
	}
}
