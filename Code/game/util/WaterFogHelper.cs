namespace chaoscore;

[Category( "Rendering" )]
[Icon( "foggy" )]
public class WaterFogHelper : Component
{

	[RequireComponent] public MeshComponent meshcom { get; set; }

	[Property, Range( 0, 1 )] float FogDensity { get; set; } = 0.5f;
	[Property, Range( 0, 1024 ), Step( 1 )] float FogEndDistance { get; set; } = 128.0f;
	[Property] Color FogColor { get; set; } = Color.Magenta.WithAlpha( 1 );

	protected override void OnDirty()
	{
		if ( !meshcom.IsValid() )
			return;

		//		meshcom.SceneObject.Attributes.Set( "FogEnd", FogEndDistance );
		//		meshcom.SceneObject.Attributes.Set( "FogDensity", FogDensity );
		//		meshcom.SceneObject.Attributes.Set( "FogColor", FogColor );

		base.OnDirty();
	}
}
