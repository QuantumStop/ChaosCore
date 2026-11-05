using System;
using System.Runtime.CompilerServices;
namespace Core;

[PresetTarget( "KelvinSpotLight", Name = "Kelvin Spot Light", Icon = "light_mode", Category = "Rendering", IncludeAll = true )]

[Category( "Light" )]
[Icon( "light_mode" )]
[EditorHandle( "" )]

public class KelvinSpotLight : SpotLight
{
	[Property, PresetSelector, Header( "Mode" ), Order( 0 )] public GenericPresetResource Preset { get; set; }

	private const float _oneOverPI = 1 / MathF.PI;

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
#if PI
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

	private float _resultBrightness => PI ? MathF.PI : _oneOverPI;
	private float _piOld => PI ? _oneOverPI : 1;
#else
	private float _resultBrightness => 1;
#endif
#endif

	/// <summary>Force refresh the light</summary>
	public void Refresh() => Dirty();

	private void Dirty()
	{
#if PLU && PI
		if ( Focused )
			ConeInner = ConeOuter;
		else
			ConeInner = 0;
#endif

		LightColor = LightColor.WithColorMultiplied( _resultBrightness );
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		var editorvis = "models/editor/spot.vmdl";

		if ( editorvis.EndsWith( ".vmdl" ) )
		{
			var vmdl = Model.Load( editorvis );
#if PI
			Gizmo.Draw.Color = LightColor.WithColorMultiplied( _piOld );
#else
			Gizmo.Draw.Color = LightColor;
#endif

#if PI
			Gizmo.Draw.Model( vmdl ).ColorTint = LightColor.WithColorMultiplied( _piOld );
#else
			Gizmo.Draw.Model( vmdl ).ColorTint = LightColor;
#endif
			Gizmo.Hitbox.Model( vmdl );
			Gizmo.Draw.Model( vmdl ).Flags.CastShadows = false;

			if ( Gizmo.IsHovered )
			{
				Gizmo.Draw.Color = Color.Orange.WithAlpha( (MathF.Sin( WorldTime.Now * 20f ) * 0.3f) + 0.7f );
				Gizmo.Draw.LineBBox( vmdl.Bounds );
			}
			else if ( Gizmo.IsSelected )
			{
				Gizmo.Draw.Color = Color.White;
				Gizmo.Draw.LineBBox( vmdl.Bounds );
			}
		}
	}
}
