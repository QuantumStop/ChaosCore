using System;
namespace Core;

[Category( "Light" )]
[Icon( "light_mode" )]
[EditorHandle( "" )]

public class KelvinDirectionalLight : DirectionalLight
{
	[Property, MakeDirty, Header( "Mode" )] public LightUnits.ColorMode ColorMode { get; set; } = LightUnits.ColorMode.Color;

	[Space]

	[Property, ShowIf( nameof( ColorMode ), LightUnits.ColorMode.Color ), MakeDirty, Header( "Color" ), ColorUsage( false, false )] public Color RGB_Color { get; set; } = Color.White.WithAlpha( 1 );

	[Space]
	[Property, ShowIf( nameof( ColorMode ), LightUnits.ColorMode.ColorTemperature )] public LightUnits.TemperatureMode TemperatureMode { get; set; } = LightUnits.TemperatureMode.Kelvin;
	[Property, ShowIf( nameof( IsKelvin ), true ), Range( 1000, 20000 ), Step( 50 ), MakeDirty, Header( "Temperature" )] public float KelvinTemperature { get; set; } = 6500f;
	[Property, ShowIf( nameof( IsKelvin ), true )] LightUnits.ColorPresets ColorPreset { get; set; } = LightUnits.ColorPresets.NeutralWhite;
	[Button, ShowIf( nameof( IsKelvin ), true )]
	void ApplyPreset()
	{
		KelvinTemperature = (float)ColorPreset;
		OnDirty();
	}

	[Property, ShowIf( nameof( IsMired ), true ), Range( 50, 1000 ), Step( 1 ), MakeDirty, Header( "Temperature" )] public float MiredTemperature { get; set; } = 154f;

#if PLU
	[Property, Range( 1, 110000 ), Step( 1 ), MakeDirty, Title( "Lux" ), Header( "Brightness" )] public float Brightness { get; set; } = 100000f;
	[Property] public LightUnits.SunBrightnessPresets SunBrightnessPreset { get; set; } = LightUnits.SunBrightnessPresets.Noon;
	[Button]
	void ApplyBrightnessPreset()
	{
		Brightness = (float)SunBrightnessPreset;
		OnDirty();
	}
#else
[Property, Range( 0, 15 ), MakeDirty, Title( "Brightness" ), Header( "Brightness" )] public float Brightness { get; set; } = 1;
#endif
	[Property, Hide, Space] new Color SkyColor { get; set; } = Color.Black.WithAlpha( 0 );

	protected override void OnDirty()
	{
		base.OnDirty();

		if ( IsKelvin )
		{
			RGB_Color = LightUnits.CorrelatedColorTemperatureToRGB( KelvinTemperature );
			MiredTemperature = LightUnits.KelvinToMired( KelvinTemperature );
		}
		if ( IsMired )
		{
			RGB_Color = LightUnits.CorrelatedColorTemperatureToRGB( LightUnits.MiredToKelvin( MiredTemperature ) );
			KelvinTemperature = LightUnits.MiredToKelvin( MiredTemperature );
		}

		LightColor = RGB_Color * Brightness;
		Scene.RenderAttributes.Set( "SunLux", Brightness );
	}

	protected override void DrawGizmos()
	{
		// base.DrawGizmos();

		var editorvis = "models/editor/sun.vmdl";

		if ( editorvis.EndsWith( ".vmdl" ) )
		{
			var vmdl = Model.Load( editorvis );
			Gizmo.Draw.Color = RGB_Color;
			Gizmo.Draw.Model( vmdl ).ColorTint = RGB_Color;
			Gizmo.Hitbox.Model( vmdl );
			Gizmo.Draw.Model( vmdl ).Flags.CastShadows = false;

			if ( Gizmo.IsHovered )
			{
				Gizmo.Draw.Color = Color.Orange/*.WithAlpha( (((float)Math.Sin( Time.Now * 20f )) * 0.3f) + 0.7f )*/;
				Gizmo.Draw.LineBBox( vmdl.Bounds );
			}
			else if ( Gizmo.IsSelected )
			{
				Gizmo.Draw.Color = Color.White;
				Gizmo.Draw.LineBBox( vmdl.Bounds );
			}
		}
	}

	private bool IsKelvin => (ColorMode == LightUnits.ColorMode.ColorTemperature) && (TemperatureMode == LightUnits.TemperatureMode.Kelvin);
	private bool IsMired => (ColorMode == LightUnits.ColorMode.ColorTemperature) && (TemperatureMode == LightUnits.TemperatureMode.Mired);
}
