namespace Core;

public partial class VanityChannel
{
	// Stable slot, we rely on this to know where this channel
	// should be and where it can't during production
	public VanitySlot Slot { get; set; }

	// What kind of content is this channel
	public VanityContent Content { get; set; }

	// Positional and sizing relative to Viewport (used for overlays)
	public float PosX { get; set; } = 0.5f;
	public float PosY { get; set; } = 0.5f;

	public int ZIndex { get; set; }

	// Timed visibility tracking
	public float StartTime { get; set; }

	public bool IsDrawPermanent { get; set; }

	// Effect
	public VanityEffectData Effect { get; set; }

	// Text
	public TextRendering.Scope TextScope { get; set; }

	// Cached data: Avoid rebuilding the same text all the time
	public string[] Lines { get; set; } = [];
	public int TotalCharacterCount { get; set; }

	public Sandbox.UI.TextAlign Alignment { get; set; }

	// Core styling
	public Color BackgroundColor { get; set; } = Color.Transparent;

	public Texture Texture { get; set; }

	// For anything custom
	public string CustomType { get; set; }
}

