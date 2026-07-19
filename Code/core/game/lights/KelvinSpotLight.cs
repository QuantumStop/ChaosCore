using System;
namespace Core;

[Category( "Light" )]
[Icon( "light_mode" )]
[EditorHandle( "" )]

public class KelvinSpotLight : SpotLight
{
	[Property, Header( "Mode" )]
	public LightUnits.ColorMode ColorMode
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Dirty();
			}
		}
	} = LightUnits.ColorMode.Color;

	[Space]

	[Property, ShowIf( nameof( ColorMode ), LightUnits.ColorMode.Color ), Header( "Color" ), ColorUsage( false, false )]
	public Color RGB_Color
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Dirty();
			}
		}
	} = Color.White.WithAlpha( 1 );

	[Space]
	[Property, ShowIf( nameof( ColorMode ), LightUnits.ColorMode.ColorTemperature )] public LightUnits.TemperatureMode TemperatureMode { get; set; } = LightUnits.TemperatureMode.Kelvin;
	[Property, ShowIf( nameof( IsKelvin ), true ), Range( 1000, 20000 ), Step( 50 ), Header( "Temperature" )]
	public float KelvinTemperature
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Dirty();
			}
		}
	} = 6500f;
	[Property, ShowIf( nameof( IsKelvin ), true )] LightUnits.ColorPresets ColorPreset { get; set; } = LightUnits.ColorPresets.NeutralWhite;
	[Button, ShowIf( nameof( IsKelvin ), true )]
	void ApplyPreset()
	{
		KelvinTemperature = (float)ColorPreset;
		Dirty();
	}

	[Property, ShowIf( nameof( IsMired ), true ), Range( 50, 1000 ), Step( 1 ), Header( "Temperature" )]
	public float MiredTemperature
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Dirty();
			}
		}
	} = 154f;

#if PLU
	/// <summary>
	/// Lumens
	/// </summary>
	[Property, Range( 1, 20000 ), Step( 10 ), Title( "Lumen" ), Header( "Brightness" )]
	public float Brightness
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Dirty();
			}
		}
	} = 1000f;
	[Property] LightUnits.LumenBrightnessPresets LumenBrightnessPresets { get; set; } = LightUnits.LumenBrightnessPresets.InteriorLight;
	[Button]
	void ApplyBrightnessPreset()
	{
		Brightness = (float)LumenBrightnessPresets;
		Dirty();
	}
	/// <summary>
	/// Makes the brightness falloff (when angle is changed) less shit
	/// </summary>
	[Property]
	private bool Focused
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Dirty();
			}
		}
	} = false;

	/// <summary>
	/// The light specific calculation
	/// </summary>
	private float energy_spot() { return MathF.PI; }
	/// <summary>
	/// Conversion to candela
	/// </summary>
	private float ResultBrightness => Brightness / energy_spot();
#else
	[Property, Range( 0, 15 ), Title( "Brightness" ), Header( "Brightness" )]
	public float Brightness
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Dirty();
			}
		}
	} = 1;
	/// <summary>
	/// Due to how stuff is supossed to be rendered, the diffuse is intended to be darker by a factor of PI, which may not be desirable if WYSIWYG color values are expected. 
	/// This compensates the brightness automatically, if desired. 
	/// Hammer lights don't have that, so they would have to be manually adjusted if needed.
	/// </summary>
	[Property, Title( "PI Compensation" )]
	public bool PI
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Dirty();
			}
		}
	} = true;
#if PI
	private float ResultBrightness => Brightness * (PI ? MathF.PI : 1.0f);
#else
	private float ResultBrightness => Brightness;
#endif
#endif

	/// <summary>
	/// Force refresh the light
	/// </summary>
	public void Refresh() => Dirty();

	private void Dirty()
	{
#if PLU
		if ( Focused )
			ConeInner = ConeOuter;
		else
			ConeInner = 0;
#endif

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

		LightColor = RGB_Color * ResultBrightness;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		var editorvis = "models/editor/spot.vmdl";

		if ( editorvis.EndsWith( ".vmdl" ) )
		{
			var vmdl = Model.Load( editorvis );
			Gizmo.Draw.Color = RGB_Color;
			Gizmo.Draw.Model( vmdl ).ColorTint = RGB_Color;
			Gizmo.Hitbox.Model( vmdl );
			Gizmo.Draw.Model( vmdl ).Flags.CastShadows = false;

			if ( Gizmo.IsHovered )
			{
				Gizmo.Draw.Color = Color.Orange.WithAlpha( (((float)Math.Sin( Time.Now * 20f )) * 0.3f) + 0.7f );
				Gizmo.Draw.LineBBox( vmdl.Bounds );
			}
			else if ( Gizmo.IsSelected )
			{
				Gizmo.Draw.Color = Color.White;
				Gizmo.Draw.LineBBox( vmdl.Bounds );
			}
		}
	}

	float GetHalfConeAngle()
	{
		float ClampedInnerConeAngle = Math.Clamp( ConeInner, 0.0f, 89.0f ) * MathF.PI / 180.0f;
		float ClampedOuterConeAngle = Math.Clamp( ConeOuter * MathF.PI / 180.0f, ClampedInnerConeAngle + 0.001f, 89.0f * MathF.PI / 180.0f + 0.001f );
		return ClampedOuterConeAngle;
	}

	float GetCosHalfConeAngle()
	{
		return MathF.Cos( GetHalfConeAngle() );
	}

	private bool IsKelvin => (ColorMode == LightUnits.ColorMode.ColorTemperature) && (TemperatureMode == LightUnits.TemperatureMode.Kelvin);
	private bool IsMired => (ColorMode == LightUnits.ColorMode.ColorTemperature) && (TemperatureMode == LightUnits.TemperatureMode.Mired);
}
