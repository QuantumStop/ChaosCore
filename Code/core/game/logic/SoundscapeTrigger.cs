using Sandbox.Audio;

namespace Core;
/// <summary>
/// a wrapper for the vanilla Soundscape trigger
/// </summary>
[EditorHandle( "" )]
[Title( "EditorHandleless Soundscape" )]
public class SoundscapeTrigger : Sandbox.SoundscapeTrigger { }

[Title( "Soundscape" )]
public class EnvironmentSoundscape : BaseEntity, Component.ExecuteInEditor
{
	[DebugExpose, Property, Feature( "Debug" ), ReadOnly] private SoundscapeTrigger _soundscapeComponent;

	[DebugExpose, Property, Change( nameof( SoundscapeFile ) )] public Soundscape Soundscape { get; set; }
	private void SoundscapeFile() { _soundscapeComponent.Soundscape = Soundscape; }

	[DebugExpose, Property] public EnvironmentManager.EnvironmentType EnvironmentType { get; set; }

	[DebugExpose, Property, Change( nameof( ChangeActive ) )] bool StayActive { get; set; } = false;
	private void ChangeActive() { _soundscapeComponent.StayActiveOnExit = StayActive; }

	[DebugExpose, Property, Change( nameof( ChangeType ) ), Space] public SoundscapeTrigger.TriggerType triggerType { get; set; }
	private void ChangeType() { _soundscapeComponent.Type = triggerType; }

	[DebugExpose, Property, Change( nameof( ChangeRadius ) ), ShowIf( nameof( triggerType ), SoundscapeTrigger.TriggerType.Sphere ), Step( 1 )] public float Radius { get; set; } = 512f;
	private void ChangeRadius() { _soundscapeComponent.Radius = Radius; }

	[DebugExpose, Property, Change( nameof( ChangeBox ) ), ShowIf( nameof( triggerType ), SoundscapeTrigger.TriggerType.Box )] public Vector3 BoxSize { get; set; } = 16f;
	private void ChangeBox() { _soundscapeComponent.BoxSize = BoxSize; }

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( _soundscapeComponent.IsValid() )
		{ _soundscapeComponent.Destroy(); }
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( triggerType == SoundscapeTrigger.TriggerType.Point )
		{
			return;
		}

		if ( triggerType == SoundscapeTrigger.TriggerType.Sphere )
		{
			if ( Gizmo.IsSelected )
			{
				Gizmo.Draw.Color = _soundscapeComponent.Playing ? Gizmo.Colors.Active : Gizmo.Colors.Blue;
				Gizmo.Draw.LineSphere( (Vector3)0f, Radius, 8 );
			}
		}
		else if ( triggerType == SoundscapeTrigger.TriggerType.Box && Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = _soundscapeComponent.Playing ? Gizmo.Colors.Active : Gizmo.Colors.Blue;
			Gizmo.Draw.LineBBox( new BBox( -BoxSize, BoxSize ) );
		}
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		CreateEverything();
	}

	protected override void OnStart()
	{
		base.OnStart();

		CreateEverything();
	}

	private void CreateEverything()
	{
		_soundscapeComponent = Components.GetOrCreate<SoundscapeTrigger>();
		_soundscapeComponent.Flags = ComponentFlags.Hidden;
		_soundscapeComponent.TargetMixer = Mixer.Default;
		_soundscapeComponent.StayActiveOnExit = StayActive;
	}
}
