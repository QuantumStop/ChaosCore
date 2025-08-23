using Sandbox.Utility;
using System;

namespace Core;

[Category( "Post Processing" )]
[Icon( "flash_on" )]
public sealed class ScreenFlash : PostProcess, Component.ExecuteInEditor
{
	public static ScreenFlash StaticRef { get; set; }
	[MakeDirty] private float Strength { get; set; } = 0f;
	[MakeDirty] private Color FlashColor { get; set; }
	[MakeDirty] private float FadeOutSpeed { get; set; }

	public ScreenFlash()
	{
		StaticRef = this;
	}

	[Button, Tint( EditorTint.Red )]
	void TestFlashRed()
	{
		Set( Color.Red, 1.25f );
	}

	[Button, Tint( EditorTint.Blue )]
	void TestFlashBlue()
	{
		Set( Color.Blue, 1.25f );
	}

	[Button]
	void KingdomeCome()
	{
		Set( Color.White, 1f );
	}

	// TODO: check for nightvision and force the color to be very visible
	public static void Set( Color color, float FadeOutSpeed )
	{
		StaticRef.Strength = 1f;
		StaticRef.FlashColor = color;
		StaticRef.FadeOutSpeed = FadeOutSpeed;
	}

	Sandbox.Rendering.CommandList Commands;
	protected override void OnEnabled()
	{
		Commands = new Sandbox.Rendering.CommandList( "ColorFlash" );
		Camera.AddCommandList( Commands, Sandbox.Rendering.Stage.BeforePostProcess, 5000 );
		OnDirty();
	}
	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		Strength = Math.Max( Strength - Time.Delta / FadeOutSpeed, 0f );
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

	public void Rebuild()
	{
		if ( Commands is null )
			return;

		Commands.Reset();

		Commands.Attributes.GrabFrameTexture( "ColorBuffer", false );
		Commands.Attributes.Set( "screen_flash_strength", Easing.QuadraticInOut( Strength ) );
		Commands.Attributes.Set( "screen_flash_color", FlashColor );

		Commands.Blit( Material.FromShader( "postprocess_colorflash" ) );
	}
}
