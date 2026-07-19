namespace Core.AI;

using Core;
using System;
using System.Runtime.CompilerServices;
using static AIController;

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
	[Property] public AIController TargetNPC;
	/// <summary>
	/// The animation played once the sequence begins. If none specified, it will go straight to the action animation.
	/// </summary>
	[Property] public bool UseAnimgraph { get; set; }
	[Property, ShowIf( nameof( UseAnimgraph ), true )] public AnimationGraph Animgraph { get; set; }
	[Property, SequenceSelector, ShowIf( nameof( UseAnimgraph ), false )] public string EntryAnimation { get; set; }
	/// <summary>
	/// The primary animation played. Can be loop if specified, and stopped through scripting.
	/// </summary>
	[Property, SequenceSelector, ShowIf( nameof( UseAnimgraph ), false )] public string ActionAnimation { get; set; }
	/// <summary>
	/// The final animation, played after the action.
	/// </summary>
	[Property, SequenceSelector, ShowIf( nameof( UseAnimgraph ), false )] public string PostActionAnimation { get; set; }
	/// <summary>
	/// The way in which the NPC will navigate for this sequence. 
	/// </summary>
	[Property] public MovementOptions MoveToPosition { get; set; }
	/// <summary>
	/// The next script to play once this one is done.
	/// </summary>
	[Property] public ScriptedSequence NextScript { get; set; }
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
	public bool previewingAnimation;

	enum PreviewStage { Entry, Action, PostAction, Done } // reattempt to do preview
	PreviewStage previewStage;
	TimeSince startTime;
	int counter { get; set; } = -1;


	protected override void OnStart()
	{

		if ( StartActive )
			BeginScriptedSequence();

		base.OnStart();
	}

	protected override string GetEditorVis() => TargetNPC.IsValid() ? TargetNPC?.EditorVis : "models/editor/scripted_sequence.vmdl";

	static Material ghost => Material.Load( "materials/dev/ghost.vmat" );

	protected override void EntityDefaultGizmo( string editorVis, bool isModel )
	{
		if ( GetEditorVis() is null ) return;

		Model vmdl = Model.Load( GetEditorVis() );
		Gizmo.Hitbox.Model( vmdl );

		if ( previewSceneModel is null )
			previewSceneModel = new SceneModel( Scene.SceneWorld, vmdl, WorldTransform ); // i did this on gizmo.world before. do not do as i did. it caused me great pain.

		previewSceneModel.Transform = WorldTransform;


		if ( ghost.IsValid() && TargetNPC.IsValid() )
			previewSceneModel.SetMaterialOverride( ghost );

		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.LineBBox( vmdl.Bounds );
			HandlePreviewStart();
		}
		else
		{
			if ( Gizmo.IsHovered )
			{
				Gizmo.Draw.Color = Color.White.WithAlpha( (((float)Math.Sin( Time.Now * 20f )) * 0.3f) + 0.7f );
				Gizmo.Draw.LineBBox( vmdl.Bounds );
			}
			HandlePreviewStop();
		}
	}

	public void HandlePreviewStart()
	{


		if ( !previewingAnimation )
		{
			previewingAnimation = true;

			previewSceneModel.UseAnimGraph = false;

			previewStage = PreviewStage.Entry;
			PlayPreviewStage();
		}

		if ( previewStage != PreviewStage.Done )
		{
			if ( previewAnimTime + RealTime.Delta >= previewSceneModel.CurrentSequence.Duration )
			{
				AdvancePreviewStage();
				previewAnimTime = 0;
				previewSceneModel.CurrentSequence.Time = 0;
			}
			else
			{
				previewAnimTime += RealTime.Delta;
				previewSceneModel.Update( RealTime.Delta );
			}
		}
	}

	public void HandlePreviewStop()
	{
		if ( previewingAnimation )
		{
			previewingAnimation = false;
			previewStage = PreviewStage.Done;
			previewSceneModel?.Delete();
			previewSceneModel = null;
		}
	}

	SceneModel previewSceneModel;
	float previewAnimTime;
	void AdvancePreviewStage()
	{
		previewStage = previewStage switch
		{
			PreviewStage.Entry => PreviewStage.Action,
			PreviewStage.Action => PreviewStage.PostAction,
			PreviewStage.PostAction => PreviewStage.Entry,
			_ => PreviewStage.Entry
		};

		PlayPreviewStage();
	}

	int _previewSkipGuard = 0;// preview is really being a pain in the ass right now

	void PlayPreviewStage()
	{
		if ( _previewSkipGuard > 3 )
		{
			_previewSkipGuard = 0;
			previewSceneModel.UseAnimGraph = true;
			return; // all anims null, nothing to play so enable animgraph and return
		}

		string anim = previewStage switch
		{
			PreviewStage.Entry => EntryAnimation,
			PreviewStage.Action => ActionAnimation,
			PreviewStage.PostAction => PostActionAnimation,
			_ => null
		};

		if ( !string.IsNullOrEmpty( anim ) )
		{
			_previewSkipGuard = 0;
			previewSceneModel.CurrentSequence.Name = anim;
			previewSceneModel.CurrentSequence.Time = 0;
			previewAnimTime = 0;
		}
		else
		{
			_previewSkipGuard++;
			previewAnimTime = 0;
			AdvancePreviewStage();
		}
	}

	public IEnumerable<string> GetAnimationList()
	{
		if ( directPlayback.Sequences is null )
		{
			Log.Warning( $"No sequences for {TargetNPC.TargetName}" );
		}

		foreach ( var seq in directPlayback.Sequences )
		{
			Log.Info( $"{seq} grabbed" );
			yield return seq;
		}

	}

	[Button]

	public void TestScriptedSequence()
	{
		BeginScriptedSequence();
	}
	public void BeginScriptedSequence()
	{
		//	Log.Info( $"Beginning Scripted Sequence {this}" );
		if ( MoveToPosition < MovementOptions.DontMove )
		{
			if ( TargetNPC is null )
			{
				Log.Warning( $"Scripted Sequence {this} with no Target NPC!" );
				return;
			}

			TargetNPC.shouldMoveToCine = true;
			isWaitingForNPCMovement = true;
		}

		directPlayback = TargetNPC.BodyModel.SceneModel.DirectPlayback;

		//	TargetNPC.Brain.idealState = NpcBrain.AIState.SCRIPTED;
		TargetNPC.aiBrain.aiState = AI_BehaviorState.BEHAVIORSTATE_SCRIPTED;
		TargetNPC._scriptContext = AIController.ScriptingContext.SCRIPT_SEQUENCE;
		TargetNPC._Cine = this;
		TargetNPC.inCine = true;

		if ( MoveToPosition == MovementOptions.DontMove )
		{
			RunEntry();
			isActive = true;
		}


		//	RunPostAction();

	}

	protected override void OnFixedUpdate()
	{
		if ( isWaitingForNPCMovement && TargetNPC.hasReachedCine )
		{
			isWaitingForNPCMovement = false;
			TargetNPC.shouldMoveToCine = false;
			TargetNPC.hasReachedCine = false;

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

		if ( NextScript.IsValid() ) // if we have another script to do, get to it
			NextScript.BeginScriptedSequence();
		else
			TargetNPC.aiBrain.aiState = AI_BehaviorState.BEHAVIORSTATE_DEFAULT;
		//	else
		//	TargetNPC.Brain.idealState = NpcBrain.AIState.IDLE;
	}


	public void RunEntry()
	{
		if ( string.IsNullOrEmpty( EntryAnimation ) )
		{
			counter = 0;
			DoNextTask();
			return;
		}
		directPlayback.StartTime = Time.Now;
		startTime = 0;
		isAnimPlaying = true;
		directPlayback.Play( EntryAnimation );
		counter = 0;
	}

	public void RunAction()
	{
		if ( string.IsNullOrEmpty( ActionAnimation ) )
		{
			counter++;
			DoNextTask();
			return;
		}
		directPlayback.StartTime = Time.Now;
		startTime = 0;
		isAnimPlaying = true;
		directPlayback.Play( ActionAnimation );
		if ( LoopActionAnimation ) return; // dont add to counter since that is what controls the flow
		counter++;
	}

	public void RunPostAction()
	{
		if ( string.IsNullOrEmpty( PostActionAnimation ) )
		{
			counter++;
			DoNextTask();
			return;
		}
		directPlayback.StartTime = Time.Now;
		startTime = 0;
		isAnimPlaying = true;
		directPlayback.Play( PostActionAnimation );
		counter++;
	}

}
