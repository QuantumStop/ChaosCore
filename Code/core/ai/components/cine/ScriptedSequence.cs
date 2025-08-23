using Sandbox;
using System;

namespace Core;

public class ScriptedSequence : BaseEntity
{
	public enum MovementOptions
	{
		Walk,
		Run,
		Instantaneous,
		MoveToTarget,
		TurnToFace,
		DontMove,
	}

	public AnimGraphDirectPlayback directPlayback { get; set; }
	/// <summary>
	/// The NPC that the sequence will target.
	/// </summary>
	[Property] public NpcController TargetNPC;
	/// <summary>
	/// The animation played once the sequence begins. If none specified, it will go straight to the action animation.
	/// </summary>
	[Property] public string EntryAnimation { get; set; }
	/// <summary>
	/// The primary animation played. Can be loop if specified, and stopped through scripting.
	/// </summary>
	[Property] public string ActionAnimation { get; set; }
	/// <summary>
	/// The final animation, played after the action.
	/// </summary>
	[Property] public string PostActionAnimation { get; set; }
	/// <summary>
	/// The way in which the NPC will navigate for this sequence. 
	/// </summary>
	[Property] public MovementOptions MoveToPosition { get; set; }
	/// <summary>
	/// The next script to play once this one is done.
	/// </summary>
	[Property] ScriptedSequence NextScript { get; set; }
	/// <summary>
	/// Loops the action animation indefinitely. Can be stopped with StopLoopingAction method.
	/// </summary>
	[Property] public bool LoopActionAnimation { get; set; } = false;
	/// <summary>
	/// Starts this sequence as soon as the game is loaded
	/// </summary>
	[Property] public bool StartActive { get; set; } = false;
	/// <summary>
	/// If true, NPC takes no damage while in scripted sequence.
	/// </summary>
	[Property] public bool AllowActorDeath { get; set; } = false;

	public bool isWaitingForNPCMovement;
	public bool isActive;
	public bool isAnimPlaying;
	TimeSince startTime;
	int counter { get; set; } = -1;

	
	protected override void OnStart()
	{
		if ( StartActive )
		{
			BeginScriptedSequence();
		}
		base.OnStart();
	}


	protected override string GetEditorVis() { return TargetNPC?.EditorVis; }
    private SceneObject previewModel;

	protected override void EntityDefaultGizmo( string editorVis, bool isModel )
	{
		Gizmo.Draw.Color = Color.White;

		if ( GetEditorVis() == null )
			return;

		Model vmdl = Model.Load( GetEditorVis() );
		Gizmo.Hitbox.Model( vmdl );

		if ( previewModel == null || !previewModel.IsValid() )
		{
			Material material = Material.Load( "materials/shaders/forcefield_test.vmat" ); //  TODO: Forcefield for now, we'll have a better one later

			previewModel = new SceneObject( Scene.SceneWorld, vmdl, WorldTransform );
			if ( material.IsValid() )
				previewModel.SetMaterialOverride( material );
			previewModel.Flags.CastShadows = false;
		}

		if ( previewModel != null )
		{
			previewModel.Transform = WorldTransform; // Need update the transform in Gizmo context
		}

		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.LineBBox( vmdl.Bounds );
		}

		else if ( Gizmo.IsHovered )
		{
			Gizmo.Draw.Color = Color.White.WithAlpha( (((float)Math.Sin( Time.Now * 20f )) * 0.3f) + 0.7f );
			Gizmo.Draw.LineBBox( vmdl.Bounds );
		}
	}


	public IEnumerable<string> GetAnimationList()
	{
		if ( directPlayback.Sequences == null )
		{
			Log.Warning( $"No sequences for {TargetNPC.TargetName}" );
		}

		foreach ( var seq in directPlayback.Sequences )
		{

			Log.Info( $"{seq} grabbed" );
			yield return seq;
		}

	}

	[Button] public void BeginScriptedSequence()
	{
		if ( MoveToPosition < MovementOptions.DontMove)
		{
			TargetNPC.BaseNPC.shouldMoveToCine = true;
			isWaitingForNPCMovement = true;
		}

		directPlayback = TargetNPC.BaseNPC.AGDirectPlayback;
		
		TargetNPC.Brain.idealState = NpcBrain.AIState.SCRIPTED;
		TargetNPC.BaseNPC._Cine = this;
		TargetNPC.BaseNPC.inCine = true;


		if ( MoveToPosition == MovementOptions.DontMove )
		{
			RunEntry();
			isActive = true;
		}
	

		//	RunPostAction();

	}

    protected override void OnFixedUpdate()
    {
		if ( isWaitingForNPCMovement && TargetNPC.BaseNPC.hasReachedCine )
		{
			isWaitingForNPCMovement = false;
			TargetNPC.BaseNPC.shouldMoveToCine = false;
			TargetNPC.BaseNPC.hasReachedCine = false;

			RunEntry();
			isActive = true;
		}

		if ( isActive )
		{
			KeepTrack();
		}

		base.OnFixedUpdate();
    }

    void KeepTrack()
	{
	if ( isAnimPlaying )
		{
			float elapsed = startTime;
			float duration = directPlayback.Duration;

			if ( elapsed >= duration )
			{
				//Log.Info( $"Animation finished after {elapsed}s." );
				isAnimPlaying = false;

				DoNextTask();
			}
		}
	}

	public void DoNextTask()
	{
		switch ( counter )
		{
			case 0:
				RunAction();
				break;
			case 1:
				RunPostAction();
				break;
			case 2:
				EndSequence();
				break;
		}

	}

	void StopLoopingAction()
	{
		LoopActionAnimation = false;
	}

	public void EndSequence()
	{
		directPlayback.Cancel();
		isActive = false;

		if ( NextScript != null ) // if we have another script to do, get to it
		NextScript.BeginScriptedSequence();
		else
			TargetNPC.Brain.idealState = NpcBrain.AIState.IDLE;
	}

	public void RunEntry()
	{
		Log.Info( $"ScriptedSequence Started" );
		directPlayback.StartTime = Time.Now;
		startTime = 0;
		isAnimPlaying = true;
		directPlayback.Play(EntryAnimation);
		counter = 0;
	}

	public void RunAction()
	{
		directPlayback.StartTime = Time.Now;
		startTime = 0;
		isAnimPlaying = true;
		directPlayback.Play( ActionAnimation );
		if ( LoopActionAnimation )
		{
			return; // Dont add to the counter since that is what controls the flow
		}
		counter++;
	}

	public void RunPostAction()
	{
		directPlayback.StartTime = Time.Now;
		startTime = 0;
		isAnimPlaying = true;
		directPlayback.Play( PostActionAnimation );
		counter++;
	}

}
