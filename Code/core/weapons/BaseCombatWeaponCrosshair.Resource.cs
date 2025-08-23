[GameResource( "Crosshair Data", "crs", "Crosshair configuration for weapons", Icon = "center_focus_strong", IconFgColor = "#ffffff", IconBgColor = "#007acc" )]
public class CrosshairData : GameResource
{
	[Order( 0 )] public WeaponCrosshairType WeaponCrosshairType { get; set; }

	[Category( "Crosshair" ), Range( 0.1f, 1 ), Step( 0.01f )] public float CrosshairScale { get; set; } = 0.5f;
	[Category( "Crosshair" ), Range( 1, 128 ), Step( 1 )] public float CrosshairWidth { get; set; } = 5;
	[Category( "Crosshair" ), ShowIf( nameof( CrosshairGeneric ), true ), Range( 4, 128 ), Step( 1 )] public float CrosshairGapX { get; set; } = 15;
	[Category( "Crosshair" ), ShowIf( nameof( CrosshairGeneric ), true ), Range( 4, 128 ), Step( 1 )] public float CrosshairGapY { get; set; } = 15;

	[Category( "Crosshair" ), ShowIf( nameof( CrosshairCircle ), true ), Range( 1, 5 ), Step( 1 ), Title( "Segment" ), WideMode] public List<CrosshairCircleSegment> CrosshairCircleSegment { get; set; }

	[Category( "Crosshair" ), ShowIf( nameof( CrosshairGeneric ), true ), Range( 1, 500 ), Step( 0.01f )] public float CrosshairLength { get; set; } = 18;
	[Category( "Crosshair" ), Range( 0, 64 ), Step( 1 )] public float CrosshairRecoilScale { get; set; } = 15;
	[Category( "Crosshair" )] public Color CrosshairColor { get; set; } = Color.White.WithAlpha( 1 );


	[Space( 10 )]
	[Header( "Crosshair: Dot" )]
	[Category( "Crosshair" ), ShowIf( nameof( CrosshairCircleDot ), false )] public bool CrosshairHasDot { get; set; } = false;
	[Category( "Crosshair" ), ShowIf( nameof( CrosshairCircleDot ), false )] public bool CrosshairDotOutline { get; set; } = false;
	[Category( "Crosshair" ), ShowIf( nameof( CrosshairCircleDot ), false )] public Color CrosshairDotColor { get; set; } = Color.White.WithAlpha( 1 );


	[Space( 10 )]
	[Header( "Crosshair: Outline" )]
	[Category( "Crosshair" )] public bool CrosshairHasOutline { get; set; } = true;
	[Category( "Crosshair" ), Range( 1, 20 ), Step( 1 ), Title( "Outline Thickness" )] public float CrosshairOutlineThickness { get; set; } = 10;
	[Category( "Crosshair" ), Range( 0, 50 ), Step( 1 ), Title( "Outline Color" )] public Color CrosshairOutlineColor { get; set; } = Color.Black.WithAlpha( 1 );


	[Hide] private bool CrosshairGeneric => WeaponCrosshairType == WeaponCrosshairType.CROSSHAIR_CROSS_A;

	[Hide] private bool CrosshairCircleDot => WeaponCrosshairType == WeaponCrosshairType.CROSSHAIR_DOT;

	[Hide] private bool CrosshairCircle => WeaponCrosshairType == WeaponCrosshairType.CROSSHAIR_CIRCLE;
}

public class CrosshairCircleSegment // In the future want to handle a complex dynamic crosshair like this and just store all this there
{
	[Title( "Circle: Thickness" ), Range( 1, 32 ), Step( 1 )]
	public float CrosshairCircleThickness { get; set; } = 2f;

	[Title( "Circle: Segments" ), Range( 4, 160 ), Step( 8 )]
	public int CrosshairCircleSegments { get; set; } = 64;

	[Title( "Circle: Position Offset" ), Range( 0, 64 ), Step( 1 )]
	public float CrosshairCirclePosition { get; set; } = 0f;

	[Title( "Circle: Base Color" )]
	public Color CrosshairCircleColor { get; set; } = Color.White.WithAlpha( 1f );

	// --- Optional Enhancements ---

	[Category( "Crosshair: Animation" ), Title( "Animate Angle Offset Speed" ), Description( "Rotates the circle over time. 0 = no animation." )]
	public float AnimationSpeed { get; set; } = 0f;

	[Category( "Crosshair: Visuals" ), Title( "Alpha Fade Multiplier" ), Range( 0f, 1f ), Step( 0.01f ), Description( "Fades this ring's opacity by multiplying alpha. 1 = full opacity." )]
	public float AlphaFade { get; set; } = 1f;

	[Category( "Crosshair: Arc" ), Title( "Arc Start Angle (Degrees)" ), Range( 0f, 360f ), Step( 1 )]
	public float StartAngle { get; set; } = 360f;

	[Category( "Crosshair: Arc" ), Title( "Arc End Angle (Degrees)" ), Range( 0f, 360f ), Step( 1 )]
	public float EndAngle { get; set; } = 0f;

	[Category( "Crosshair: Visuals" ), Title( "Gradient End Color" ), Description( "Optional. If set, the color will fade to this across the arc." )]
	public Color? GradientToColor { get; set; } = null;
}
