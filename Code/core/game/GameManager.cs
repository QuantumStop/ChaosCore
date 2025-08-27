namespace Core;

using System;
using System.Dynamic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

/// The GameObjectSystem to automatically create everything is made per-game, check chaoscore for example

[Title( "Game Manager" )]
public partial class GameManager : BaseEntity, Component.ExecuteInEditor, ISceneStartup
{
	/// <summary>
	/// Static instance of the only game manager
	/// </summary>
	public static GameManager Instance;
	/// <summary>
	/// Which rules are used to determine gameworks
	/// </summary>
	public static GameRules Rules;

	public GameObject Player { get; set; }
	[Property, ReadOnly, JsonIgnore] protected Transform LastEditorCameraPosition { get; set; }
	[Property, ReadOnly, Range( 0f, 2f )] public float TimeScaleSlider { get; set; } = 1f;

	private float PreviousTimeScaleSlider { get; set; } = 1f;

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( Gizmo.CameraTransform.Position.LengthSquared > 0 )
			LastEditorCameraPosition = Gizmo.CameraTransform;
	}

	[ConVar( "host_timescale", ConVarFlags.Replicated ), Description( "Affects the scale of time, making things faster or slower." )] public static float TimeScale { get; set; } = 1f;
	protected override void OnUpdate()
	{
		base.OnUpdate();

		Rules?.GameFrame();

		if ( PreviousTimeScaleSlider != TimeScaleSlider )
			TimeScale = TimeScaleSlider;
		else
			TimeScaleSlider = TimeScale;

		PreviousTimeScaleSlider = TimeScaleSlider;
		Scene.TimeScale = Scene.IsEditor ? TimeScale : 1;

		DrawAllDebugGizmos();
		ToggleXGUIDebug();
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		Rules?.GameTick();
	}

	protected override void OnStart() { SetInstanceThis(); }

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Scene.IsEditor )
			return;

		DecideGameRules();

		PreSpawn();

		PlayerSpawn();

		PostSpawn();

	}

	/// <summary>
	/// Set the proper gamerules on start, don't call base on override
	/// </summary>
	protected virtual void DecideGameRules() { Rules = new GameRulesFallback(); }

	/// <summary>
	/// Set the instance to this unique manager, don't call base on override
	/// </summary>
	protected virtual void SetInstanceThis() { Instance = this; }
}
