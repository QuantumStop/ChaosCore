using Sandbox.Rendering;
using System;
namespace Core;

[Icon( "format_align_justify" )]
[Description( "An entity that displays text on player's screens." )]
public class ui_text : BaseEntity
{
	public enum TextEffects
	{
		[Title( "Text Effect: Fade In/Out" )] TEXTEFFECT_FADEINOUT,
		[Title( "Text Effect: Scanout" )] TEXTEFFECT_SCANOUT
	}

	public enum TextAlignmentFlag
	{
		[Title( "Text Left" )] TEXTALIGNMENT_LEFT,
		[Title( "Text Center" )] TEXTALIGNMENT_CENTER,
		[Title( "Text Right" )] TEXTALIGNMENT_RIGHT
	}


	/// <summary>
	/// Message to display onscreen. \n signifies a new line in the text.
	/// </summary>
	[Property, TextArea] public TextRendering.Scope MessageText { get; set; }


	/// <summary>
	/// Horizontal position on the player's screens to draw the text. The value should be between 0 and 1,
	/// where 0 is the far left of the screen and 1 is the far right.
	/// </summary>
	[Property, Range( 0, 1, clamped: true )] public float PosX { get; set; } = 0.5f;


	/// <summary>
	/// Vertical position on the player's screens to draw the text. The value should be between 0 and 1, 
	/// where 0 is the top of the screen and 1 is the bottom.
	/// </summary>
	[Property, Range( 0, 1, clamped: true )] public float PosY { get; set; } = 0.5f;


	[Property] public TextEffects CurrentEFfect { get; set; } = TextEffects.TEXTEFFECT_FADEINOUT;
	[Property, MakeDirty] public TextAlignmentFlag TextAlignment { get; set; }


	/// <summary>
	/// Font scale factor for the text. This is a multiplier applied to the base font size.
	/// For example: 0.05 means 5% of the screen size.
	/// </summary>
	[Property, Range( 0.0f, 1.0f )] public float FontSize { get; set; } = 0.05f;


	/// <summary>
	/// Text padding on the screen. This is a percentage of the smaller axis of the screen.
	/// For example: 0.015 means 1.5% of the smaller axis
	/// </summary>
	[Property, Range( 0.0f, 1.0f )] public float TextPadding { get; set; } = 0.015f;


	/// <summary>
	/// The time it should take for the text to fully fade in.
	/// </summary>
	[Property] public float FadeInTime { get; set; } = 0.5f;

	/// <summary>
	/// The time it should take for the text to fade out, after the hold time has expired.
	/// </summary>
	[Property] public float FadeOutTime { get; set; } = 0.5f;

	/// <summary>
	/// The time the text should stay onscreen, after fading in, before it begins to fade out.
	/// </summary>
	[Property] public float HoldTime { get; set; } = 2.0f;

	/// <summary>
	/// If the 'Text Effect' is set to Scan Out, this is the time it should take to scan out all the letters in the text.
	/// </summary>	
	[Property] public float ScanTime { get; set; } = 1.0f;

	/// <summary>
	/// The scanning color for the letter being scanned if the Text Effect keyvalue is set to Scan Out—usually a different shade of primary color.
	/// </summary>	
	[Property] public Color ScanColorFX { get; set; }
	
	/// <summary>
	/// Z-Index order, can be used to layer things nicely! I.e: Need to have text be readable on top screen fade, or not.
	/// </summary>
	[Property, Title( "Z-Index" )] public int ZIndex { get; set; } = 1;

	/// <summary>
	/// Creates a new vanity channel based on this component's properties.
	/// </summary>
	public VanityChannel BuildChannel()
	{
		return new VanityChannel
		{
			Id = $"Vanity_{TargetName}",
			Text = MessageText.Text,
			PosX = PosX,
			PosY = PosY,
			FontSize = FontSize,
			TextColor = MessageText.TextColor,
			ZIndex = ZIndex,
			ScanColor = ScanColorFX,
			FadeInTime = FadeInTime,
			HoldTime = HoldTime,
			FadeOutTime = FadeOutTime,
			ScanTime = ScanTime,
			Effect = CurrentEFfect switch
			{
				TextEffects.TEXTEFFECT_SCANOUT => "scanout",
				_ => "fadeinout"
			},
			Alignment = TextAlignment.ToString()
		};
	}

	/// <summary>
	/// Display the message text.
	/// </summary>
	public BaseEntity Display( BaseEntity activator = null )
	{
		VanityChannel channel = BuildChannel();

		BasePlayer.Local?
			.HUDGameObject?
			.GetComponent<VanityUI>()?
			.UpdateChannel( channel.Id, channel );

		return activator;
	}

}
