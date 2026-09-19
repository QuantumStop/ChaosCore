namespace Core;

using System;
using System.Globalization;
using System.Text.RegularExpressions;

public static partial class VanityAPI
{
	private const float _scanCharRevealTime = 0.4f;

	private static readonly Regex _localizationTokenRegex = new( @"#([\w\.]+)", RegexOptions.Compiled );

	public static void PrepareText( VanityChannel channel )
	{
		if ( channel?.TextScope is null )
			return;

		channel.Lines = GetTextLines( channel.TextScope.Text );

		int totalCharacterCount = 0;

		foreach ( string line in channel.Lines )
			totalCharacterCount += line?.Length ?? 0;

		channel.TotalCharacterCount = totalCharacterCount;
	}

	public static string GetTextBlockStyle( VanityChannel channel )
	{
		if ( channel?.TextScope is null )
			return string.Empty;

		string posX = (MathX.Clamp( channel.PosX, 0f, 1f ) * 100f).ToString( "F2", CultureInfo.InvariantCulture );
		string posY = (MathX.Clamp( channel.PosY, 0f, 1f ) * 100f).ToString( "F2", CultureInfo.InvariantCulture );

		float alpha = GetEffectAlpha( channel );

		return
			$"position:absolute;" +
			$"left:{posX}%;top:{posY}%;" +
			$"transform:translate(-{posX}%, -{posY}%);" +
			GetTextScopeStyle( channel, alpha, channel.Effect is not VanityScanOutEffect ) +
			$"text-align:{GetTextAlign( channel )};" +
			$"align-items:{GetTextAlignItems( channel )};";
	}

	private static string GetTextScopeStyle( VanityChannel channel, float alpha, bool includePaintEffects )
	{
		var scope = channel.TextScope;
		Color color = scope.TextColor;

		string style =
			$"font-size:{scope.FontSize.ToString( "F2", CultureInfo.InvariantCulture )}px;" +
			$"font-family:{scope.FontName};" +
			$"font-weight:{scope.FontWeight};" +
			$"font-style:{(scope.FontItalic ? "italic" : "normal")};" +
			$"font-variant-numeric:{ToCssKeyword( scope.FontVariantNumeric )};" +
			$"line-height:{scope.LineHeight.ToString( "F3", CultureInfo.InvariantCulture )};" +
			$"letter-spacing:{scope.LetterSpacing.ToString( "F2", CultureInfo.InvariantCulture )}px;" +
			$"word-spacing:{scope.WordSpacing.ToString( "F2", CultureInfo.InvariantCulture )}px;" +
			$"image-rendering:{ToCssKeyword( scope.FilterMode )};" +
			$"font-smooth:{ToCssKeyword( scope.FontSmooth )};";

		// Scanout paints each character independently, so don't apply the
		// block paint effects in that case.
		if ( includePaintEffects )
			style += GetTextScopePaintStyle( channel, color, alpha );

		return style;
	}

	private static string GetTextScopePaintStyle( VanityChannel channel, Color textColor, float alpha )
	{
		var scope = channel.TextScope;

		string style = $"font-color:{textColor.WithAlphaMultiplied( alpha ).Rgba};";

		// Prefer the regular paint pass when enabled, otherwise use the
		// equivalent "under" effect.
		var outline = scope.Outline.Enabled ? scope.Outline : scope.OutlineUnder;
		var shadow = scope.Shadow.Enabled ? scope.Shadow : scope.ShadowUnder;

		if ( outline.Enabled )
		{
			style +=
				$"text-stroke:" +
				$"{outline.Size.ToString( "F2", CultureInfo.InvariantCulture )}px " +
				$"{outline.Color.WithAlphaMultiplied( alpha ).Rgba};";
		}

		if ( shadow.Enabled )
		{
			style +=
				$"text-shadow:" +
				$"{shadow.Offset.x.ToString( "F2", CultureInfo.InvariantCulture )}px " +
				$"{shadow.Offset.y.ToString( "F2", CultureInfo.InvariantCulture )}px " +
				$"{shadow.Size.ToString( "F2", CultureInfo.InvariantCulture )}px " +
				$"{shadow.Color.WithAlphaMultiplied( alpha ).Rgba};";
		}

		return style;
	}

	public static string GetScanCharStyle( VanityChannel channel, int charIndex, int totalCharCount )
	{
		if ( channel?.TextScope is null )
			return string.Empty;

		if ( channel.Effect is not VanityScanOutEffect scan )
			return string.Empty;

		// Leave enough time for the final character to complete its reveal.
		float scanTime = MathF.Max( scan.ScanTime - _scanCharRevealTime, 0f );

		// Spread character starts evenly across the scan duration.
		float delay = totalCharCount > 1 ? scanTime / (totalCharCount - 1) * charIndex : 0f;

		float elapsed = WorldTime.Now - channel.StartTime;
		float reveal = MathX.Clamp( (elapsed - delay) / _scanCharRevealTime, 0f, 1f );

		float flashTime = MathF.Max( scan.FlashTime, 0.0001f );
		float flashDelay = MathF.Max( scan.FlashDelay, 0f );

		// Start at the scan color and blend back toward the normal text color.
		float flash = 1f - MathX.Clamp( (elapsed - delay - flashDelay) / flashTime, 0f, 1f );

		float alpha = reveal * GetEffectAlpha( channel );

		Color textColor = channel.TextScope.TextColor;
		Color scanColor = scan.FlashColor.a > 0f ? scan.FlashColor : textColor;
		Color color = Color.Lerp( textColor, scanColor, flash );

		return $"display:inline;" + GetTextScopePaintStyle( channel, color, alpha );
	}

	public static string GetResolvedText( string rawText )
	{
		if ( string.IsNullOrWhiteSpace( rawText ) )
			return string.Empty;

		// Resolve any #localization.tokens embedded in the displayed text.
		return _localizationTokenRegex.Replace( rawText, match =>
		{
			string token = match.Groups[1].Value;
			string localized = Language.GetPhrase( token );

			// Keep the original token if no localized phrase exists.
			return string.IsNullOrEmpty( localized ) ? match.Value : localized;
		}
		);
	}

	public static string[] GetTextLines( string rawText )
	{
		string resolvedText = GetResolvedText( rawText );

		if ( string.IsNullOrEmpty( resolvedText ) )
			return [];

		// Support both actual newlines and "\n"
		return resolvedText
			.Replace( "\r\n", "\n" )
			.Replace( '\r', '\n' )
			.Replace( "\\n", "\n" )
			.Split( '\n' );
	}

	private static string GetTextAlign( VanityChannel channel )
	{
		return channel.Alignment switch
		{
			Sandbox.UI.TextAlign.Center => "center",
			Sandbox.UI.TextAlign.Right => "right",
			_ => "left"
		};
	}

	private static string GetTextAlignItems( VanityChannel channel )
	{
		return channel.Alignment switch
		{
			Sandbox.UI.TextAlign.Center => "center",
			Sandbox.UI.TextAlign.Right => "flex-end",
			_ => "flex-start"
		};
	}

	private static string ToCssKeyword( object value )
	{
		return value?.ToString()?.ToLowerInvariant() ?? string.Empty;
	}
}
