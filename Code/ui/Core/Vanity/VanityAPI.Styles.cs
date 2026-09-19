namespace Core;

using System;

public static partial class VanityAPI
{
	public static string GetRootStyle( VanityChannel channel )
	{
		if ( channel is null )
			return string.Empty;

		return
			$"position:absolute;" +
			$"top:0;left:0;width:100%;height:100%;" +
			$"z-index:{channel.ZIndex};";
	}

	public static string GetColorStyle( VanityChannel channel )
	{
		if ( channel is null )
			return string.Empty;

		float alpha = GetEffectAlpha( channel );
		Color color = channel.BackgroundColor.WithAlphaMultiplied( alpha );

		return
			$"position:absolute;" +
			$"top:0;left:0;width:100%;height:100%;" +
			$"background-color:{color.Rgba};";
	}

	public static string GetTextureStyle( VanityChannel channel )
	{
		if ( channel?.Texture is null )
			return string.Empty;

		float alpha = GetEffectAlpha( channel );

		return
			$"position:absolute;" +
			$"top:0;left:0;width:100%;height:100%;" +
			$"background-image:url({channel.Texture});" +
			$"background-size:cover;" +
			$"background-position:center;" +
			$"opacity:{alpha};";
	}

	public static float GetEffectAlpha( VanityChannel channel )
	{
		if ( channel is null )
			return 1f;

		float holdTime;
		float fadeInTime;
		float fadeOutTime;
		float fadeOutStart;

		float elapsed = WorldTime.Now - channel.StartTime;

		if ( channel.Effect is VanityScanOutEffect scan )
		{
			holdTime = MathF.Max( scan.HoldTime, 0f );
			fadeOutTime = MathF.Max( scan.FadeOutTime, 0f );
			fadeOutStart = MathF.Max( scan.ScanTime, 0f ) + holdTime;

			if ( fadeOutTime > 0f && elapsed > fadeOutStart )
				return MathX.Clamp( 1f - ((elapsed - fadeOutStart) / fadeOutTime), 0f, 1f );

			return 1f;
		}

		if ( channel.Effect is not VanityFadeEffect fade )
			return 1f;

		fadeInTime = MathF.Max( fade.FadeInTime, 0f );
		holdTime = MathF.Max( fade.HoldTime, 0f );
		fadeOutTime = MathF.Max( fade.FadeOutTime, 0f );

		switch ( fade.Direction )
		{
			case VanityDirection.In:
				{
					if ( fadeInTime <= 0f )
						return 1f;

					return MathX.Clamp( elapsed / fadeInTime, 0f, 1f );
				}

			case VanityDirection.Out:
				{
					if ( elapsed <= holdTime )
						return 1f;

					if ( fadeOutTime <= 0f )
						return 0f;

					return MathX.Clamp( 1f - ((elapsed - holdTime) / fadeOutTime), 0f, 1f );
				}

			case VanityDirection.InOut:
				{
					if ( fadeInTime > 0f && elapsed < fadeInTime )
						return MathX.Clamp( elapsed / fadeInTime, 0f, 1f );

					fadeOutStart = fadeInTime + holdTime;

					if ( fadeOutTime > 0f && elapsed > fadeOutStart )
						return MathX.Clamp( 1f - ((elapsed - fadeOutStart) / fadeOutTime), 0f, 1f );

					return 1f;
				}
		}

		return 1f;
	}
}
