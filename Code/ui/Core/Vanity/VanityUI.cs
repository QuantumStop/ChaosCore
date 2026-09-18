namespace Core;

using System;
using System.Globalization;
using System.Text.RegularExpressions;

public class VanityChannel
{
	public string Id { get; set; }

	// Textual content (optional depending on effect)
	public string Text { get; set; }
	public TextRendering.Scope TextScope { get; set; }

	// Positional and sizing information (used for overlays)
	public float PosX { get; set; } = 0.5f;
	public float PosY { get; set; } = 0.5f;

	// Timing controls
	public float FadeInTime { get; set; } = 0.5f;
	public float HoldTime { get; set; } = 1.0f;
	public float FadeOutTime { get; set; } = 0.5f;
	public float ScanTime { get; set; } = 1.0f;
	public float ScanFlashTime { get; set; } = 0.35f;
	public float ScanFlashDelay { get; set; } = 0.1f;
	public bool FadeFrom { get; set; } // Fade Out/In

	// Core styling
	public Color ScanColor { get; set; } = Color.White;
	public Color BackgroundColor { get; set; } = Color.Transparent;

	public int ZIndex { get; set; } = 0;
	public string Alignment { get; set; } = "TEXTALIGNMENT_LEFT";

	// Effect specifier
	public string Effect { get; set; } = "none"; // e.g., "fade", "scanout", "image", "shader"

	// Optional texture reference
	public Texture Texture { get; set; } = null;

	// Timed visibility tracking
	public float StartTime { get; set; }
	public float VisibilityStartTime { get; set; } = -1f;

	public bool IsTextual => !string.IsNullOrWhiteSpace( TextScope.Text );
	public bool IsVisualAsset => Texture.IsValid();

	public bool IsDrawPermanent { get; set; } = false;
}

public class VanityUI : PanelComponent
{
	public static VanityUI Local;

	[Property, ReadOnly] public Dictionary<string, VanityChannel> ActiveChannels { get; set; } = new();

	[Property] public float TimeElapsed { get; set; }

	private const float ScanCharRevealTime = 0.4f;

	public async void UpdateChannel( string id, VanityChannel channel )
	{
		// Remove existing channel to ensure clean state
		if ( ActiveChannels.ContainsKey( id ) )
		{
			ActiveChannels.Remove( id );

			StateHasChanged();
			await GameTask.Delay( 5 ); // small 5ms delay so we don't immediately read the same thing 
		}

		// Reset timing to now
		channel.StartTime = Time.Now;

		channel.VisibilityStartTime = Time.Now + (channel.Effect switch
		{
			"fade" => channel.FadeInTime, // explicitly add fade effect timing
			_ => 0f
		});

		ActiveChannels[id] = channel;

	}

	protected override void OnUpdate()
	{
		if ( ActiveChannels.Count == 0 )
			return;

		TimeElapsed = Time.Now - ActiveChannels.Values.Min( c => c.StartTime ); // Optional

		StateHasChanged();
	}

	public static string GetRootStyle( VanityChannel channel )
	{
		if ( channel is null )
			return "";

		return $"position: absolute; " +
			   $"top: 0; left: 0; width: 100%; height: 100%;" +
			   $"z-index: {channel.ZIndex};";
	}

	public static string GetTextBlockStyle( VanityChannel ch )
	{
		string posX = (MathX.Clamp( ch.PosX, 0f, 1f ) * 100f).ToString( "F2", CultureInfo.InvariantCulture );
		string posY = (MathX.Clamp( ch.PosY, 0f, 1f ) * 100f).ToString( "F2", CultureInfo.InvariantCulture );
		float alpha = GetTextAlpha( ch );

		return
			$"position:absolute;" +
			$"left:{posX}%;top:{posY}%;" +
			$"transform:translate(-{posX}%, -{posY}%);" +
			GetTextScopeStyle( ch, alpha, ch.Effect != "scanout" ) +
			$"white-space: pre-wrap;" +
			$"text-align:{GetTextAlign( ch )};" +
			$"align-items:{GetTextAlignItems( ch )};" +
			$"max-width:100%;" +
			$"max-height:100%;" +
			$"display: flex;" +
			$"flex-direction: column;";
	}

	private static string GetTextScopeStyle( VanityChannel ch, float alpha, bool includePaintEffects )
	{
		var scope = ch.TextScope;
		var color = scope.TextColor;

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

		if ( includePaintEffects )
			style += GetTextScopePaintStyle( ch, color, alpha );

		return style;
	}

	private static string GetTextScopePaintStyle( VanityChannel ch, Color textColor, float alpha )
	{
		var scope = ch.TextScope;
		string style = $"font-color:{textColor.ToCssRgba( textColor.a * alpha )};";
		var outline = scope.Outline.Enabled ? scope.Outline : scope.OutlineUnder;
		var shadow = scope.Shadow.Enabled ? scope.Shadow : scope.ShadowUnder;

		if ( outline.Enabled )
			style += $"text-stroke:{outline.Size.ToString( "F2", CultureInfo.InvariantCulture )}px {outline.Color.ToCssRgba( outline.Color.a * alpha )};";

		if ( shadow.Enabled )
			style += $"text-shadow:{shadow.Offset.x.ToString( "F2", CultureInfo.InvariantCulture )}px {shadow.Offset.y.ToString( "F2", CultureInfo.InvariantCulture )}px {shadow.Size.ToString( "F2", CultureInfo.InvariantCulture )}px {shadow.Color.ToCssRgba( shadow.Color.a * alpha )};";

		return style;
	}

	private static string ToCssKeyword( object value )
	{
		return value.ToString().ToLowerInvariant();
	}

	private static string GetTextAlign( VanityChannel ch )
	{
		return ch.Alignment switch
		{
			"TEXTALIGNMENT_CENTER" => "center",
			"TEXTALIGNMENT_RIGHT" => "right",
			_ => "left"
		};
	}

	private static string GetTextAlignItems( VanityChannel ch )
	{
		return ch.Alignment switch
		{
			"TEXTALIGNMENT_CENTER" => "center",
			"TEXTALIGNMENT_RIGHT" => "flex-end",
			_ => "flex-start"
		};
	}

	private static float GetTextAlpha( VanityChannel ch )
	{
		float elapsed = Time.Now - ch.StartTime;

		if ( ch.Effect != "fadeinout" && ch.Effect != "scanout" )
			return 1f;

		float fadeInTime = MathF.Max( ch.FadeInTime, 0f );
		float holdTime = MathF.Max( ch.HoldTime, 0f );
		float fadeOutTime = MathF.Max( ch.FadeOutTime, 0f );
		float introTime = ch.Effect == "scanout"
			? MathF.Max( ch.ScanTime, 0f )
			: fadeInTime;

		if ( ch.Effect == "fadeinout" && fadeInTime > 0f && elapsed < fadeInTime )
			return MathX.Clamp( elapsed / fadeInTime, 0f, 1f );

		float fadeOutStart = introTime + holdTime;

		if ( fadeOutTime > 0f && elapsed > fadeOutStart )
			return MathX.Clamp( 1f - ((elapsed - fadeOutStart) / fadeOutTime), 0f, 1f );

		return elapsed <= fadeOutStart + fadeOutTime ? 1f : 0f;
	}

	public static string GetScanCharStyle( VanityChannel ch, int charIndex, int totalCharCount )
	{
		float scanTime = MathF.Max( ch.ScanTime - ScanCharRevealTime, 0f );
		float delay = totalCharCount > 1
			? (scanTime / (totalCharCount - 1)) * charIndex
			: 0f;

		float elapsed = Time.Now - ch.StartTime;
		float reveal = MathX.Clamp( (elapsed - delay) / ScanCharRevealTime, 0f, 1f );
		float flashTime = MathF.Max( ch.ScanFlashTime, 0.0001f );
		float flashDelay = MathF.Max( ch.ScanFlashDelay, 0f );
		float flash = 1f - MathX.Clamp( (elapsed - delay - flashDelay) / flashTime, 0f, 1f );
		float alpha = reveal * GetTextAlpha( ch );
		Color textColor = ch.TextScope.TextColor;
		Color scanColor = ch.ScanColor.a > 0f ? ch.ScanColor : textColor;
		Color color = Color.Lerp( textColor, scanColor, flash );

		return
			$"display:inline;" +
			GetTextScopePaintStyle( ch, color, alpha );
	}

	private static readonly Regex LocalizationTokenRegex = new( @"#([\w\.]+)", RegexOptions.Compiled );

	public static string GetResolvedText( string rawText )
	{
		if ( string.IsNullOrWhiteSpace( rawText ) )
			return string.Empty;

		return LocalizationTokenRegex.Replace( rawText, match =>
		{
			string token = match.Groups[1].Value;
			string localized = Language.GetPhrase( token );
			return string.IsNullOrEmpty( localized ) ? match.Value : localized;
		} );
	}

	public static string[] GetTextLines( string rawText )
	{
		string resolvedText = GetResolvedText( rawText );

		if ( string.IsNullOrEmpty( resolvedText ) )
			return Array.Empty<string>();

		return resolvedText
			.Replace( "\r\n", "\n" )
			.Replace( '\r', '\n' )
			.Replace( "\\n", "\n" )
			.Split( '\n' );
	}

	public static string[] GetTextLines( VanityChannel channel )
	{
		return GetTextLines( channel.TextScope.Text );
	}

}

public static class ColorExtensions
{
	public static string ToCssRgba( this Color color, float alpha = 1f )
	{
		int r = (int)(color.r * 255);
		int g = (int)(color.g * 255);
		int b = (int)(color.b * 255);
		return $"rgba({r},{g},{b},{alpha.ToString( "F2", CultureInfo.InvariantCulture )})";
	}

	public static string ToCssHex( this Color color )
	{
		int r = (int)(color.r * 255);
		int g = (int)(color.g * 255);
		int b = (int)(color.b * 255);
		return $"#{r:X2}{g:X2}{b:X2}";
	}

	public static Color WithAlpha( this Color color, float alpha )
	{
		return new Color( color.r, color.g, color.b, alpha );
	}
}
