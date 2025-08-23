using System;
namespace Core;

public class LightUnits
{
	static public Color CorrelatedColorTemperatureToRGB( float temperature )
	{
		Color FinalColor;

		// Temperature must fall between 1000 and 40000 degrees
		// The fitting require to divide kelvin by 1000 (allow more precision)
		float kelvin = Math.Clamp( temperature, 1000f, 40000f ) / 1000.0f;
		float kelvin2 = kelvin * kelvin;

		// Using 6570 as a pivot is an approximation, pivot point for red is around 6580 and for blue and green around 6560.
		// Calculate each color in turn (Note, clamp is not really necessary as all value belongs to [0..1] but can help for extremum).
		// Red
		FinalColor.r = kelvin < 6.570f ? 1.0f : Math.Clamp( (1.35651f + 0.216422f * kelvin + 0.000633715f * kelvin2) / (-3.24223f + 0.918711f * kelvin), 0.0f, 1.0f );
		// Green
		FinalColor.g = kelvin < 6.570f ?
			Math.Clamp( (-399.809f + 414.271f * kelvin + 111.543f * kelvin2) / (2779.24f + 164.143f * kelvin + 84.7356f * kelvin2), 0.0f, 1.0f ) :
			Math.Clamp( (1370.38f + 734.616f * kelvin + 0.689955f * kelvin2) / (-4625.69f + 1699.87f * kelvin), 0.0f, 1.0f );
		// Blue
		FinalColor.b = kelvin > 6.570f ? 1.0f : Math.Clamp( (348.963f - 523.53f * kelvin + 183.62f * kelvin2) / (2848.82f - 214.52f * kelvin + 78.8614f * kelvin2), 0.0f, 1.0f );

		FinalColor.a = 1f;

		return FinalColor;
	}

	public enum SunBrightnessPresets
	{
		[Description( "1 lx" ), Icon( "dark_mode" )] Moon = 1,
		[Description( "5 000 lx" ), Icon( "wb_twilight" )] LowSun = 5000,
		[Description( "20 000 lx" ), Icon( "cloud" )] Cloudy = 20000,
		[Description( "100 000 lx" ), Icon( "sunny" )] Noon = 100000,
	}

	public enum LumenBrightnessPresets
	{
		[Description( "12 lm" ), Icon( "cake" )] CandleLight = 12,
		[Description( "300 lm" ), Icon( "floor_lamp" )] DecorativeLight = 300,
		[Description( "1000 lm" ), Icon( "scene_light" )] InteriorLight = 1000,
		[Description( "10000 lm" ), Icon( "wall_lamp" )] ExteriorLight = 10000
	}

	public enum ColorPresets
	{
		[Description( "1700K" ), Icon( "fireplace" )] Match = 1700,
		[Description( "1830K" ), Icon( "cake" )] Candle = 1830,
		[Description( "2500K" ), Icon( "wb_twilight" )] SunSunrise = 2500,
		[Description( "2900K" ), Icon( "wb_iridescent" )] TungstenLampA = 2900,
		[Description( "3200K" ), Icon( "wb_iridescent" )] FluorescentLights = 3200,
		[Description( "4000K" ), Icon( "wb_incandescent" )] TungstenLampB = 4000,
		[Description( "5000K" ), Icon( "light_mode" )] SunNoon = 5000,
		[Description( "5600K" ), Icon( "wb_sunny" )] Daylight = 5600,
		[Description( "6000K" ), Icon( "contrast" )] NeutralWhite = 6500,
		[Description( "7000K" ), Icon( "wb_shade" )] OutdoorShade = 7000,
		[Description( "7500K" ), Icon( "foggy" )] Overcast = 7500,
		[Description( "9000K" ), Icon( "cloud" )] PartlyCloudy = 9000
	}

	public enum ColorMode
	{
		[Description( "Use regular RGB picker" ), Icon( "wb_auto" )] Color,
		[Description( "Use light temperature to determine color" ), Icon( "wb_incandescent" )] ColorTemperature
	}

	public enum LightUnit
	{
		Unitless,
		Lumen,
		Candela,
		Nits,
		EV
	}

	static private float ConvertValue( float x ) => 1000000 / x;
	/// <summary>
	/// Convert Kelvin to Mired
	/// </summary>
	/// <param name="k">Input Kelvin value</param>
	/// <returns>Mired value</returns>
	public static float KelvinToMired( float k ) => ConvertValue( k );
	/// <summary>
	/// Convert Mired to Kelvin
	/// </summary>
	/// <param name="m">Input Mired value</param>
	/// <returns>Kelvin value</returns>
	public static float MiredToKelvin( float m ) => ConvertValue( m );

	public enum TemperatureMode
	{
		[Description( "Conventional way to determine temperature" ), Icon( "wb_auto" )] Kelvin,
		[Description( "More linear spread of temperature colors. PS: Inverted" ), Icon( "wb_incandescent" )] Mired
	}
}
