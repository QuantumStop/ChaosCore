using System;

namespace Core;

[PresetTarget( "KelvinDirlLight", Name = "Kelvin Directional Light", Icon = "light_mode", Category = "Rendering", IncludeAll = true )]
[Category( "Light" )]
[Icon( "light_mode" )]
[EditorHandle( "" )]
public class KelvinDirectionalLight : DirectionalLight
{
	[Property, PresetSelector, Header( "Mode" ), Order( 0 )] public GenericPresetResource Preset { get; set; }

	private const float _oneOverPI = 1 / MathF.PI;

#if PLU
	[Property, Range( 1, 110000 ), Step( 1 ), Title( "Lux" ), Header( "Brightness" )]
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
	} = 100000f;

	[Property] public LightUnits.SunBrightnessPresets SunBrightnessPreset { get; set; } = LightUnits.SunBrightnessPresets.Noon;

	[Button]
	void ApplyBrightnessPreset()
	{
		Brightness = (float)SunBrightnessPreset;
		Dirty();
	}

	private float _resultBrightness => Brightness;
#else
#if PI
	/// <summary>
	/// Compensates for diffuse rendering being darker by a factor of PI.
	/// Hammer lights must be adjusted manually if needed.
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
	private float _resultBrightness => Brightness;
#endif
#endif

	[Property, Hide, Space]
	new Color SkyColor { get; set; } = Color.Black.WithAlpha( 0 );

	/// <summary>Force refresh the light.</summary>
	public void Refresh() => Dirty();

	private void Dirty()
	{
		LightColor = LightColor.WithColorMultiplied( _resultBrightness );
		SendAttribute();
	}

	private void SendAttribute()
	{
#if !PLU
		Scene.RenderAttributes.Set( "SunLux", PI ? MathF.PI : 1 );
#endif
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();
		SendAttribute();
	}

	protected override void DrawGizmos()
	{
		// base.DrawGizmos();

		var editorvis = "models/editor/sun.vmdl";

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
