namespace Core.AI;

using Core;
using System;
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

	public AnimGraphDirectPlayback DirectPlayback { get; set; }
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

	public bool IsWaitingForNPCMovement;
	public bool IsActive;
	public bool IsAnimPlaying;
	public bool PreviewingAnimation;

	private enum PreviewStage { Entry, Action, PostAction, Done } // reattempt to do preview
	private PreviewStage _previewStage;
	private TimeSince _startTime;
	private int _counter { get; set; } = -1;


	protected override void OnStart()
	{
		if ( StartActive )
			BeginScriptedSequence();

		base.OnStart();
	}

	protected override string GetEditorVis() => TargetNPC.IsValid() ? TargetNPC?.EditorVis : "models/editor/scripted_sequence.vmdl";

	private static Material _ghost => Material.Load( "materials/dev/ghost.vmat" );

	protected override void EntityDefaultGizmo( string editorVis, bool isModel )
	{
		if ( GetEditorVis() is null ) return;

		Model vmdl = Model.Load( GetEditorVis() );
		Gizmo.Hitbox.Model( vmdl );

		if ( _previewSceneModel is null )
			_previewSceneModel = new SceneModel( Scene.SceneWorld, vmdl, WorldTransform ); // i did this on gizmo.world before. do not do as i did. it caused me great pain.

		_previewSceneModel.Transform = WorldTransform;


		if ( _ghost.IsValid() && TargetNPC.IsValid() )
			_previewSceneModel.SetMaterialOverride( _ghost );

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
				Gizmo.Draw.Color = Color.White.WithAlpha( (((float)Math.Sin( WorldTime.Now * 20f )) * 0.3f) + 0.7f );
				Gizmo.Draw.LineBBox( vmdl.Bounds );
			}
			HandlePreviewStop();
		}
	}

	public void HandlePreviewStart()
	{
		if ( !PreviewingAnimation )
		{
			PreviewingAnimation = true;

			_previewSceneModel.UseAnimGraph = false;

			_previewStage = PreviewStage.Entry;
			PlayPreviewStage();
		}

		if ( _previewStage != PreviewStage.Done )
		{
			if ( _previewAnimTime + RealTime.Delta >= _previewSceneModel.CurrentSequence.Duration )
			{
				AdvancePreviewStage();
				_previewAnimTime = 0;
				_previewSceneModel.CurrentSequence.Time = 0;
			}
			else
			{
				_previewAnimTime += RealTime.Delta;
				_previewSceneModel.Update( RealTime.Delta );
			}
		}
	}

	public void HandlePreviewStop()
	{
		if ( PreviewingAnimation )
		{
			PreviewingAnimation = false;
			_previewStage = PreviewStage.Done;
			_previewSceneModel?.Delete();
			_previewSceneModel = null;
		}
	}

	private SceneModel _previewSceneModel;
	private float _previewAnimTime;
	private void AdvancePreviewStage()
	{
		_previewStage = _previewStage switch
		{
			PreviewStage.Entry => PreviewStage.Action,
			PreviewStage.Action => PreviewStage.PostAction,
			PreviewStage.PostAction => PreviewStage.Entry,
			_ => PreviewStage.Entry
		};

		PlayPreviewStage();
	}

	private int _previewSkipGuard = 0;// preview is really being a pain in the ass right now

	private void PlayPreviewStage()
	{
		if ( _previewSkipGuard > 3 )
		{
			_previewSkipGuard = 0;
			_previewSceneModel.UseAnimGraph = true;
			return; // all anims null, nothing to play so enable animgraph and return
		}

		string anim = _previewStage switch
		{
			PreviewStage.Entry => EntryAnimation,
			PreviewStage.Action => ActionAnimation,
			PreviewStage.PostAction => PostActionAnimation,
			_ => null
		};

		if ( !string.IsNullOrEmpty( anim ) )
		{
			_previewSkipGuard = 0;
			_previewSceneModel.CurrentSequence.Name = anim;
			_previewSceneModel.CurrentSequence.Time = 0;
			_previewAnimTime = 0;
		}
		else
		{
			_previewSkipGuard++;
			_previewAnimTime = 0;
			AdvancePreviewStage();
		}
	}

	public IEnumerable<string> GetAnimationList()
	{
		if ( DirectPlayback.Sequences is null )
		{
			Log.Warning( $"No sequences for {TargetNPC.TargetName}" );
		}

		foreach ( var seq in DirectPlayback.Sequences )
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

			TargetNPC.ShouldMoveToCine = true;
			IsWaitingForNPCMovement = true;
		}

		DirectPlayback = TargetNPC.BodyModel.SceneModel.DirectPlayback;

		//	TargetNPC.Brain.idealState = NpcBrain.AIState.SCRIPTED;
		TargetNPC.AIBrain.aiState = AI_BehaviorState.BEHAVIORSTATE_SCRIPTED;
		TargetNPC.ScriptContext = AIController.ScriptingContext.SCRIPT_SEQUENCE;
		TargetNPC.Cine = this;
		TargetNPC.InCine = true;

		if ( MoveToPosition == MovementOptions.DontMove )
		{
			RunEntry();
			IsActive = true;
		}


		//	RunPostAction();

	}

	protected override void OnFixedUpdate()
	{
		if ( IsWaitingForNPCMovement && TargetNPC.HasReachedCine )
		{
			IsWaitingForNPCMovement = false;
			TargetNPC.ShouldMoveToCine = false;
			TargetNPC.HasReachedCine = false;

			RunEntry();
			IsActive = true;
		}

		if ( IsActive )
		{
			KeepTrack();
		}

		base.OnFixedUpdate();
	}

	private void KeepTrack()
	{
		if ( IsAnimPlaying )
		{
			float elapsed = _startTime;
			float duration = DirectPlayback.Duration;

			if ( elapsed >= duration )
			{
				//Log.Info( $"Animation finished after {elapsed}s." );
				IsAnimPlaying = false;

				DoNextTask();
			}
		}
	}

	public void DoNextTask()
	{
		switch ( _counter )
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

	private void StopLoopingAction()
	{
		LoopActionAnimation = false;
	}

	public void EndSequence()
	{
		DirectPlayback.Cancel();
		IsActive = false;

		if ( NextScript.IsValid() ) // if we have another script to do, get to it
			NextScript.BeginScriptedSequence();
		else
			TargetNPC.AIBrain.aiState = AI_BehaviorState.BEHAVIORSTATE_DEFAULT;
		//	else
		//	TargetNPC.Brain.idealState = NpcBrain.AIState.IDLE;
	}


	public void RunEntry()
	{
		if ( string.IsNullOrEmpty( EntryAnimation ) )
		{
			_counter = 0;
			DoNextTask();
			return;
		}
		DirectPlayback.StartTime = WorldTime.Now;
		_startTime = 0;
		IsAnimPlaying = true;
		DirectPlayback.Play( EntryAnimation );
		_counter = 0;
	}

	public void RunAction()
	{
		if ( string.IsNullOrEmpty( ActionAnimation ) )
		{
			_counter++;
			DoNextTask();
			return;
		}
		DirectPlayback.StartTime = WorldTime.Now;
		_startTime = 0;
		IsAnimPlaying = true;
		DirectPlayback.Play( ActionAnimation );
		if ( LoopActionAnimation ) return; // dont add to counter since that is what controls the flow
		_counter++;
	}

	public void RunPostAction()
	{
		if ( string.IsNullOrEmpty( PostActionAnimation ) )
		{
			_counter++;
			DoNextTask();
			return;
		}
		DirectPlayback.StartTime = WorldTime.Now;
		_startTime = 0;
		IsAnimPlaying = true;
		DirectPlayback.Play( PostActionAnimation );
		_counter++;
	}

}
