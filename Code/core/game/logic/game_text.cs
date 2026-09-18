using System;

namespace Core;

[PresetTarget( "game_text", Name = "Game Text", Icon = "text_fields", Category = "UI", IncludeAll = true )]

/// <summary>
/// An entity that displays text on player's screens.
/// </summary>
[Icon( "format_align_justify" ), Category( "Logic" ), Title( "game_text" )]

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

	public enum TextChannel
	{
		[Title( "Channel 1" )] Channel1 = 1,
		[Title( "Channel 2" )] Channel2 = 2,
		[Title( "Channel 3" )] Channel3 = 3,
		[Title( "Channel 4" )] Channel4 = 4
	}

	public enum TextRecipient
	{
		[Title( "Everyone" )] Everyone,
		[Title( "Specific Player" )] Specific,
		[Title( "Activator" )] Activator
	}

	/// <summary>
	/// Message to display onscreen. \n signifies a new line in the text.
	/// </summary>
	[Property, TextArea] public TextRendering.Scope MessageText { get; set; }

    [Property, PresetSelector] public GenericPresetResource Preset { get; set; }	
	
	[Space( 15 )]	

	[Property, Title( "Effect" )] public TextEffects CurrentEFfect { get; set; } = TextEffects.TEXTEFFECT_FADEINOUT;
	[Property] public TextAlignmentFlag TextAlignment { get; set; }
	[Property] public TextChannel Channel { get; set; } = TextChannel.Channel1;
	[Property, Title( "Z-Index" )] public int ZIndex { get; set; } = 1;
	[Property] public TextRecipient Recipient { get; set; } = TextRecipient.Everyone;
	[Property, ShowIf( nameof( Recipient ), TextRecipient.Specific )] public BasePlayer SpecificPlayer { get; set; }

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
	[Property, Group( "Scan" ), ShowIf( nameof( CurrentEFfect ), TextEffects.TEXTEFFECT_SCANOUT )] public float ScanTime { get; set; } = 1.0f;

	/// <summary>
	/// How long the scan color should stay pronounced on each revealed letter.
	/// </summary>
	[Property, Group( "Scan" ), ShowIf( nameof( CurrentEFfect ), TextEffects.TEXTEFFECT_SCANOUT )] public float ScanFlashTime { get; set; } = 0.35f;

	/// <summary>
	/// How long after each letter starts revealing before its scan color begins fading back to the text color.
	/// </summary>
	[Property, Group( "Scan" ), ShowIf( nameof( CurrentEFfect ), TextEffects.TEXTEFFECT_SCANOUT )] public float ScanFlashDelay { get; set; } = 0.1f;

	/// <summary>
	/// The scanning color for the letter being scanned if the Text Effect keyvalue is set to Scan Out—usually a different shade of primary color.
	/// </summary>	
	[Property, Group( "Scan" ), ShowIf( nameof( CurrentEFfect ), TextEffects.TEXTEFFECT_SCANOUT )] public Color ScanColorFX { get; set; } = Color.White;

	/// <summary>
	/// Creates a new vanity channel based on this component's properties.
	/// </summary>
	public VanityChannel BuildChannel()
	{
		return new VanityChannel
		{
			Id = $"Vanity_GameText_{(int)Channel}",
			Text = MessageText.Text,
			TextScope = MessageText,
			PosX = PosX,
			PosY = PosY,
			ZIndex = ZIndex,
			ScanColor = ScanColorFX,
			FadeInTime = FadeInTime,
			HoldTime = HoldTime,
			FadeOutTime = FadeOutTime,
			ScanTime = ScanTime,
			ScanFlashTime = ScanFlashTime,
			ScanFlashDelay = ScanFlashDelay,
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
		if ( !TryGetTargetSteamId( activator, out var targetSteamId ) )
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

	private void DisplayLocal()
	{
		VanityChannel channel = BuildChannel();

		BasePlayer.Local?
			.HUDGameObject?
			.GetComponent<VanityUI>()?
			.UpdateChannel( channel.Id, channel );
	}

	private bool TryGetTargetSteamId( BaseEntity activator, out ulong steamId )
	{
		steamId = 0;

		return Recipient switch
		{
			TextRecipient.Everyone => true,
			TextRecipient.Specific => TryGetPlayerSteamId( SpecificPlayer, out steamId ),
			TextRecipient.Activator => TryGetActivatorSteamId( activator, out steamId ),
			_ => true
		};
	}

	private static bool TryGetActivatorSteamId( BaseEntity activator, out ulong steamId )
	{
		steamId = 0;

		if ( !activator.IsValid() )
			return false;

		if ( activator is BasePlayer player )
			return TryGetPlayerSteamId( player, out steamId );

		if ( activator is BasePawn pawn && pawn.PawnCamera.IsValid() )
			return TryGetPawnSteamId( pawn, out steamId );

		var activatorPlayer = activator.GameObject.GetComponentInParent<BasePlayer>();
		if ( activatorPlayer.IsValid() )
			return TryGetPlayerSteamId( activatorPlayer, out steamId );

		var activatorPawn = activator.GameObject.GetComponentInParent<BasePawn>();
		if ( activatorPawn.IsValid() && activatorPawn.PawnCamera.IsValid() )
			return TryGetPawnSteamId( activatorPawn, out steamId );

		return false;
	}

	private static bool TryGetPlayerSteamId( BasePlayer player, out ulong steamId )
	{
		steamId = 0;

		if ( !player.IsValid() )
			return false;

		return TryGetPawnSteamId( player, out steamId );
	}

	private static bool TryGetPawnSteamId( BasePawn pawn, out ulong steamId )
	{
		steamId = 0;

		if ( !pawn.IsValid() )
			return false;

		if ( pawn.Owner.IsValid() && pawn.Owner.Connection is not null )
		{
			steamId = pawn.Owner.Connection.SteamId;
			return true;
		}

		if ( pawn.SteamId != 0 )
		{
			steamId = pawn.SteamId;
			return true;
		}

		if ( pawn.IsControlledLocally && Connection.Local is not null )
		{
			steamId = Connection.Local.SteamId;
			return true;
		}

		return false;
	}

}
