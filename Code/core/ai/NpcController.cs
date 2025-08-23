namespace Core;

using Sandbox.Internal;
using System;
using static BaseNPCConditions;
using static BaseNpc;
using static NpcBrain;

public class NpcController : BaseEntity
{
	[Property, Feature( "Brain" )] public bool enableBrain { get; private set; } = true;

	public NpcBrain Brain { get; private set; }
	public BaseNpc BaseNPC { get; private set; }
	public BaseNpcAbility Ability { get; private set; }
	[Property, Feature( "NPC Base" )] public NpcDefinition Definition { get; set; }

	[Property] public AI_SleepState sleepState { get; private set; }

	[Property] public int WakeRadius { get; private set; } = 512;
	[Property] public PathTrack MoveToTarget { get; private set; } // If specified, NPC will move here after spawn.
	public bool shouldFollowMoveToTarget { get; private set; } = false; // Used to tell the npc if we have a MoveToTarget goal
	public NpcRelations Relations { get; private set; }
	public NpcTargeting Targeting { get; private set; }
	public NpcSoundManager SoundMgr { get; private set; }
	public BaseNPCConditions Conditions { get; private set; }
	public List<AIConditions> ActiveConditions { get; set; } = new();

	public NpcSoundManager SoundManager { get; private set; }
	public NavMeshAgent Agent { get; private set; }
	public NavDebugRenderer navDebugRenderer { get; private set; }

	[Property, Feature( "Debug" )] public bool enableBaseNPCDebug { get; private set; }
	[Property, Feature( "Debug" )] public bool enableScheduleDebug { get; private set; }
	[Property, Feature( "Debug" )] public bool enableConditionDebug { get; private set; }
	[Property, Feature( "Debug" )] public bool enableSoundDebug { get; private set; }
	[Property, Feature( "Debug" )] public bool enableNavDebug { get; private set; }

	private GameObject systems;
	
	protected override string GetEditorVis()
	{
		// Gets the first model from the NPC definition, as rendering several models in gizmo isn't ideal
	    // Then return the model path, fallback model if no model is present
		string modelVis = Definition?.Models?.First().ResourcePath; 
      
		return modelVis ?? "models/humans/basemesh_male.vmdl"; 
	}

	protected override void EntityDefaultGizmo( string editorVis, bool isModel )
	{
		Gizmo.Draw.Color = Color.White;
		var editorvis    = GetEditorVis();

		if ( editorvis == null )
			return;

		if ( Game.IsEditor )
		{
			if ( Initialized )
				return;
		}

		var vmdl = Model.Load( editorvis );
		Gizmo.Hitbox.Model( vmdl );
		Gizmo.Draw.Model( vmdl ).Flags.CastShadows = true;

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

	protected override void OnAwake()
	{
		// So we store most of the components in a child gameobject, this is solely to reduce to absolute hell that having all components on one GO used to be
		systems = GameObject.Children.FirstOrDefault( c => c.Name == "Systems" );
		if ( systems == null )
		{
			systems = new GameObject();
			systems.Name = "AIContainer";
			systems.Parent = GameObject.Root;
		}

		// Assign components for the child
		Brain = systems.Components.GetOrCreate<NpcBrain>();
		Conditions = systems.Components.GetOrCreate<BaseNPCConditions>();
		Targeting = systems.Components.GetOrCreate<NpcTargeting>();
		SoundManager = systems.Components.GetOrCreate<NpcSoundManager>();
		Relations = systems.Components.GetOrCreate<NpcRelations>();

		//Agent = systems.Components.GetOrCreate<NavMeshAgent>();

		// Set up parent NPC  TODO: Move a lot of this out
		BaseNPC = Components.GetOrCreate<BaseNpc>();
		BaseNPC.Brain = Brain;
		BaseNPC.Conditions = Conditions;
		BaseNPC.Targeting = Targeting;
		BaseNPC.Relations = Relations;
		BaseNPC.Agent = Components.GetOrCreate<NavMeshAgent>();

		BaseNPC.NpcDef = Definition;
		BaseNPC.Agent.Height = Definition.AgentHeight;
		BaseNPC.Agent.Radius = Definition.AgentRadius;
		BaseNPC.Agent.Acceleration = Definition.AgentAccel;

		Ability = systems.Components.GetOrCreate<BaseNpcAbility>();

		var hitbox = systems.Components.GetOrCreate<ModelHitboxes>();
		BaseNPC.Hitboxes = hitbox;

		var animgraphController = systems.Components.GetOrCreate<BaseNpcAnimgraphController>();
		BaseNPC.AnimgraphController = animgraphController;

		Tags.Add( "npc" );
	}

	protected override void OnStart()
	{
		if ( enableNavDebug )
		{
			navDebugRenderer = systems.Components.GetOrCreate<NavDebugRenderer>();
			navDebugRenderer.Npc = BaseNPC;
		}

		if ( MoveToTarget != null )

		{
			Brain.idealState = AIState.SCRIPTED;
			shouldFollowMoveToTarget = true;

		//	BaseNPC._currentPathCorner = MoveToTarget.pathPoints[0];
			//BaseNPC._currentPathCorner.currentUser = BaseNPC;

			Brain.StartSchedule( AISchedules.SCHED_SCRIPTED_MOVE );

		}
		else
		{
			Brain.StartSchedule( AISchedules.SCHED_IDLE_STAND );
			Brain.idealState = AIState.IDLE;
		}


		BaseNPC.DebugMode = enableBaseNPCDebug;
		Conditions.DrawDebug = enableConditionDebug;
		BaseNPC.TargetName = TargetName;
		Brain.Enabled = enableBrain;

		BaseNPC.SetSleepState( sleepState );

		BaseNPC.BodyModel.Set( "b_IsMoving", true ); // horrible evil nasty thing, need to figure out why weird behavior with this disabled
		base.OnStart();
	}

	protected override void OnUpdate()
	{
		if ( enableSoundDebug )
		{
			SoundManager.DrawDebug = true;
		}

		if ( enableScheduleDebug )
		{
			Brain.DrawDebug = true;
			//	Brain.DrawDebugText = true;
		}

		base.OnUpdate();
	}
}
