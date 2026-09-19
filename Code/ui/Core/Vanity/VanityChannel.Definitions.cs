namespace Core;

public enum VanityContent
{
	[Icon( "text_fields" )] Text,
	[Icon( "palette" )] Color,
	[Icon( "image" )] Texture,
	[Icon( "extension" )] Custom
}

public enum VanitySlot
{
	[Title( "Channel 1" )] Channel1 = 1,
	[Title( "Channel 2" )] Channel2 = 2,
	[Title( "Channel 3" )] Channel3 = 3,
	[Title( "Channel 4" )] Channel4 = 4
}

public enum VanityEffectType
{
	[Icon( "block" )] None = 0,

	// Visibility
	[Icon( "gradient" )] Fade = 1,
	[Icon( "visibility" )] Blink = 2,
	[Icon( "flash_on" )] Flicker = 3,
	[Icon( "graphic_eq" )] Pulse = 4,
	[Icon( "view_array" )] Wipe = 10,
	[Icon( "scanner" )] ScanOut = 11,
	[Icon( "keyboard" )] Typewriter = 12,
	[Icon( "blur_on" )] Dissolve = 13,

	// Transform
	[Icon( "swipe" )] Slide = 20,
	[Icon( "zoom_out_map" )] Scale = 21,
	[Icon( "rotate_right" )] Rotate = 22,
	[Icon( "vibration" )] Shake = 23,
	[Icon( "waves" )] Wobble = 24,

	// Distortion
	[Icon( "broken_image" )] Glitch = 30,
	[Icon( "water" )] Ripple = 31,
	[Icon( "waves" )] Wave = 32,
	[Icon( "blur_on" )] Blur = 33,
	[Icon( "grid_on" )] Pixelate = 34,

	// Color
	[Icon( "palette" )] ColorShift = 40,
	[Icon( "flare" )] Glow = 41,
	[Icon( "flash_on" )] Flash = 42,

	// Texture
	[Icon( "swap_horiz" )] TextureScroll = 50,
	[Icon( "zoom_in" )] TextureZoom = 51,
	[Icon( "rotate_right" )] TextureRotate = 52,
	[Icon( "transform" )] TextureDistort = 53,
	[Icon( "compare" )] CrossFade = 54
}

public enum VanityDirection
{
	[Icon( "block" )] None,
	[Icon( "arrow_back" )] Left,
	[Icon( "arrow_forward" )] Right,
	[Icon( "arrow_upward" )] Up,
	[Icon( "arrow_downward" )] Down,
	[Icon( "login" )] In,
	[Icon( "logout" )] Out,
	[Icon( "sync_alt" )] InOut
}

public enum VanityLoopMode
{
	[Icon( "block" )] None,
	[Icon( "looks_one" )] Once,
	[Icon( "loop" )] Loop,
	[Icon( "swap_horiz" )] PingPong
}

public enum VanityBlendMode
{
	[Icon( "layers" )] Normal,
	[Icon( "add_circle" )] Additive,
	[Icon( "close" )] Multiply,
	[Icon( "filter_none" )] Screen
}

public enum VanityAxis
{
	[Icon( "block" )] None,
	[Icon( "swap_horiz" )] X,
	[Icon( "swap_vert" )] Y,
	[Icon( "open_with" )] XY
}

public enum VanityRecipient
{
	[Title( "Everyone" ), Icon( "groups" )]
	Everyone,

	[Title( "Specific Player" ), Icon( "person" )]
	Specific,

	[Title( "Activator" ), Icon( "touch_app" )]
	Activator
}
