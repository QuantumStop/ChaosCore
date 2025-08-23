partial class GameSettings
{
	public struct Sensitivity
	{
		/// <summary>
		/// Camera Sensitiivity when using KBM
		/// </summary>
		[ConVar( "setting_sensitivity_mouse", Help = "Mouse sensitivity", Saved = true )]
		public static float Mouse { get; set; } = 1;

		/// <summary>
		/// Camera Sensitivity when using a controller
		/// </summary>
		[ConVar( "setting_sensitivity_controller", Help = "Controller sensitivity", Saved = true )]
		public static float Controller { get; set; } = 1;
	}

	public struct InvertCamera
	{
		/// <summary>
		/// Whether the mouse's pitch should be inverted or not
		/// </summary>
		[ConVar( "setting_mouse_y_inverted", Help = "Whether the mouse's pitch should be inverted or not", Saved = true, Min = 0, Max = 1 )]
		public static bool PitchMouse { get; set; } = false;

		/// <summary>
		/// Whether the mouse's yaw should be inverted or not
		/// </summary>
		[ConVar( "setting_mouse_x_inverted", Help = "Whether the mouse's yaw should be inverted or not", Saved = true, Min = 0, Max = 1 )]
		public static bool YawMouse { get; set; } = false;

		/// <summary>
		/// Whether the controller's pitch should be inverted or not
		/// </summary>
		[ConVar( "setting_controller_y_inverted", Help = "Whether the controller's pitch should be inverted or not", Saved = true, Min = 0, Max = 1 )]
		public static bool PitchController { get; set; } = false;

		/// <summary>
		/// Whether the mouse's yaw should be inverted or not
		/// </summary>
		[ConVar( "setting_controller_x_inverted", Help = "Whether the controller's yaw should be inverted or not", Saved = true, Min = 0, Max = 1 )]
		public static bool YawController { get; set; } = false;
	}

	/// <summary>
	/// False for hold, true for click (toggle)
	/// </summary>
	public struct CrouchMode
	{
		[ConVar( "setting_keyboard_crouch", Help = "Whether the crouch button should be held or clicked once", Saved = true, Min = 0, Max = 1 )]
		static public bool CrouchKeyboard { get; set; } = false;
		[ConVar( "setting_controller_crouch", Help = "Whether the crouch button should be held or clicked once", Saved = true, Min = 0, Max = 1 )]
		static public bool CrouchController { get; set; } = true;
	}

	/// <summary>
	/// False for hold, true for click (toggle)
	/// </summary>
	public struct SprintMode
	{
		[ConVar( "setting_keyboard_sprint", Help = "Whether the sprint button should be held or clicked once", Saved = true, Min = 0, Max = 1 )]
		static public bool SprintKeyboard { get; set; } = false;
		[ConVar( "setting_controller_sprint", Help = "Whether the sprint button should be held or clicked once", Saved = true, Min = 0, Max = 1 )]
		static public bool SprintController { get; set; } = true;
	}
}
