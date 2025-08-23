using System;
using Core;
namespace chaoscore;

[Category( "Post Processing" )]
[Icon( "dark_mode" )]

public sealed class Nightvision : PostProcess
{
	[MakeDirty, Property, Range( 0, 2 ), Step( 0.1f ), Group( "Settings" )] float NoiseStrength { get; set; } = 1f;
	[MakeDirty, Property, Group( "Debug" ), ReadOnly] public bool EffectEnabled { get; set; } = false;
	[Property, Group( "Components" )] public KelvinSpotLight SpotlightComponent { get; set; }
	[Property, Group( "Components" )] public SoundEvent StartSound { get; set; }
	[Property, Group( "Components" )] public SoundEvent LoopSound { get; set; }
	[Property, Group( "Components" )] public SoundEvent EndSound { get; set; }

	[MakeDirty, Property, Group( "Settings" )] public Texture NoiseTex { get; set; }

	public SoundHandle loopSoundHandle;
	Sandbox.Rendering.CommandList Commands;

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( Input.Pressed( "nightvis" ) && Player.Local?.LifeState == LifeState.Alive )
		{
			if ( !EffectEnabled ) Enable(); else Disable();
		}
		else if ( Player.Local?.LifeState == LifeState.Dead )
		{
			Disable();
		}
	}
	private void Enable()
	{
		if ( EffectEnabled )
			return;

		Commands = new Sandbox.Rendering.CommandList( "Nightvision" );
		Camera.AddCommandList( Commands, Sandbox.Rendering.Stage.BeforePostProcess, 4000 );
		OnDirty();

		EffectEnabled = true;

		SpotlightComponent.Enabled = true;

		if ( StartSound != null )
			Sound.Play( StartSound ).ListenLocal = true;

		if ( LoopSound != null )
		{
			loopSoundHandle = Sound.Play( LoopSound );
			loopSoundHandle.ListenLocal = true;
		}
	}
	private void Disable()
	{
		if ( !EffectEnabled )
			return;

		Camera.RemoveCommandList( Commands );
		Commands = null;

		EffectEnabled = false;

		SpotlightComponent.Enabled = false;

		if ( EndSound != null )
			Sound.Play( EndSound ).ListenLocal = true;

		if ( LoopSound != null )
			loopSoundHandle.Stop( 0.3f );
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
		Commands.Attributes.Set( "noise_strength", NoiseStrength );
		Commands.Attributes.Set( "noise", NoiseTex );

		Commands.Blit( Material.FromShader( "nightvision" ) );
	}
}
