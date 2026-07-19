/// <summary>
/// The REAL settings class because fuckers can't have shit thats not ass
/// </summary>
public static partial class GameSettings
{
	/// <summary>
	/// The player's (Vertical) Field of View. Horizontal means you will always see the same amount vertically on all aspect ratios, with top and bottom cropping your view. The opposite happens in CS2.
	/// </summary>
	[ConVar( "fov", Help = "Default field of view", Min = 60.0f, Max = 120.0f, Saved = true )]
	public static float FieldOfView { get; set; } = 105.0f;

	[ConVar( "hud_deadzone_x", Help = "Horizontal padding of the HUD's canvas", Min = 0.0f, Max = 1.0f, Saved = true )]
	public static float HorizontalHUDPadding { get; set; } = 0.2f;

	[ConVar( "hud_deadzone_y", Help = "Vertical padding of the HUD's canvas", Min = 0.0f, Max = 1.0f, Saved = true )]
	public static float VerticalHUDPadding { get; set; } = 0.4f;
}
