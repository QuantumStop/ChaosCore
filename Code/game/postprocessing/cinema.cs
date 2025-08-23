namespace chaoscore;

using Sandbox.Utility;
using System;

[Category( "Post Processing" )]
[Icon( "crop_16_9" )]

public sealed class Cinema : PostProcess, Component.ExecuteInEditor
{

	//	public RangeAttribute(float min, float max, float step = 0.01f, bool clamped = true, bool slider = true)
	[MakeDirty, Property, Feature( "Debug" ), Range( 0, 1 ), Step( 0.00001f ), ReadOnly] private float Progression { get; set; } = 0f;
	[Property, Feature( "Debug" ), ReadOnly] private int which_button { get; set; } = 0;
	[Property, Feature( "Debug" ), Title( "Are sides letterboxed" ), ReadOnly] private bool LetterboxSides { get; set; } = false;

	[MakeDirty, Property, Feature( "Settings" ), Range( 1, 3 ), Step( 0.01f )] public float Target_Ratio { get; set; } = 2.4f;
	[MakeDirty, Property, Feature( "Settings" ), Range( 0, 0.5f )] public float Ratio_Tolerance { get; set; } = 0.1f;
	[MakeDirty, Property, Feature( "Settings" ), Range( 0.25f, 2 )] public float Speed { get; set; } = 1.0f;

	[Button( "Appear", "exposure" ), Feature( "Debug" )]
	public void Appear()
	{
		Progression = 0;
		which_button = 1;
		Log.Info( "button = 1" );
	}

	[Button( "Disappear", "exposure" ), Feature( "Debug" )]
	public void Disappear()
	{
		Progression = 1;
		which_button = 2;
		Log.Info( "button = 2" );
	}

	protected override void OnFixedUpdate()
	{

		if ( which_button == 1 )
		{
			Progression += Easing.SineEaseOut( (Time.Delta * Speed) );
			//			Progression = Easing.SineEaseOut( Progression );

			// stop moving if reached 1
			if ( Progression >= 1 )
			{
				Progression = 1;
				which_button = 0;
				Log.Info( "zero" );
			}
		}
		else if ( which_button == 2 )
		{
			Progression -= (Time.Delta * Speed);
			//			Progression = Easing.SineEaseIn( Progression );

			// stop moving if reached 0
			if ( Progression <= 0 )
			{
				Progression = 0;
				which_button = 0;
				Log.Info( "zero 2" );
			}
		}

	}

	Sandbox.Rendering.CommandList Commands;

	protected override void OnEnabled()
	{
		Commands = new Sandbox.Rendering.CommandList( "Cinema" );
		Camera.AddCommandList( Commands, Sandbox.Rendering.Stage.AfterPostProcess, 5000 );
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

		if ( Progression <= 0 )
			return;

		Commands.Attributes.GrabFrameTexture( "ColorBuffer", false );

		if ( (Screen.Width / Screen.Height) > (Target_Ratio - Ratio_Tolerance) )
		{
			LetterboxSides = true;
			Commands.Attributes.SetCombo( "D_VERTICAL", 1 );
		}
		else
		{
			LetterboxSides = false;
			Commands.Attributes.SetCombo( "D_VERTICAL", 0 );
		}

		Commands.Attributes.Set( "vProgress", Progression );
		Commands.Attributes.Set( "vRatio", Target_Ratio );

		Commands.Blit( Material.FromShader( "cinema" ) );
	}

}
