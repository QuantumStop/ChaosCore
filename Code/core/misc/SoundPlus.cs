namespace Core;

using System;

public static class SoundPlus
{
	public static float DbToFloat( float db ) => MathF.Pow( 10, db / 20 );
}
