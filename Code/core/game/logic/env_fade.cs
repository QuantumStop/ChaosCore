namespace Core;

/// <summary>
/// An entity that fades out/in player's view.
/// </summary>
[Icon( "format_align_justify" ), Category( "Logic" ), Title( "env_fade" )]
public class ui_fade : BaseEntity
{
	public new delegate void ChaosOutput( ui_fade activator );

	/// <summary>
	/// The time that it will take to fade the screen in or out.
	/// </summary>
	[Property] public float Duration { get; set; }

	/// <summary>
	/// The time to hold the faded in/out state.
	/// </summary>
	[Property] public float HoldFade { get; set; }

	/// <summary>
	/// Fade color, this also includes alpha of the fade.
	/// </summary>
	[Property] public Color FadeColor { get; set; } = Color.Black;

	/// <summary>
	/// Z-Index order, can be used to layer things nicely! I.e: Need to have text be readable on top screen fade, or not.
	/// </summary>
	[Property, Title( "Z-Index" )] public int ZIndex { get; set; } = 1;

	[Property] public VanitySlot Channel { get; set; }

	[Property] public VanityRecipient Recipient { get; set; } = VanityRecipient.Everyone;

	[Property, ShowIf( nameof( Recipient ), VanityRecipient.Specific )]
	public BasePlayer SpecificPlayer { get; set; }

	/// <summary>
	/// Fired when the fade has begun.
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnBeginFade { get; set; }

	/// <summary>
	/// Screen fades from the specified color instead of to it.
	/// </summary>
	[Group( "SpawnFlags" ), Property, Order( 2 )]
	public bool FadeFrom { get; set; } = false;

	/// <summary>
	/// Fade remains indefinitely until another fade deactivates it.
	/// </summary>
	[Group( "SpawnFlags" ), Property, Order( 2 )]
	public bool StayOut { get; set; } = false;

	/// <summary>
	/// Creates a new vanity channel based on this component's properties.
	/// </summary>
	public VanityChannel BuildChannel()
	{
		return new VanityChannel
		{
			Slot = Channel,
			Content = VanityContent.Color,

			Effect = new VanityFadeEffect
			{
				// FadeFrom means we're fading from the solid color back out.
				// StayOut means fade into the color and remain there.
				// Otherwise fade in, hold, then fade back out.
				Direction = FadeFrom ? VanityDirection.Out : StayOut
					? VanityDirection.In
					: VanityDirection.InOut,

				FadeInTime = FadeFrom ? 0f : Duration,
				HoldTime = HoldFade,
				FadeOutTime = FadeFrom || !StayOut ? Duration : 0f
			},

			BackgroundColor = FadeColor,
			IsDrawPermanent = StayOut,
			ZIndex = ZIndex
		};
	}

	/// <summary>
	/// Start the screen fade.
	/// </summary>
	public BaseEntity Fade( BaseEntity activator = null )
	{
		if ( !VanityAPI.TryGetTargetSteamId(
			Recipient,
			SpecificPlayer,
			activator,
			out var targetSteamId
		) )
		{
			return activator;
		}

		if ( Networking.IsHost )
		{
			FadeRpc( targetSteamId );
		}
		else
		{
			if ( targetSteamId != 0 && Connection.Local.SteamId != targetSteamId )
				return activator;

			FadeLocal();
		}

		OnBeginFade?.Invoke( this );

		return activator;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void FadeRpc( ulong targetSteamId )
	{
		if ( targetSteamId != 0 && Connection.Local.SteamId != targetSteamId )
			return;

		FadeLocal();
	}

	private void FadeLocal() => VanityAPI.Show( BuildChannel() );


	/// <summary>
	/// Start the screen fade in the opposite direction.
	/// </summary>
	public BaseEntity FadeReverse( BaseEntity activator = null )
	{
		if ( !VanityAPI.TryGetTargetSteamId(
			Recipient,
			SpecificPlayer,
			activator,
			out var targetSteamId
		) )
		{
			return activator;
		}

		if ( Networking.IsHost )
		{
			FadeReverseRpc( targetSteamId );
		}
		else
		{
			if ( targetSteamId != 0 && Connection.Local.SteamId != targetSteamId )
				return activator;

			FadeReverseLocal();
		}

		OnBeginFade?.Invoke( this );

		return activator;
	}

	[Rpc.Broadcast( NetFlags.HostOnly )]
	private void FadeReverseRpc( ulong targetSteamId )
	{
		if ( targetSteamId != 0 && Connection.Local.SteamId != targetSteamId )
			return;

		FadeReverseLocal();
	}

	private void FadeReverseLocal()
	{
		VanityChannel channel = BuildChannel();

		if ( channel.Effect is not VanityFadeEffect fade )
			return;

		fade.Direction = fade.Direction switch
		{
			VanityDirection.In => VanityDirection.Out,
			VanityDirection.Out => VanityDirection.In,
			_ => fade.Direction
		};

		(fade.FadeInTime, fade.FadeOutTime) =
			(fade.FadeOutTime, fade.FadeInTime);

		channel.IsDrawPermanent = false;

		VanityAPI.Show( channel );
	}
}
