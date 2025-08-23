[Icon( "format_align_justify" )]

[Description( "An entity that fades out/in player's view." )]
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


	/// <summary>
	/// Fired when the fade has begun.
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnBeginFade { get; set; }


	/// <summary>
	/// Screen fades from the specified color instead of to it.
	/// </summary>
	[Group( "SpawnFlags" ), Property, Order( 2 )] public bool FadeFrom { get; set; } = false;


	/// <summary>
	/// Fade remains indefinitely until another fade deactivates it.
	/// </summary>
	[Group( "SpawnFlags" ), Property, Order( 2 )] public bool StayOut { get; set; } = false;


	/// <summary>
	/// Creates a new vanity channel based on this component's properties.
	/// </summary>
	public VanityChannel BuildChannel()
	{
		return new VanityChannel
		{
			Id = $"Vanity_{TargetName}",
			Effect          = "fade",
			BackgroundColor = FadeColor,

			// Fade-in vs fade-out direction
			FadeInTime  = FadeFrom ? 0f : Duration,
			HoldTime    = StayOut ? 99999f : HoldFade, // long hold for permanent fade
			FadeOutTime = FadeFrom ? Duration : (StayOut ? 0f : Duration),
			IsDrawPermanent = StayOut,
			FadeFrom = FadeFrom,
			ZIndex = ZIndex
		};
	}


	/// <summary>
	/// Start the screen fade.
	/// </summary>
	public BaseEntity Fade( BaseEntity activator = null )
	{
		VanityChannel channel = BuildChannel();

		BasePlayer.Local?
			.HUDGameObject?
			.GetComponent<VanityUI>()?
			.UpdateChannel( channel.Id, channel );

		return activator ?? null;
	}


	/// <summary>
	/// Start the screen fade.
	/// </summary>
	public BaseEntity FadeReverse( BaseEntity activator = null )
	{
		VanityChannel channel = BuildChannel();

		BasePlayer.Local?
			.HUDGameObject?
			.GetComponent<VanityUI>()?
			.UpdateChannel( channel.Id, channel );

		return null;
	}


}
