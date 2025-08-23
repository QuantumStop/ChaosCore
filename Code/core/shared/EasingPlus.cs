using System;

public static class EasingPlus
{
	public delegate float Function( float f );

	public static readonly Dictionary<string, Function> Functions = new()
	{
		{ "ease-in-quad", EaseInQuad },
		{ "ease-out-quad", EaseOutQuad },
		{ "ease-in-out-quad", EaseInOutQuad },
		{ "ease-in-cubic", EaseInCubic },
		{ "ease-out-cubic", EaseOutCubic },
		{ "ease-in-out-cubic", EaseInOutCubic },
		{ "ease-in-quart", EaseInQuart },
		{ "ease-out-quart", EaseOutQuart },
		{ "ease-in-out-quart", EaseInOutQuart },
		{ "ease-in-quint", EaseInQuint },
		{ "ease-out-quint", EaseOutQuint },
		{ "ease-in-out-quint", EaseInOutQuint },
		{ "ease-in-sine", EaseInSine },
		{ "ease-out-sine", EaseOutSine },
		{ "ease-in-out-sine", EaseInOutSine },
		{ "ease-in-expo", EaseInExpo },
		{ "ease-out-expo", EaseOutExpo },
		{ "ease-in-out-expo", EaseInOutExpo },
		{ "ease-in-circ", EaseInCirc },
		{ "ease-out-circ", EaseOutCirc },
		{ "ease-in-out-circ", EaseInOutCirc },
		{ "linear", Linear },
		{ "spring", Spring },
		{ "ease-in-bounce", EaseInBounce },
		{ "ease-out-bounce", EaseOutBounce },
		{ "ease-in-out-bounce", EaseInOutBounce },
		{ "ease-in-back", EaseInBack },
		{ "ease-out-back", EaseOutBack },
		{ "ease-in-out-back", EaseInOutBack },
		{ "ease-in-elastic", EaseInElastic },
		{ "ease-out-elastic", EaseOutElastic },
		{ "ease-in-out-elastic", EaseInOutElastic }
	};

	// Easing functions

	public static float EaseInQuad( float f ) => f * f;

	public static float EaseOutQuad( float f ) => f * (2f - f);

	public static float EaseInOutQuad( float f )
	{
		f *= 2f;
		return f < 1f
			? 0.5f * f * f
			: -0.5f * ((f -= 1f) * (f - 2f) - 1f);
	}

	public static float EaseInCubic( float f ) => f * f * f;

	public static float EaseOutCubic( float f )
	{
		f -= 1f;
		return f * f * f + 1f;
	}

	public static float EaseInOutCubic( float f )
	{
		f *= 2f;
		return f < 1f
			? 0.5f * f * f * f
			: 0.5f * ((f -= 2f) * f * f + 2f);
	}

	public static float EaseInQuart( float f ) => f * f * f * f;

	public static float EaseOutQuart( float f )
	{
		f -= 1f;
		return -(f * f * f * f - 1f);
	}

	public static float EaseInOutQuart( float f )
	{
		f *= 2f;
		return f < 1f
			? 0.5f * f * f * f * f
			: -0.5f * ((f -= 2f) * f * f * f - 2f);
	}

	public static float EaseInQuint( float f ) => f * f * f * f * f;

	public static float EaseOutQuint( float f )
	{
		f -= 1f;
		return f * f * f * f * f + 1f;
	}

	public static float EaseInOutQuint( float f )
	{
		f *= 2f;
		return f < 1f
			? 0.5f * f * f * f * f * f
			: 0.5f * ((f -= 2f) * f * f * f * f + 2f);
	}

	public static float EaseInSine( float f ) => 1f - MathF.Cos( f * MathF.PI * 0.5f );

	public static float EaseOutSine( float f ) => MathF.Sin( f * MathF.PI * 0.5f );

	public static float EaseInOutSine( float f ) => 0.5f * (1f - MathF.Cos( MathF.PI * f ));

	public static float EaseInExpo( float f ) => f == 0f ? 0f : MathF.Pow( 2f, 10f * (f - 1f) );

	public static float EaseOutExpo( float f ) => f == 1f ? 1f : 1f - MathF.Pow( 2f, -10f * f );

	public static float EaseInOutExpo( float f )
	{
		if ( f == 0f || f == 1f )
			return f;
		f *= 2f;
		return f < 1f
			? 0.5f * MathF.Pow( 2f, 10f * (f - 1f) )
			: 0.5f * (2f - MathF.Pow( 2f, -10f * (f - 1f) ));
	}

	public static float EaseInCirc( float f ) => 1f - MathF.Sqrt( 1f - f * f );

	public static float EaseOutCirc( float f )
	{
		f -= 1f;
		return MathF.Sqrt( 1f - f * f );
	}

	public static float EaseInOutCirc( float f )
	{
		f *= 2f;
		return f < 1f
			? 0.5f * (1f - MathF.Sqrt( 1f - f * f ))
			: 0.5f * (MathF.Sqrt( 1f - (f -= 2f) * f ) + 1f);
	}

	public static float Linear( float f ) => f;

	public static float Spring( float f ) => MathF.Sin( f * MathF.PI * (0.2f + 2.5f * f * f) );

	public static float EaseInBounce( float f ) => 1f - BounceOut( 1f - f );

	public static float EaseOutBounce( float f ) => BounceOut( f );

	public static float EaseInOutBounce( float f )
	{
		if ( f < 0.5f )
			return EaseInBounce( f * 2f ) * 0.5f;
		else
			return EaseOutBounce( f * 2f - 1f ) * 0.5f + 0.5f;
	}

	public static float EaseInBack( float f )
	{
		const float s = 1.70158f;
		return f * f * ((s + 1f) * f - s);
	}

	public static float EaseOutBack( float f )
	{
		const float s = 1.70158f;
		return (f -= 1f) * f * ((s + 1f) * f + s) + 1f;
	}

	public static float EaseInOutBack( float f )
	{
		const float s = 1.70158f;
		f *= 2f;
		return f < 1f
			? 0.5f * (f * f * ((s + 1f) * f - s))
			: 0.5f * ((f -= 2f) * f * ((s + 1f) * f + s) + 2f);
	}

	public static float EaseInElastic( float f )
	{
		// const float c4 = (2f * MathF.PI) / 3f;
		return f == 0f
			? 0f
			: f == 1f
				? 1f
				: -MathF.Pow( 2f, 10f * (f - 1f) ) * MathF.Sin( (f - 1f) * 2f * MathF.PI / 0.3f );
	}

	public static float EaseOutElastic( float f )
	{
		//	const float c4 = (2f * MathF.PI) / 3f;
		return f == 0f
			? 0f
			: f == 1f
				? 1f
				: MathF.Pow( 2f, -10f * f ) * MathF.Sin( (f - 0.75f) * 2f * MathF.PI / 0.3f ) + 1f;
	}

	public static float EaseInOutElastic( float f )
	{
		//	const float c4 = (2f * MathF.PI) / 3f;
		if ( f == 0f || f == 1f )
			return f;
		f *= 2f;
		return f < 1f
			? -0.5f * MathF.Pow( 2f, 10f * (f - 1f) ) * MathF.Sin( (f - 1f) * 2f * MathF.PI / 0.3f )
			: MathF.Pow( 2f, -10f * (f - 1f) ) * MathF.Sin( (f - 1f) * 2f * MathF.PI / 0.3f ) * 0.5f + 1f;
	}

	private static float BounceOut( float f )
	{
		if ( f < 1f / 2.75f )
			return 7.5625f * f * f;
		if ( f < 2f / 2.75f )
			return 7.5625f * (f -= 1.5f / 2.75f) * f + 0.75f;
		if ( f < 2.5f / 2.75f )
			return 7.5625f * (f -= 2.25f / 2.75f) * f + 0.9375f;
		return 7.5625f * (f -= 2.625f / 2.75f) * f + 0.984375f;
	}

}
