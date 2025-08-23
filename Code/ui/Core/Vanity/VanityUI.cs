using System;
using System.Globalization;
using System.Text.RegularExpressions;

public class VanityChannel
{
	public string Id { get; set; }

	// Textual content (optional depending on effect)
	public string Text { get; set; }

	// Positional and sizing information (used for overlays)
	public float PosX { get; set; } = 0.5f;
	public float PosY { get; set; } = 0.5f;
	public float FontSize { get; set; } = 1.0f;

	// Timing controls
	public float FadeInTime { get; set; } = 0.5f;
	public float HoldTime { get; set; } = 1.0f;
	public float FadeOutTime { get; set; } = 0.5f;
	public float ScanTime { get; set; } = 1.0f;
	public bool FadeFrom { get; set; } // Fade Out/In

	// Core styling
	public Color TextColor { get; set; } = Color.White;
	public Color ScanColor { get; set; } = Color.White;
	public Color BackgroundColor { get; set; } = Color.Transparent;

	public int ZIndex { get; set; } = 0;
	public string FontFamily { get; set; } = "GorDIN";
	public bool Bold { get; set; } = false;
	public bool Italic { get; set; } = false;
	public string Alignment { get; set; } = "TEXTALIGNMENT_LEFT";

	// Effect specifier
	public string Effect { get; set; } = "none"; // e.g., "fade", "scanout", "image", "shader"

	// Optional texture reference
	public Texture Texture { get; set; } = null;

	// Timed visibility tracking
	public float StartTime { get; set; }
	public float VisibilityStartTime { get; set; } = -1f;

	public bool IsTextual => !string.IsNullOrWhiteSpace( Text );
	public bool IsVisualAsset => Texture != null;

	public bool IsDrawPermanent { get; set; } = false;
}

public struct VanityChar
{
	public char Char;
	public string Style;

	public VanityChar( char c, string style )
	{
		Char = c;
		Style = style;
	}
}


public class VanityUI : PanelComponent
{
	public static VanityUI Local;

	[Property, ReadOnly, MakeDirty] public Dictionary<string, VanityChannel> ActiveChannels { get; set; } = new();

	[Property] public float TimeElapsed { get; set; }

	private const float TextPadding = 0.1f;

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
			"scanout" => channel.ScanTime,
			"fadeinout" => channel.FadeInTime,
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

		// Only refresh UI when something is visible
		if ( ActiveChannels.Values.Any( c => Time.Now >= c.VisibilityStartTime ) )
		{
			StateHasChanged();
		}
	}

	public string GetRootStyle( VanityChannel channel )
	{
		if ( channel is null )
			return "";

		// Handle regular text effects
		Vector2 screenPos = GetScreenPosition( channel.PosX, channel.PosY );
		string alignmentTransform = channel.Alignment switch
		{
			"TEXTALIGNMENT_CENTER" => "translate(-50%, -50%)",
			"TEXTALIGNMENT_RIGHT" => "translate(-100%, -50%)",
			_ => "translate(0%, -50%)"
		};

		// Handle base root styling
		if ( channel.Effect == "fade" )
		{
			return $"position: absolute; " +
				   $"top: 0; left: 0; width: 100%; height: 100%;" +
				   $"z-index: {channel.ZIndex};";
				   		
		}
		else
		{
			return $"position: absolute; " +
				   $"left: {screenPos.x}px; top: {screenPos.y}px; " +
				   $"z-index: {channel.ZIndex};";
		}

	}

	public string GetTextBlockStyle( VanityChannel ch )
	{

		return
			$"font-size:{ch.FontSize * 10}vh;" +
			$"font-color:{ch.TextColor}px;" +
			$"font-family:{ch.FontFamily};" +
			$"font-weight:{(ch.Bold ? "bold" : "normal")};" +
			$"font-style:{(ch.Italic ? "italic" : "normal")};" +
			$"white-space: pre;" +
			$"display: flex;" +
			$"flex-direction: column;";
	}

	private Vector2 GetScreenPosition( float normalizedX, float normalizedY )
	{
		float screenWidth = Screen.Width;
		float screenHeight = Screen.Height;
		float minAxis = MathF.Min( screenWidth, screenHeight );
		float padPixels = TextPadding * minAxis;
		float padX = padPixels / screenWidth;
		float padY = padPixels / screenHeight;

		normalizedX = MathX.Lerp( padX, 1f - padX, normalizedX );
		normalizedY = MathX.Lerp( padY, 1f - padY, normalizedY );

		return new Vector2( normalizedX * screenWidth, normalizedY * screenHeight );
	}

	private static readonly Regex LocalizationTokenRegex = new( @"#([\w\.]+)", RegexOptions.Compiled );

	public string GetResolvedText( string rawText )
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
