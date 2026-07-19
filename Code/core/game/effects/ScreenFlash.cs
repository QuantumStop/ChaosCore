using Sandbox.Utility;
using System;

namespace Core;

[Category( "Post Processing" )]
[Icon( "flash_on" )]
public sealed class ScreenFlash : BasePostProcess<ScreenFlash>, Component.ExecuteInEditor
{
	public static ScreenFlash Instance { get; set; }
	public ScreenFlash() => Instance = this;

	private float Strength { get; set; } = 0f;
	private Color FlashColor { get; set; }
	private float FadeOutSpeed { get; set; }

	[ConVar( "r_screenflash", ConVarFlags.Saved, Help = "Screen flash when there is damage or other momentary event in the need of pazazz", Saved = true )]
	public static bool EffectEnabled { get; set; } = true;
	[Button, Tint( EditorTint.Red )]
	void TestFlashRed()
	{
		Set( Color.Red, 5.25f );
	}

	[Button, Tint( EditorTint.Blue )]
	void TestFlashBlue()
	{
		Set( Color.Blue, 5.25f );
	}

	[Button]
	void KingdomeCome()
	{
		Set( Color.White, 1f );
	}

	public static void Set( Color color, float FadeOutSpeed )
	{
		if ( EffectEnabled )
		{
			Instance.Strength = 1f;
			Instance.FlashColor = color;
			Instance.FadeOutSpeed = FadeOutSpeed;
		}
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();
		if ( EffectEnabled ) Strength = Math.Max( Strength - Time.Delta / FadeOutSpeed, 0f );
	}

	public override void Render()
	{
		if ( Strength.AlmostEqual( 0 ) ) return;

		Attributes.Set( "screen_flash_strength", Easing.QuadraticInOut( Strength ) );
		Attributes.Set( "screen_flash_color", FlashColor );

		var blit = BlitMode.WithBackbuffer( Shader, Sandbox.Rendering.Stage.BeforePostProcess, 5000, false );
		Blit( blit, "Colorflash Overlay" );
	}

	private static Material Shader = Material.FromShader( "postprocess_colorflash.shader" );
}
