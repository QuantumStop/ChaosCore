using Sandbox.Utility;
using System;

namespace Core;

[Category( "Post Processing" )]
[Icon( "flash_on" )]
public sealed class ScreenFlash : BasePostProcess<ScreenFlash>
{
	public static ScreenFlash Instance { get; set; }

	private float _strength { get; set; } = 0f;
	private Color _flashColor { get; set; }
	private float _fadeOutSpeed { get; set; }

	[ConVar( "r_screenflash", ConVarFlags.Saved, Help = "Screen flash when there is damage or other momentary event in the need of pazazz", Saved = true )]
	public static bool EffectEnabled { get; set; } = true;
	[Button, Tint( EditorTint.Red )]
	void TestFlashRed() => Set( Color.Red, 5.25f );

	[Button, Tint( EditorTint.Blue )]
	void TestFlashBlue() => Set( Color.Blue, 5.25f );

	[Button]
	void KingdomeCome() => Set( Color.White, 1f );

	protected override void OnStart()
	{
		base.OnStart();

		// Make sure we never actually get NaN, that we can get due to the fact our instance is static.
		// This fixes screen becoming white on a scene load/game reload.
		_strength = float.IsFinite( _strength ) ? _strength : 0f;
		_fadeOutSpeed = float.IsFinite( _fadeOutSpeed ) && _fadeOutSpeed > 0f
			? _fadeOutSpeed
			: 1f;
	}

	public static void Set( Color color, float FadeOutSpeed, float strength = 1f )
	{
		if ( EffectEnabled )
		{
			Instance._strength = strength;
			Instance._flashColor = color;
			Instance._fadeOutSpeed = FadeOutSpeed;
		}
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( EffectEnabled ) _strength = Math.Max( _strength - WorldTime.Delta / _fadeOutSpeed, 0f );
	}

	public override void Render()
	{
		if ( _strength.AlmostEqual( 0 ) ) return;


		Attributes.Set( "screen_flash_strength", Easing.QuadraticInOut( _strength ) );
		Attributes.Set( "screen_flash_color", _flashColor );

		var blit = BlitMode.WithBackbuffer( _shader, Sandbox.Rendering.Stage.BeforePostProcess, 5000, false );
		Blit( blit, "Colorflash Overlay" );
	}

	private static Material _shader = Material.FromShader( "postprocess_colorflash.shader" );
}
