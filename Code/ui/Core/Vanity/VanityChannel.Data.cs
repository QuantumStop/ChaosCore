namespace Core;

public abstract class VanityEffectData
{
	public abstract VanityEffectType Type { get; }

	// Generic effect modifiers
	public VanityDirection Direction { get; set; }
	public VanityLoopMode LoopMode { get; set; }
	public VanityBlendMode BlendMode { get; set; }
	public VanityAxis Axis { get; set; }

	// Generic controls, expected to be mutated from
	public float Intensity { get; set; } = 1.0f;
	public float Speed { get; set; } = 1.0f;
	public float Amount { get; set; } = 1.0f;
	public float Frequency { get; set; } = 1.0f;
}

public sealed class VanityFadeEffect : VanityEffectData
{
	public override VanityEffectType Type => VanityEffectType.Fade;

	public float FadeInTime { get; set; } = 0.5f;
	public float HoldTime { get; set; } = 1.0f;
	public float FadeOutTime { get; set; } = 0.5f;
}

public sealed class VanityScanOutEffect : VanityEffectData
{
	public override VanityEffectType Type => VanityEffectType.ScanOut;

	public float ScanTime { get; set; } = 1.0f;
	public float HoldTime { get; set; } = 1.0f;

	public float FlashTime { get; set; } = 0.35f;
	public float FlashDelay { get; set; } = 0.1f;
	public Color FlashColor { get; set; } = Color.White;

	public float FadeOutTime { get; set; } = 0.5f;
}
