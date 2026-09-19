using System;

namespace Core;

[PresetTarget( "game_text", Name = "Game Text", Icon = "text_fields", Category = "UI", IncludeAll = true )]

/// <summary>
/// An entity that displays text on player's screens.
/// </summary>
[Icon( "format_align_justify" ), Category( "Logic" ), Title( "game_text" )]
public class ui_text : BaseEntity
{
	/// <summary>
	/// Message to display onscreen. \n signifies a new line in the text.
	/// </summary>
	[Property, TextArea] public TextRendering.Scope MessageText { get; set; }

	[Property, PresetSelector] public GenericPresetResource Preset { get; set; }

	[Space( 15 )]

	[Property, Title( "Effect" ), EnumOptions( VanityEffectType.Fade, VanityEffectType.ScanOut )] 
	public VanityEffectType Effect { get; set; } = VanityEffectType.Fade;

	[Property] public Sandbox.UI.TextAlign TextAlignment { get; set; }
	[Property, Title( "Z-Index" )] public int ZIndex { get; set; } = 1;
	[Property] public VanitySlot Channel { get; set; }
	[Property] public VanityRecipient Recipient { get; set; } = VanityRecipient.Everyone;
	[Property, ShowIf( nameof( Recipient ), VanityRecipient.Specific )] public BasePlayer SpecificPlayer { get; set; }

	[Space( 15 )]

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

	[Space( 10 )]

	/// <summary>
	/// The time it should take for the text to fully fade in.
	/// </summary>
	[Property, Group( "Timing" )] public float FadeInTime { get; set; } = 0.5f;

	/// <summary>
	/// The time it should take for the text to fade out, after the hold time has expired.
	/// </summary>
	[Property, Group( "Timing" )] public float FadeOutTime { get; set; } = 0.5f;

	/// <summary>
	/// The time the text should stay onscreen, after fading in, before it begins to fade out.
	/// </summary>
	[Property, Group( "Timing" )] public float HoldTime { get; set; } = 2.0f;

	/// <summary>
	/// If the 'Text Effect' is set to Scan Out, this is the time it should take to scan out all the letters in the text.
	/// </summary>	
	[Property, Group( "Scan" ), ShowIf( nameof( Effect ), VanityEffectType.ScanOut )] public float ScanTime { get; set; } = 1.0f;

	/// <summary>
	/// How long the scan color should stay pronounced on each revealed letter.
	/// </summary>
	[Property, Group( "Scan" ), ShowIf( nameof( Effect ), VanityEffectType.ScanOut )] public float ScanFlashTime { get; set; } = 0.35f;

	/// <summary>
	/// How long after each letter starts revealing before its scan color begins fading back to the text color.
	/// </summary>
	[Property, Group( "Scan" ), ShowIf( nameof( Effect ), VanityEffectType.ScanOut )] public float ScanFlashDelay { get; set; } = 0.1f;

	/// <summary>
	/// The scanning color for the letter being scanned if the Text Effect keyvalue is set to Scan Out—usually a different shade of primary color.
	/// </summary>	
	[Property, Group( "Scan" ), ShowIf( nameof( Effect ), VanityEffectType.ScanOut )] public Color ScanColorFX { get; set; } = Color.White;

	private VanityEffectData _effectChoice => Effect switch
	{
		VanityEffectType.ScanOut => new VanityScanOutEffect
		{
			ScanTime = ScanTime,
			HoldTime = HoldTime,
			FlashTime = ScanFlashTime,
			FlashDelay = ScanFlashDelay,
			FlashColor = ScanColorFX,
			FadeOutTime = FadeOutTime
		},

		_ => new VanityFadeEffect
		{
			Direction = VanityDirection.InOut,
			FadeInTime = FadeInTime,
			HoldTime = HoldTime,
			FadeOutTime = FadeOutTime
		}
	};

	public VanityChannel BuildChannel()
	{
		return new VanityChannel
		{
			Slot = Channel,
			Content = VanityContent.Text,
			TextScope = MessageText,
			PosX = PosX,
			PosY = PosY,
			ZIndex = ZIndex,
			Effect = _effectChoice,
			Alignment = TextAlignment
		};
	}

	/// <summary>
	/// Display the message text.
	/// </summary>
	public BaseEntity Display( BaseEntity activator = null )
	{
		if ( !VanityAPI.TryGetTargetSteamId( Recipient, SpecificPlayer, activator, out var targetSteamId ) )
			return activator;

		if ( Networking.IsHost )
		{
			DisplayRpc( targetSteamId );
		}
		else
		{
			if ( targetSteamId != 0 && Connection.Local.SteamId != targetSteamId )
				return activator;

			DisplayLocal();
		}

		return activator;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void DisplayRpc( ulong targetSteamId )
	{
		if ( targetSteamId != 0 && Connection.Local.SteamId != targetSteamId )
			return;

		DisplayLocal();
	}

	private void DisplayLocal() => VanityAPI.Show( BuildChannel() );

}
