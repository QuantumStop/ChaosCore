using System;

public class FlickeringLight : SpotLight
{
	[Property] public Gradient LightGradient { get; set; }
	protected override void DrawGizmos()
	{
		OnFixedUpdate();
		base.DrawGizmos();
	}

	protected override void OnFixedUpdate()
	{
		var fps = 20f;
		var rnd1 = 150000f * MathF.Sin( 1030000f * MathF.Floor( (Time.Now * fps) - 0.5f ) );
		rnd1 -= MathF.Floor( rnd1 );
		rnd1 = (rnd1 * (0.8f - 0.01f)) + 0.01f; //0.01,0.8

		var rnd2 = 150000f * MathF.Sin( 133000f * MathF.Floor( (Time.Now * fps) - 0.5f ) );
		rnd2 -= MathF.Floor( rnd2 );
		rnd2 = (rnd2 * (0.01f - 0f)) + 0; //0,0.01

		var intensity = (MathF.Floor( MathF.Sin( ((MathF.Floor( (Time.Now * fps) - 0.5f )) / fps) + MathF.Sin( ((MathF.Floor( (Time.Now * fps) - 0.5f )) / fps) * 5f ) ) + 1f ) * rnd1) + rnd2 * (MathF.Sin( ((MathF.Floor( (Time.Now * fps) - 0.5f )) / fps) * 3f ) + 1f);

		LightColor = LightGradient.Evaluate( 1f - intensity ).Darken( intensity ) * 10f;
		base.OnFixedUpdate();
	}
}
