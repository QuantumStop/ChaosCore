namespace Core;

[Category( "Post Processing" )]
[Icon( "lens_blur" )]
public sealed class Underwater : BasePostProcess<Underwater>
{
	private static readonly Texture NormA = Texture.Load( "materials/water/textures/water_normal.vtex" );
	private static readonly Texture NormB = Texture.Load( "materials/water/textures/water_normal4.vtex" );
	private static readonly Texture NormC = Texture.Load( "materials/water/textures/water_normal3.vtex" );
	private static readonly Texture NormD = Texture.Load( "materials/water/textures/water_normal4.vtex" );
	private static readonly Texture Noise = Texture.Load( "materials/water/textures/water_noise.vtex" );

	private static Material Shader = Material.FromShader( "underwater" );

	public override void Render()
	{
		if ( BasePlayer.Local.IsValid() && BasePlayer.Local.IsUnderwater )
		{
			Attributes.Set( "NormalA", NormA );
			Attributes.Set( "NormalB", NormB );
			Attributes.Set( "NormalC", NormC );
			Attributes.Set( "NormalD", NormD );
			Attributes.Set( "NoiseAll", Noise );
			var blit = BlitMode.WithBackbuffer( Shader, Sandbox.Rendering.Stage.BeforePostProcess, 555, true );
			Blit( blit, "Underwater Overlay" );
		}
	}
}
