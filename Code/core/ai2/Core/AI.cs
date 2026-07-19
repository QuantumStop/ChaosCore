//#define OLD_THINK // uncomment to test the old tick system


using Core.Voxels;
#if FMOD
using FMODSbox;
#endif
using Sandbox.Navigation;
using Sandbox.VR;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Core.AI.NpcRelations;
using static Sandbox.UI.Panel;

namespace Core.AI;

public interface IAIEvent : ISceneEvent<IAIEvent>
{
	void OnDamage( DamageInfo dmgInfo );
	void OnTouch();
}

[Title( "AI Controller" )]
[Category( "NPC" )]
[Icon( "videogame_asset" )]

public class AIController : BaseEntity, Component.IDamageable, Component.ICollisionListener
{
	public static bool NoTarget { get; set; } = false;

	[ConCmd( "notarget" )]
	private static void ToggleNoTarget()
	{
		NoTarget = !NoTarget;

		Log.Info( "notarget: " + NoTarget );
	}

	/// <summary>
	/// Determines which decision making the NPC should use. Default is GOAP, scripted is for scripted sequences, scripted movement, etc.
	/// </summary>
	public enum AI_BehaviorState
	{
		BEHAVIORSTATE_DEFAULT,
		BEHAVIORSTATE_SCRIPTED,
	}

	/// <summary>
	/// The type of goal. Sparsely used, but will eventually be used to determine what can interrupt a movement.
	/// </summary>
	public enum GoalType
	{
		GOALTYPE_NONE,
		GOALTYPE_TARGETENT,
		GOALTYPE_ENEMY,
		GOALTYPE_PATHCORNER,
		GOALTYPE_PATHCORNER_CONTINUOUS,
		GOALTYPE_CINE,
		GOALTYPE_LOCATION,
		GOALTYPE_FLANK,
		GOALTYPE_COVER,

		GOALTYPE_INVALID
	};

	/// <summary>
	/// The type of movement which this npc can use
	/// </summary>
	public enum MoveType
	{

		MOVE_NONE,
		MOVE_GROUND,
		MOVE_FLY,
		MOVE_CRAWL,
		MOVE_SWIM

	}

	/// <summary>
	/// The type of scripting which we are currently under.
	/// </summary>
	public enum ScriptingContext
	{
		SCRIPT_SEQUENCE,
		SCRIPT_CHOREO,
		SCRIPT_PATH,
		SCRIPT_FOLLOW_ENTITY,
		SCRIPT_FORCED_MOVE,
	}

	public enum NavigationStatus
	{
		NO_NAVIGATION_TARGET,
		NAVIGATION_STARTED,
		NAVIGATION_COMPLETED,
		NAVIGATION_IN_ERROR,

	}

	public enum SleepState
	{
		SLEEPSTATE_NONE,
		SLEEPSTATE_WAIT_FOR_INPUT,
	}


	public struct LookTarget
	{
		public Vector3 Position;
		public float Priority;
	}

#if IGNIS
	[SaveRestore] 
#endif
	public WorldState CurrentState { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public WorldState GoalState { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public NavMeshAgent Agent { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public AIBlackBoard Blackboard { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public SkinnedModelRenderer BodyModel { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public ModelHitboxes HitboxSet { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public ModelPhysics PhysModel { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public ModelCollider Collider { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public AnimGraphDirectPlayback AGDirectPlayback { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public AIManager aiManager { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public AIBrain aiBrain { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public AICorpse aiCorpse { get; set; }



#if IGNIS
	[SaveRestore] 
#endif
	public AISquad aiSquad { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public NpcRelations Relationships { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public AINavigation Navigation { get; set; }
	//public NPCPhysics Physics;


#if IGNIS
	[SaveRestore] 
#endif
	public ThreatEvaluator threatEvaluator { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public ScentSensor scentSensor { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public NpcTargetingSensor targetingSensor { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public SquadSensor squadSensor { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public PainSensor painSensor { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public TouchSensor touchSensor { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public ScentEmitter scentEmitter { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public WorldState WorldState { get; private set; } = new();
#if IGNIS
	[SaveRestore] 
#endif
	public List<AIAction> Actions { get; private set; } = new();
#if IGNIS
	[SaveRestore] 
#endif
	public List<Goal> Goals { get; private set; } = new();
#if IGNIS
	[SaveRestore] 
#endif
	public List<AIAbility> Abilities { get; private set; } = new();

	[Property] public SleepState currentSleepState { get; set; }

#if IGNIS
	[SaveRestore] 
#endif
	private AIPlanner planner = new();
#if IGNIS
	[DebugExpose]
#endif
	public string currentGoalName { get; set; }

#if IGNIS
	[SaveRestore] 
#endif
	public Goal currentGoal { get; set; }

#if IGNIS
	[DebugExpose, SaveRestore] 
#endif
	public AIAction currentAction { get; set; }

	[Property] public bool DebugPlanning { get; set; } = false;
#if IGNIS
	[DebugExpose( hideIfEmpty: true )]
#endif
	[Property] public string squadName { get; set; }
#if IGNIS
	[SaveRestore, DebugExpose( hideIfEmpty: true )] 
#endif
	public NavigationStatus navStatus;

	[Property] public event Action<DamageInfo> Damaged;
	[Property] public event Action Touched;

#if IGNIS
[DebugExpose, SaveRestore]
#endif
	public GoalType currentMoveGoal = GoalType.GOALTYPE_NONE;

#if IGNIS
[DebugExpose, SaveRestore]
#endif
	public MoveType moveType;

#if IGNIS
[SaveRestore]
#endif
	public float NextRange1AttackTime;

#if IGNIS
[SaveRestore]
#endif
	public float TurnSpeed = 5;

#if IGNIS
[SaveRestore]
#endif
	public float lastDamageTime;

#if IGNIS
[DebugExpose, SaveRestore]
#endif
	public float curHealth;

#if IGNIS
[SaveRestore]
#endif
	public float maxHealth;

#if IGNIS
[SaveRestore]
#endif
	public bool IsAlive = true;

#if IGNIS
[SaveRestore]
#endif
	public bool lookAtTarget = false;

#if IGNIS
[SaveRestore]
#endif
	public bool lookInMoveDirection = false;

#if IGNIS
[SaveRestore]
#endif
	public List<LookTarget> PotentialLookTargets = new List<LookTarget>(); // Vector 3 for position, float for look priority

#if IGNIS
[SaveRestore]
#endif
	public LookTarget? currentLookTarget;

#if IGNIS
[SaveRestore]
#endif
	public bool staticEnemy; // static enemies do not move or face a target

#if IGNIS
[SaveRestore]
#endif
	public bool touchingPlayer = false;

#if IGNIS
[SaveRestore]
#endif
	public bool touchingAlly = false;

#if IGNIS
[SaveRestore]
#endif
	public bool touchingEnemy = false;

	/// <summary>
	/// When enabled, npcs will be placed at the closest point on the navmesh. Prevents poorly placed npcs from breaking due to not being on the navmesh
	/// </summary>
	[Property] public bool fallToGround = true;

#if IGNIS
[SaveRestore]
#endif
	public ScriptingContext _scriptContext;

#if IGNIS
[SaveRestore]
#endif
	public bool inCine { get; set; } = false; // For scripted sequences

#if IGNIS
[SaveRestore]
#endif
	public ScriptedSequence _Cine { get; set; } = null; // For scripted sequences

#if IGNIS
[SaveRestore]
#endif
	public bool shouldMoveToCine = false;

#if IGNIS
[SaveRestore]
#endif
	public bool hasReachedCine = false;

#if IGNIS
[SaveRestore]
#endif
	public bool shouldAttackBullseye = false;

#if IGNIS
[SaveRestore]
#endif
	public Bullseye currentBullseye;

#if IGNIS
[SaveRestore]
#endif
	public Vector3? TargetPosition;

#if IGNIS
[SaveRestore]
#endif
	public Vector3 LastAttackDir;


	// currently playing event. may need to be a list eventually?
#if FMOD
FMOD.Studio.EventInstance currentEvent;
#else
	private SoundEvent currentEvent;
#endif

	[Property] public NpcDefinition Definition { get; set; }

	[Property] public AIBehavior BehaviorModule { get; set; }

#if IGNIS
[SaveRestore]
#endif
	public List<AIAction> currentPlan { get; set; } = new List<AIAction>();

#if IGNIS
[SaveRestore]
#endif
	private int planStep = 0;

#if IGNIS
[SaveRestore]
#endif
	public List<string> lastDebugState = new List<string>();

#if IGNIS
[SaveRestore]
#endif
	public List<string> currentDebugState = new List<string>();

#if IGNIS
[SaveRestore]
#endif
	public HintNode _activeHintNode;

#if IGNIS
[SaveRestore]
#endif
	public float? lastSeenEnemyTime = null;

#if IGNIS
[SaveRestore]
#endif
	public Vector3? enemyLKP = null;

#if IGNIS
[SaveRestore]
#endif
	public Vector3? enemyLKV = null;

	/// <summary>
	///  This is set by the navigation module
	/// </summary>

#if IGNIS
[SaveRestore]
#endif
	public bool isMoving = false;

#if IGNIS
[SaveRestore]
#endif
	public bool canMove = true;

#if IGNIS
[SaveRestore]
#endif
	public float NextNavAllowedTime;

#if IGNIS
[SaveRestore]
#endif
	public Vector3 LastNavTarget;

	private const float _maxNavDistance = 512f; // this name is a bit confusing, but this dictates how close the player must be for us to try and update nav most often
	private const float _navPlayerNearDist = 256f;
	private const float _navPlayerFarDist = 2048f;

#if IGNIS
[SaveRestore]
#endif
	private float _lightDamageExpiry;

#if IGNIS
[SaveRestore]
#endif
	private float _heavyDamageExpiry;

#if IGNIS
[SaveRestore]
#endif
	public bool KillOnNextUpdate = false; // sometimes the ai misbehaves

#if IGNIS
[SaveRestore]
#endif
	public Vector3 LastSoundHeardPosition;

#if IGNIS
[SaveRestore]
	private GameObject _bleeder; // holds the blood prefab
#endif

	public JsonObject CreateSaveState()
	{
		var state = new JsonObject
		{
			[nameof( CurrentState )] = Json.ToNode( CurrentState )
		};

		return state;
	}

	public override JsonObject CustomSerialize()
	{
		JsonObject customdata = new JsonObject
		{
			{"currentGoalName", currentGoalName},
			{"currentAction", Json.ToNode((currentAction))},
		};
		base.CustomSerialize();
		return customdata;
	}
	public override void CustomDeserialize( JsonObject node )
	{
		base.CustomDeserialize( node );

		node.TryGetPropertyValue( "currentGoalName", out JsonNode goalNode );
		currentGoalName = goalNode?.ToString();

		node.TryGetPropertyValue( "currentAction", out JsonNode actionNode );
		if ( actionNode != null )
		{
			currentAction = actionNode.Deserialize<AIAction>();
		}
	}

	public bool TryApplySaveState( JsonObject state )
	{
		if ( state is null )
			return true;

		if ( state[nameof( CurrentState )] is JsonNode statenode )
			CurrentState = Json.FromNode<WorldState>( statenode );

		return true;

	}

	/// <summary>
	/// Inits all of the AI modules
	/// </summary>
	public void InitAIModules()
	{
		aiBrain = new AIBrain(); // give this guy a brain first
		aiBrain.Init( this );

		BodyModel = Components.GetOrCreate<SkinnedModelRenderer>();
		BodyModel.OnFootstepEvent += OnFootstepEvent;
		BodyModel.OnGenericEvent += OnGenericEvent;

		HitboxSet = Components.GetOrCreate<ModelHitboxes>();
		HitboxSet.Renderer = BodyModel;

		if ( moveType == MoveType.MOVE_NONE )
			staticEnemy = true;
		if ( moveType == MoveType.MOVE_GROUND || moveType == MoveType.MOVE_CRAWL )

			Agent = Components.GetOrCreate<NavMeshAgent>();
		PhysModel = Components.GetOrCreate<ModelPhysics>();
		Collider = Components.GetOrCreate<ModelCollider>();

		targetingSensor = new NpcTargetingSensor() { Owner = this };
		threatEvaluator = new ThreatEvaluator();
		scentSensor = new ScentSensor( this );
		painSensor = new PainSensor( this );
		touchSensor = new TouchSensor( this );

		Blackboard = new AIBlackBoard();
		Blackboard.Init( this );

		if ( moveType == MoveType.MOVE_GROUND || moveType == MoveType.MOVE_CRAWL )
		{
			Navigation = new AINavigation();
			Navigation.Init( this );
		}

		PhysModel.Enabled = false;
		PhysModel.MotionEnabled = false;

		BodyModel.Model = Definition.Models[0];
		BodyModel.CreateBoneObjects = true;
		PhysModel.Model = BodyModel.Model;

		Collider.Model = BodyModel.Model;
		Collider.ColliderFlags = ColliderFlags.IgnoreTraces;

		if ( moveType == MoveType.MOVE_GROUND || moveType == MoveType.MOVE_CRAWL )
		{
			Agent.MaxSpeed = Definition.AgentMaxSpeed;
			Agent.Acceleration = Definition.AgentAccel;
			Agent.Radius = Definition.AgentRadius;
			Agent.Height = Definition.AgentHeight;
			Agent.Separation = Definition.AgentSeparation;
		}

		if ( BodyModel.SceneModel is not null )
			AGDirectPlayback = BodyModel.SceneModel.DirectPlayback;
		else
			Log.Warning( $"{TargetName} has null SceneModel!" );

		Blackboard.playerReference = Scene.GetAllComponents<BasePlayer>().FirstOrDefault();

		Relationships = AddComponent<NpcRelations>(); // soon i destroy ALL COMPONENTS. this should be a CLASS!!!
		Relationships.Owner = this;
		Relationships.Faction = Definition.Faction;
		Relationships.Init();

		BehaviorModule?.Init( this );
		aiCorpse = new AICorpse();
		aiCorpse.Init( this );

		if ( BehaviorModule is null )
			Log.Error( "AI::InitModules failed to create BehaviorModule!" );
	}

	public void PopulateActions()
	{
		foreach ( var action in Definition.ActionList )
		{
			var instance = AIActionRegistry.Current?.Create( action.Action, this );
			if ( instance is not null )
				Actions.Add( instance );
		}
	}

	public void PopulateGoals()
	{
		foreach ( var goals in Definition.Goals )
		{
			Goals.Add( new Goal(
				goals.ResourceName,
				[new WorldFact( goals.Goal.goalName, goals.Goal.goalState )],
				priority: goals.Goal.goalWeight ) );
		}
	}

	/// <summary>
	/// Configures the default worldstate by setting all facts to false.
	/// </summary>
	public void ConfigureDefaultWorldState()
	{
#if IGNIS || STANDALONE
		foreach ( var fact in AIFacts.All() )
		{
			WorldState.Set( fact, false );
		}
#endif
	}

	public void PopulateHints()
	{
		foreach ( var hint in Scene.GetAllComponents<HintNode>() )
		{
			Blackboard.nodePool.Add( hint );
		}
	}

	public void ConfigureSquad()
	{
		if ( !string.IsNullOrEmpty( squadName ) )
		{
			var squadSystem = Scene.GetSystem<AISquadManager>();
			aiSquad = squadSystem.GetOrCreateSquad( squadName, this );
			squadSensor = new SquadSensor( this );
		}
	}

	public float lastRangeAttack1Time = 0f;
	public float lastRangeAttack2Time = 0f;
	public float lastMeleeAttack1Time = 0f;
	public float lastMeleeAttack2Time = 0f;

	public float rangeAttack1Cooldown = 4f;
	public float rangeAttack2Cooldown = 4f;
	public float meleeAttack1Cooldown = 1f;
	public float meleeAttack2Cooldown = 2f;

	/// <summary>
	/// Returns true if we have met the conditions to dispatch a rangeattack1
	/// </summary>
	/// <returns></returns>
	public bool CanRangeAttack1()
	{
		if ( Time.Now <= (lastRangeAttack1Time + rangeAttack1Cooldown) ) return false;
		return true;
	}
	/// <summary>
	/// Returns true if we have met the conditions to dispatch a rangeattack2
	/// </summary>
	/// <returns></returns>
	public bool CanRangeAttack2()
	{
		if ( Time.Now <= (lastRangeAttack2Time + rangeAttack2Cooldown) ) return false;

		return true;

	}
	/// <summary>
	/// Returns true if we have met the conditions to dispatch a meleeattack1
	/// </summary>
	/// <returns></returns>
	public bool CanMeleeAttack1()
	{
		if ( Time.Now <= (lastMeleeAttack1Time + meleeAttack1Cooldown) ) return false;

		return true;

	}
	/// <summary>
	/// Returns true if we have met the conditions to dispatch a meleeattack2
	/// </summary>
	/// <returns></returns>
	public bool CanMeleeAttack2()
	{
		if ( Time.Now <= (lastMeleeAttack2Time + meleeAttack2Cooldown) ) return false;

		return true;

	}

#if FMOD
	/// <summary>
	/// Returns the currently active FMOD event
	/// </summary>
	/// <returns></returns>
	public FMOD.Studio.EventInstance GetCurrentFMODEvent()
	{
		return currentEvent;
	}

	/// <summary>
	/// Sets the active FMOD event
	/// </summary>
	/// <returns></returns>
	public void SetCurrentFMODEvent( FMOD.Studio.EventInstance eventToSet )
	{
		currentEvent = eventToSet;
	}


#else
	public SoundEvent GetCurrentSoundEvent() => currentEvent;
	public void SetCurrentSoundEvent( SoundEvent eventToSet ) => currentEvent = eventToSet;
#endif

	/// <summary>
	/// Grabs the max health from the NPC definition and sets it.
	/// </summary>
	public void GetAndSetHealth()
	{
		maxHealth = Definition.Health;
		curHealth = maxHealth;
	}

	/// <summary>
	/// Registers an AI with the global ai manager.
	/// </summary>
	public void AddNPCToAIManager()
	{
		aiManager = Scene.GetSystem<AIManager>();
		aiManager.AddAI( this );
	}

	/// <summary>
	/// Handles setup and creation of the NPC using the Definition.
	/// </summary>
	public void Spawn()
	{
		moveType = Definition.MoveType;
#if IGNIS || STANDALONE
		if ( BehaviorModule is null )
		{
			var behaviorType = AIBehavior.ResolveBehaviorType( Definition.BehaviorClass );
			BehaviorModule = Activator.CreateInstance( behaviorType ) as AIBehavior;
#endif
		BehaviorModule?.Bind( this );

		InitAIModules();
		ConfigureDefaultWorldState();
		PopulateGoals();
		PopulateActions();
		ConfigureSquad();
		CollectAbilities();
		PopulateHints();
		GetAndSetHealth();
		AddNPCToAIManager();

		if ( currentSleepState > 0 )
			SleepNPC();

		Tags.Add( "npc" );
		if ( fallToGround && moveType == MoveType.MOVE_GROUND || moveType == MoveType.MOVE_CRAWL )
			GameObject.WorldPosition = Scene.NavMesh.GetClosestPoint( Agent.WorldPosition ).Value;

		scentEmitter = new ScentEmitter
		{
			Radius = Definition.OdorIntensity,
			Intensity = 1,
			Category = ScentCategory.Creature,
			SourceEnt = this,
			Position = WorldPosition
		};
	}
	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( collision.Other.GameObject.Root.GetComponent<BasePlayer>() is { } player )
		{
			Vector3 touchPos = collision.Other.GameObject.WorldPosition;
			FaceTarget( touchPos, 2f );


			Touched?.Invoke();
			touchingPlayer = true;
			//	BodyModel.Set( "b_Interrupt", true );
			//Log.Info( "Touching player" );
		}
		else if ( collision.Other.GameObject.Root.GetComponent<AIController>() is { } npc )
		{
			Vector3 touchPos = collision.Other.GameObject.WorldPosition;
			FaceTarget( touchPos, 2f );


			Touched?.Invoke();

			var friend = (npc.Definition.ResourceName == Definition.ResourceName && aiSquad == npc.aiSquad); // if we have the same def and are in the same squad, we are a friend 

			touchingAlly = friend;
			BodyModel.Set( "b_Interrupt", true );
			Log.Info( $"Touching npc isFriend{friend}" );
		}

		// TODO update facts for touching npcs based on relationship
	}
	protected override void OnStart() => Spawn();
	protected override void OnDestroy()
	{
		StopAllNPCSounds();
		currentAction = null;
		currentGoal = null;
		currentPlan = null;

	}

	/// <summary>
	/// Handles footstep sounds
	/// </summary>
	/// <param name="e"></param>
	private void OnFootstepEvent( SceneModel.FootstepEvent e ) => PlayFootstepSound( e.Transform.Position, e.Volume, e.FootId );

	/// <summary>
	/// Generic animevent handler
	/// </summary>
	/// <param name="e"></param>
	private void OnGenericEvent( SceneModel.GenericEvent e )
	{
		switch ( e.Type )
		{
			case "StartAttackSound":
				PlayRangeAttack1Sound();
				break;
			case "EndAttackAnim":
				StopAllNPCSounds(); // i should really be storing animevent driven events seperately, but for now this will do
									// todo should hook these into the npc behavior module and pass down info

				PlayRangeAttack1SecondarySound();

				break;
		}
		BehaviorModule.HandleGenericEvent( e );
	}

	/// <summary>
	/// Plays a footstep sound from a defined position
	/// </summary>
	/// <param name="worldPosition"></param>
	/// <param name="volume"></param>
	/// <param name="foot"></param>
	public void PlayFootstepSound( Vector3 worldPosition, float volume, int foot )
	{
		var tr = Scene.Trace
			.Ray( worldPosition + Vector3.Up * 10, worldPosition + Vector3.Down * 20 )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( !tr.Hit || !tr.Surface.IsValid() ) return;

#if FMOD

		var handle = FMODSound.Play( "event:/Physics/StepLeft", GameObject );
		handle.setVolume( 1 );
#else
		SoundEvent soundEvent = tr.Surface.SoundCollection.FootLeft;
		if ( !soundEvent.IsValid() ) return;
		GameObject.PlaySound( soundEvent, 0 ).Volume *= volume * 0.5f;
#endif


	}

	/// <summary>
	/// Sets NPC movement speed in units 
	/// </summary>
	/// <param name="speed"></param>
	public void SetMovementSpeed( float speed )
	{
		if ( moveType == MoveType.MOVE_GROUND || moveType == MoveType.MOVE_CRAWL )
		{
			Agent.MaxSpeed = speed;
			Agent.Acceleration = speed * 1.25f;
		}

	}

	/// <summary>
	/// Resets transient conditions
	/// </summary>
	public void ResetTimedConditions()
	{
		// facts should be expanded with a parameter to include facts that should return to false after an interval, like this
		if ( Time.Now > _lightDamageExpiry ) WorldState.Set( AIFacts.LightDamage, false );
		if ( Time.Now > _heavyDamageExpiry ) WorldState.Set( AIFacts.HeavyDamage, false );

		WorldState.Set( AIFacts.EnemyHurt, false );
		WorldState.Set( AIFacts.WithPack, false );

	}

	/// <summary>
	/// Sets the priority of a goal at runtime
	/// </summary>
	/// <param name="goalName"></param>
	/// <param name="priority"></param>
	public void SetGoalPriority( string goalName, float priority )
	{
		var goal = Goals.First( x => x.Name == goalName );
		goal.Priority = priority;

	}

	/// <summary>
	/// Creates the bleeder particle effect
	/// </summary>
	/// <returns></returns>
	GameObject CreateBleederEntity() => Scene.GetPrefab( "prefabs/npc/Util/BloodSplat.prefab" ).Clone( WorldPosition );

	/// <summary>
	/// Creates the bleeder and handles spawning of particles
	/// </summary>
	/// <param name="vecDir"></param>
	/// <param name="dmginfo"></param>
	void SpawnBlood( Vector3 vecDir, DamageInfo dmginfo )
	{
#if IGNIS
		Vector3 bloodDir;
		bloodDir = vecDir.Normal;
		if ( Bleeder is null )
		{
			Bleeder = CreateBleederEntity();
			Bleeder.Parent = GameObject;
		}
		else
		{
			float strength = Math.Clamp( dmginfo.Damage * 25f, 20f, 400f );

			var particle = Bleeder.GetComponent<ParticleEffect>();
			var particleDecal = Bleeder.GetComponent<ParticleDecalRenderer>();

			particle.Emit( dmginfo.Position, Time.Delta );
		}
#endif
	}

	/// <summary>
	/// Sets the Behavior module for this instance to use
	/// </summary>
	/// <param name="behavior"></param>
	public void SetBehavior( AIBehavior behavior )
	{
		BehaviorModule = behavior;
		BehaviorModule.Init( this );
	}

	/// <summary>
	/// Rests goal priorities to their Definitions default
	/// </summary>
	public void ResetGoalPrioritiesToDefault()
	{
		if ( Definition?.Goals is null || Goals is null )
			return;

		// lookup from definition
		var defLookup = new Dictionary<string, float>();

		foreach ( var def in Definition.Goals )
		{
			if ( def?.Goal is null )
				continue;

			defLookup[def.ResourceName] = def.Goal.goalWeight;
		}

		// apply to runtime goals
		foreach ( var goal in Goals )
		{
			if ( goal is null )
				continue;

			if ( defLookup.TryGetValue( goal.Name, out var weight ) )
			{
				Log.Info( $"setting priority {weight} on {goal} for NPC {this}" );
				goal.Priority = weight;
			}
		}
	}

	/// <summary>
	/// Stops all emitted sounds from playing. Note this does not gag an npc.
	/// </summary>
	public void StopAllNPCSounds()
	{
#if FMOD
		if ( !currentEvent.isValid() ) return;

		var fmodEvent = GetCurrentFMODEvent();
		if ( !fmodEvent.isValid() ) return;

		FMOD.Studio.PLAYBACK_STATE state;
		var result = fmodEvent.getPlaybackState( out state );

		// no clue if this is right, trying to fix random failed handles
		if ( result != FMOD.RESULT.OK ) return;

		if ( state != FMOD.Studio.PLAYBACK_STATE.PLAYING ) return;

		currentEvent.stop( FMOD.Studio.STOP_MODE.ALLOWFADEOUT );
#else
		// todo
#endif
	}

	/// <summary>
	/// Applies a radial physics impulse, like a shockwave. 
	/// Nullable vector3 can be left as null to assume the caller npcs origin point
	/// OR assigned a position. Could be cool for footsteps of large npcs or whatever.
	/// </summary>
	/// <param name="maxRadius"></param>
	/// <param name="position"></param>
	public void ApplyRadialPhysImpulse( float maxRadius, Vector3? position = null )
	{
		Vector3 origin = position ?? WorldPosition;

		var objects = Scene.FindInPhysics( new Sphere( origin, maxRadius ) );
		foreach ( var obj in objects.Select( x => x.Components.Get<GameProp>() ).Where( x => x is not null ) )
		{
			Log.Info( $"Found phys: {obj.GameObject.Name}" );
			Gizmo.Draw.LineSphere( origin, maxRadius );

			if ( obj.GameObject == GameObject || obj.IsStatic ) { continue; }

			float dist = origin.Distance( obj.WorldPosition );
			float t = Math.Clamp( dist / maxRadius, 0f, 1f );
			float falloff = 1f - t;

			Vector3 dir = (obj.WorldPosition - origin).Normal;
			float impulseStrength = (MathF.Sqrt( obj.GetComponent<Rigidbody>().Mass )) * falloff;
			obj.PassImpulse( dir * impulseStrength );
			Log.Info( $"applied force of {dir * impulseStrength} to {obj}" );
		}
	}

	/// <summary>
	/// Marks the specified bullseye as the current enemy target.
	/// </summary>
	/// <param name="bullseye">The bullseye to be set as the enemy target. Cannot be null.</param>
	public void SetBullseyeAsEnemy( Bullseye bullseye )
	{
		currentBullseye = bullseye;
		shouldAttackBullseye = true;
	}

	public DamageInfo lastDamageInfo = null;


	[ConVar( "ai_physqueue" )] public static bool usePhysQueue { get; set; } = true;
	/// <summary>
	/// Handles the application of damage to the NPC, updating its state and triggering appropriate responses.
	/// </summary>
	/// <param name="dmginfo"></param>
	public void OnDamage( in DamageInfo dmginfo )
	{
		if ( !IsAlive )
			return;

		if ( curHealth <= 0 )
		{
			KillNPCOnNextUpdate();
			return;
		}

		if ( _Cine.IsValid() && !_Cine.AllowActorDeath )
			dmginfo.Damage = 0;

#if FMOD
		if ( currentEvent.isValid() ) StopAllNPCSounds();
#endif
		if ( dmginfo.Tags.Contains( "impact" ) && usePhysQueue ) // send physics calls into a queue
		{
			aiManager.QueuePhysicsImpact( dmginfo, this );
			return;
		}
		Log.Info( $"NPC OnDamage {TargetName} | dmg: {dmginfo.Damage} | tags: {dmginfo.Tags?.ToString()} | attacker {dmginfo.Attacker?.Name}" );

		painSensor.InflictPain( dmginfo );
		painSensor.ShouldUpdateWorldState = true;
		Vector3 vecDir = Vector3.Zero; // default: unknown direction

		if ( dmginfo.Attacker.IsValid() )
		{

			vecDir = (dmginfo.Position - dmginfo.Attacker.WorldPosition).Normal;
		}
		else if ( dmginfo.Origin != Vector3.Zero )
		{
			vecDir = (dmginfo.Position - dmginfo.Origin).Normal;
		}

		SpawnBlood( vecDir, dmginfo );
		FaceTarget( vecDir, 4f );
		LastAttackDir = -vecDir;

		BodyModel.Set( "b_Interrupt", true ); // flinch test

		if ( dmginfo.Damage < maxHealth * .25f )
		{

			WorldState.Set( AIFacts.LightDamage, true );
			_lightDamageExpiry = Time.Now + 0.5f;
		}
		else
		{
			WorldState.Set( AIFacts.HeavyDamage, true );
			_heavyDamageExpiry = Time.Now + 0.5f;
		}

		curHealth -= dmginfo.Damage;
		var d = dmginfo;
		Damaged?.Invoke( dmginfo );

		lastDamageInfo = dmginfo;


	}

	/// <summary>
	/// Applies the last recorded hit to the ragdoll, simulating the impact based on the damage and direction of the
	/// attack.
	/// </summary>
	public void ApplyLastHitToRagdoll()
	{
		Rigidbody targetBody = null;

		// Find the closest physics body to the hit position since Shape is always null for projectiles
		if ( lastDamageInfo.Position != Vector3.Zero )
		{
			var closest = PhysModel.Bodies
				.Where( b => b.Component?.PhysicsBody != null )
				.OrderBy( b => b.Component.PhysicsBody.MassCenter.Distance( lastDamageInfo.Position ) )
				.FirstOrDefault();

			Log.Info( $"[Ragdoll] Closest body to hit pos {lastDamageInfo.Position}: {closest.Component?.GameObject.Name ?? "NULL"}" );
			targetBody = closest.Component;
		}

		targetBody ??= PhysModel.Bodies.FirstOrDefault().Component;

		if ( targetBody != null )
		{
			Log.Info( $"[Ragdoll] Applying impulse to {targetBody} | Force: {LastAttackDir * lastDamageInfo.Damage * 25f}" );
			targetBody.ApplyImpulseAt( lastDamageInfo.Position, LastAttackDir * lastDamageInfo.Damage * 25f );
		}
		else
		{
			Log.Error( "[Ragdoll] No valid PhysicsBody found" );
		}
	}

	/// <summary>
	/// Sets the NPC to be killed on the next tick. Kills it if its set.
	/// </summary>
	public void KillNPCOnNextUpdate()
	{

		if ( KillOnNextUpdate )
			EventKilled();
		else
			KillOnNextUpdate = true;
	}

	/// <summary>
	/// Handles the death of an NPC.
	/// </summary>
	public void EventKilled()
	{
		BodyModel.Set( "b_dead", true );
		BodyModel.UseAnimGraph = false; // kind of a hacky way to get animations to stop playing once the npc has died. but it works!
		if ( !IsAlive ) return;

		Collider.Enabled = false;
		Tags.Remove( "npc" );

		Tags.Add( "ragdoll" );
		Tags.Add( "solid_playerpass" );
		PhysModel.Enabled = true;
		PhysModel.MotionEnabled = true;

		if ( PhysModel.MotionEnabled && moveType != MoveType.MOVE_NONE ) // dont apply to static npcs... but we may in the future if we have dangly bits when theyre dead. like xen trees or something
			ApplyLastHitToRagdoll();

		PlayDeathSound();
		Relationships.Destroy(); // what the fuck?
								 //aiCorpse.CreateCorpse( BodyModel, PhysModel );

		IsAlive = false;
		aiSquad?.NotifySquadMemberDead();
		aiManager.RemoveAI( this ); // this actually removes npc from the squad, removes it from the manager list, and destroys the gameobject
	}
	/// <summary>
	/// This is an extremely bad place to put this. Perhaps we need an AIEvent module for these types of interactions in the future 
	/// </summary>
	/// <param name="barnacle"></param>
	public void HandleBarnacleGrab( AIController barnacle )
	{
		PhysModel.MotionEnabled = true;
		SetAIState( AI_BehaviorState.BEHAVIORSTATE_SCRIPTED ); // disables goap

	}

	/// <summary>
	/// Sets a behavior state on the NPC. This can be GOAP or scripted
	/// </summary>
	/// <param name="state"></param>
	public void SetAIState( AI_BehaviorState state ) => aiBrain.aiState = state;

	/// <summary>
	/// Runs decision making based on the NPCs behavior state
	/// </summary>
	void RunDecisionMaking()
	{
		switch ( aiBrain.aiState )
		{
			case AI_BehaviorState.BEHAVIORSTATE_DEFAULT:
				HandleGOAPLoop();
				break;
			case AI_BehaviorState.BEHAVIORSTATE_SCRIPTED:
				HandleScripting();
				break;
		}
	}

	/// <summary>
	/// Think System version 2 idea
	/// ------------------------------------------------------------------------------------------
	///	Task style appraoch with clean seperation of info gathering and execution
	/// Yield and distribute ticks across all agents instead of ticking each one fully every frame
	/// This should probably be handled within the game manager, instead of relying on the components fixed update
	/// This allows the manager to handle its own timing and ticking of ai
	/// </summary>
#if !OLD_THINK
	// The three steps of an AI tick
	public enum ThinkPhase
	{
		Sense,
		Decide,
		Execute
	}

	public ThinkPhase CurrentPhase { get; set; } = ThinkPhase.Sense;
	public BaseEntity _frameEnemy;
	public MoveType _frameMoveType;

	/// <summary>
	/// The first tick step. Ticks sensors and writes to the world state
	/// </summary>
	/// <param name="deltaTime"></param>
	public void TickSense( float deltaTime )
	{
		iSpy.iSpyStartProfile( "TickStep_1" );

		aiBrain._lastThinkTime = Time.Now;
		aiBrain._aiLOD = aiBrain.DetermineAILOD();
		aiBrain._currentThinkRate = aiBrain.DetermineThinkRate();

		if ( painSensor is not null )
		{
			painSensor.UpdatePacket();

			WorldState.Set( AIFacts.HighPain, painSensor.GetOutputPacketData().painIsHigh );
			WorldState.Set( AIFacts.MediumPain, painSensor.GetOutputPacketData().painIsMedium );
			WorldState.Set( AIFacts.LowPain, painSensor.GetOutputPacketData().painIsLow );
		}

		if ( touchSensor is not null )
		{

			touchSensor.UpdatePacket();

			WorldState.Set( AIFacts.TouchingPlayer, touchSensor.GetOutputPacketData().touchingPlayer );
			WorldState.Set( AIFacts.TouchingFriend, touchSensor.GetOutputPacketData().touchingFriend );
			WorldState.Set( AIFacts.TouchingEnemy, touchSensor.GetOutputPacketData().touchingEnemy );
		}

		if ( targetingSensor is not null )
		{
			targetingSensor.PerformSensing();
		}

		if ( scentSensor is not null )
		{
			scentSensor.UpdatePacket();

			WorldState.Set( AIFacts.ScentDetected, scentSensor.GetOutputPacketData().AnyDetected );
			WorldState.Set( AIFacts.ScentInvestigated, false );
		}

		if ( squadSensor is not null )
		{
			squadSensor.UpdatePacket();

			WorldState.Set( AIFacts.IsSquadLeader, squadSensor.GetOutputPacketData().IsSquadLeader );
			WorldState.Set( AIFacts.SquadHasEnemyContact, squadSensor.GetOutputPacketData().SquadHasEnemyContact );
			WorldState.Set( AIFacts.LeaderDistanceOk, squadSensor.GetOutputPacketData().LeaderDistanceOK );
			WorldState.Set( AIFacts.SquadCohesionOK, squadSensor.GetOutputPacketData().SquadCohesionOK );
			WorldState.Set( AIFacts.SquadLeaderAlive, squadSensor.GetOutputPacketData().SquadLeaderAlive );
			WorldState.Set( AIFacts.SquadIsBroken, squadSensor.GetOutputPacketData().SquadIsBroken );

			WorldState.Set( AIFacts.IsSquadLeader, squadSensor.GetOutputPacketData().IsSquadLeader );
		}

		DetermineThreatLevel();

		// snapshot enemy state for downstream phases to read consistently
		_frameEnemy = Blackboard.activeEnemy;
		_frameMoveType = moveType;

		CurrentPhase = ThinkPhase.Decide;
		iSpy.iSpyEndProfile();
	}

	/// <summary>
	/// Tick step 2. Runs GOAP and ticks the behavior module
	/// </summary>
	/// <param name="deltaTime"></param>
	public void TickDecide( float deltaTime )
	{
		iSpy.iSpyStartProfile( "TickStep_2" );

		if ( _frameEnemy is not null )
			WorldState.Set( AIFacts.ThreatEliminated, false );

		RunDecisionMaking();

		BehaviorModule?.Tick();

		CurrentPhase = ThinkPhase.Execute;
		iSpy.iSpyEndProfile();

	}

	/// <summary>
	/// Checks movement, handles facing, updates animgraph, emits sounds, and resets timed conditions.
	/// </summary>
	/// <param name="deltaTime"></param>
	public void TickExecute( float deltaTime )
	{
		iSpy.iSpyStartProfile( "TickStep_3" );

		if ( _frameEnemy is not null )
		{
			if ( WorldState.Get( AIFacts.SearchingForEnemy ) )
				FaceTarget( enemyLKP, 5f );
			else if ( _frameMoveType != MoveType.MOVE_NONE )
				FaceTarget( _frameEnemy.WorldPosition, 5f );
		}

		if ( isMoving ) CheckMovement();

		UpdateSearchState();
		ResetTimedConditions();
		UpdateAnimgraphParamaters();
		HandleEmittedSounds();
		CurrentPhase = ThinkPhase.Sense; // ready for next full cycle
		iSpy.iSpyEndProfile();

	}
#endif

	private float nextIdleSoundTime = 0;
	private float nextAlertSoundTime = 0;

	void HandleEmittedSounds()
	{
		if ( WorldState.Get( AIFacts.Alert ) is false && Time.Now >= nextIdleSoundTime )
		{
			PlayIdleSound();
			nextIdleSoundTime = Time.Now + Game.Random.Float( Definition.MinIdleSoundRefire, Definition.MaxIdleSoundRefire );
		}
		else if ( WorldState.Get( AIFacts.Alert ) is true && Time.Now >= nextAlertSoundTime )
		{
			PlayAlertSound();
			nextAlertSoundTime = Time.Now + Game.Random.Float( Definition.MinAlertSoundRefire, Definition.MaxAlertSoundRefire );
		}
	}



	public void PlayIdleSound()
	{
		var sound = Definition.IdleSounds;

#if FMOD
		if ( !sound.IsValid() ) return;

		var snd = FMODSound.Play( sound, GameObject );
		SetCurrentFMODEvent( snd );

		BehaviorModule.HandleSoundEmitting( sound );
		SetCurrentFMODEvent( sound );
#else
		var snd = GameObject.PlaySound( Definition.IdleSounds );
#endif

	}
	public void PlayDeathSound()
	{
#if FMOD
		var sound = Definition.DeathSounds;
		if ( !sound.IsValid() ) return;

		var snd = FMODSound.Play( sound, GameObject );
		SetCurrentFMODEvent( snd );

		BehaviorModule.HandleSoundEmitting( sound );
		SetCurrentFMODEvent( sound );
#else
		var snd = GameObject.PlaySound( Definition.DeathSounds );
#endif
	}
	public void PlayRangeAttack1Sound()
	{
#if FMOD
		var sound = Definition.RangeAttack1Sound;
		if ( !sound.IsValid() ) return;

		var snd = FMODSound.Play( sound, GameObject );
		SetCurrentFMODEvent( snd );

		BehaviorModule.HandleSoundEmitting( sound );
		SetCurrentFMODEvent( sound );
#else
		var snd = GameObject.PlaySound( Definition.IdleSounds );
#endif
	}

	public void PlayRangeAttack1SecondarySound()
	{
		var sound = Definition.RangeAttack1SecondarySound;
#if FMOD
		if ( !sound.IsValid() ) return;

		var snd = FMODSound.Play( sound, GameObject );
		SetCurrentFMODEvent( snd );

		BehaviorModule.HandleSoundEmitting( sound );
		SetCurrentFMODEvent( sound);
#else
		var snd = GameObject.PlaySound( Definition.IdleSounds );
#endif
	}

	public void PlayAlertSound()
	{
		var sound = Definition.AlertSounds;
#if FMOD
		if ( !sound.IsValid() ) return;

		var snd = FMODSound.Play( sound, GameObject );
		SetCurrentFMODEvent( snd );

		BehaviorModule.HandleSoundEmitting( sound );
			SetCurrentFMODEvent( sound);
#else
		var snd = GameObject.PlaySound( sound );
#endif

	}

	/// <summary>
	/// Collects the abilities from the definition and adds them
	/// </summary>
	public void CollectAbilities()
	{
		foreach ( var act in Definition.Abilities )
		{
			switch ( act )
			{
				case NpcDefinition.AbilityList.ABILITY_BLINK:
					Abilities.Add( new Blink( this ) );
					break;
				case NpcDefinition.AbilityList.ABILITY_JUMP:
					Abilities.Add( new Jump( this ) );
					break;

				default:
					break;
			}

		}
	}

	/// <summary>
	/// Ticks the abilities this NPC has defined
	/// </summary>
	public void TickAbilities()
	{

		foreach ( var ability in Abilities )
		{
			ability.Tick();
		}

	}

	/// <summary>
	/// Applies the worldstate facts determined by the threat eval sensor
	/// </summary>
	public void DetermineThreatLevel()
	{
		iSpy.iSpyStartProfile( "ThreatLevel_Step1" );

		threatEvaluator.Update( this );

		WorldState.Set( AIFacts.EnemyThreatHigh, threatEvaluator.ThreatHigh );
		WorldState.Set( AIFacts.EnemyThreatLow, threatEvaluator.ThreatLow );

		iSpy.iSpyEndProfile();

	}

	/// <summary>
	/// Handles planning and action dispatch.
	/// </summary>
	/// <param name="showDebug"></param>
	public void HandleGOAPLoop( bool showDebug = false )
	{

		if ( currentGoal is not null && ShouldReplan() )
		{
			currentAction?.OnExit( this );
			currentAction = null;
			currentPlan?.Clear();
			currentGoal = null;
		}

		if ( currentAction is null && (currentPlan is null || currentPlan.Count == 0 || currentGoal is null) )
		{
			Plan();
			planStep = 0;
			return;
		}

		// Start new action
		if ( currentAction is null && currentPlan.Count > 0 )
		{
			var next = currentPlan[0];

			// Validate against real world state before starting
			if ( !WorldState.Satisfies( next.Preconditions ) )
			{
				if ( DebugPlanning )
					Log.Warning( $"[GOAP] {next.GetType().Name} preconditions not met in real world! replanning." );
				currentPlan.Clear();
				currentGoal = null;
				return;
			}

			if ( !next.CheckProceduralPrecondition( this ) )
			{
				if ( DebugPlanning )
					Log.Warning( $"[GOAP] {next.GetType().Name} procederal check failed! replanning." );
				currentPlan.Clear();
				currentGoal = null;
				return;
			}

			planStep++;
			currentAction = currentPlan[0];
			currentPlan.RemoveAt( 0 );
			currentAction.OnEnter( this );
		}

		// Perform action
		if ( currentAction is not null )
		{
			currentAction.Perform( this );

			if ( currentAction.IsFailed() )
			{
				if ( DebugPlanning )
					Log.Warning( $"[GOAP] {currentAction.GetType().Name} IsFailed returned true, replanning." );
				currentAction.OnExit( this );
				currentAction = null;
				currentPlan?.Clear();
				currentGoal = null;
				return;
			}

			if ( currentAction.IsDone() )
			{
				currentAction.ApplyEffects( WorldState );
				currentAction.OnExit( this );
				currentAction = null;
			}
		}
	}

	/// <summary>
	/// Uses GOAP to build a plan based on our goals and current worldstate
	/// </summary>
	private void Plan()
	{
		if ( Goals is null || Goals.Count == 0 || WorldState is null )
		{
			currentGoal = null;
			currentGoalName = "<none>";
			PopulateGoals();
			return;
		}

		currentGoal = null;
		float bestPriority = float.MinValue;

		foreach ( var g in Goals )
		{
			if ( g is null )
				continue;

			if ( WorldState.Satisfies( g.DesiredState ) )
				continue;

			if ( g.Priority > bestPriority )
			{
				bestPriority = g.Priority;
				currentGoal = g;
			}
		}

		currentGoalName = currentGoal is not null ? currentGoal.Name : "<none>";

		if ( currentGoal is null )
		{
			if ( DebugPlanning )
				Log.Info( "[GOAP] No valid goals." );

			return;
		}

		// Find plan to satisfy goal
		currentPlan = planner.Plan( this, WorldState.facts, Actions, currentGoal.DesiredState );
		currentAction = null;

		if ( currentPlan is null )
		{
			if ( DebugPlanning )
			{
				Log.Warning( $"[GOAP] Planner failed for goal {currentGoal.Name}" );
			}
			currentGoal = null;
			return;
		}

		// save a snapshjot of the worldstate so we can assess it later and decide if a fact changed that requires replanning
		_stateAtPlanTime = WorldState.facts.ToDictionary( f => f.Name, f => f.Value );

		if ( DebugPlanning )
		{
			var planNames = currentPlan.Select( a => a.GetType().Name );
			Log.Info( $"[GOAP] New plan for goal {currentGoal.Name}: {string.Join( " -> ", planNames )}" );
		}
	}
	/// <summary>
	/// Returns true if this NPC should replan.
	/// Replan is triggered by replan trigger facts,
	/// which should really be defined per npc.
	/// </summary>
	/// <returns></returns>
	public bool ShouldReplan()
	{
		if ( currentGoal is not null && WorldState.Satisfies( currentGoal.DesiredState ) )
			return true;

		foreach ( var fact in _replanTriggerFacts )
		{
			if ( WorldState.TryGet( fact, out var current ) &&
				_stateAtPlanTime.TryGetValue( fact, out var atPlan ) &&
				current != atPlan )
				return true;
		}

		return false;
	}
	// this should probably be defined per definition
	private readonly string[] _replanTriggerFacts =
{
	 AIFacts.Alert, AIFacts.SquadIsBroken, AIFacts.Bored, AIFacts.SquadCohesionOK, AIFacts.IsBored, AIFacts.LowPain, AIFacts.MediumPain, AIFacts.HighPain, AIFacts.FriendDied
};

	private Dictionary<string, bool> _stateAtPlanTime = new();

	/// <summary>
	/// Updates the world state when the ai is searching. feels a bit weird. this should prob be handled in a sensor
	/// </summary>
	private void UpdateSearchState()
	{
		bool isSearching = WorldState.Get( AIFacts.SearchingForEnemy );
		bool hasSearchGoal = Goals.Any( g => g.Name == "FindEnemy" );

		if ( isSearching && !hasSearchGoal )
		{
			Goals.Add( new Goal(
				"FindEnemy",
				new List<WorldFact> { new( AIFacts.EnemyVisible, true ) },
				priority: 95f  // higher than threatEliminated so it runs first
			) );

		}
		else if ( !isSearching && hasSearchGoal )
		{
			Goals.RemoveAll( g => g.Name == "FindEnemy" );

		}
	}

	/// <summary>
	/// Writes to the worldstate that a friend has died
	/// </summary>
	public void FriendDead() => WorldState.Set( AIFacts.FriendDied, true );

	private Vector3? forcedMovePosition;

	/// <summary>
	/// Called by npc_go to set a scripted movement to a position.
	/// </summary>
	/// <param name="pos"></param>
	public void SetForcedMovePosition( Vector3? pos )
	{
		if ( forcedMovePosition != null )
		{
			ClearForcedMovePosition();
			return;
		}

		forcedMovePosition = pos;

		debugPathCreated = false;
		debugPath = default;
		followingForcedMovementPath = false;
		forcedMoveComplete = false;
	}

	/// <summary>
	/// Clears the scripted movement
	/// </summary>
	public void ClearForcedMovePosition()
	{
		forcedMovePosition = null;

		// Clear the line and reset path state
		if ( pathLine != null )
		{
			pathLine.VectorPoints.Clear();
		}
		debugPathCreated = false;
		debugPath = default;

		Agent.Stop();
		SetAIState( AI_BehaviorState.BEHAVIORSTATE_DEFAULT );
		hasReachedCine = true;
		currentMoveGoal = GoalType.GOALTYPE_NONE;
		_scriptContext = ScriptingContext.SCRIPT_SEQUENCE;
	}

	/// <summary>
	/// Creates a NavMesh path. Used for npc_go
	/// </summary>
	/// <param name="target"></param>
	/// <returns></returns>
	public NavMeshPath CreateNavMeshPath( Vector3? target )
	{
		var pathReq = new CalculatePathRequest();
		pathReq.Start = WorldPosition;
		pathReq.Agent = Agent;
		pathReq.Target = target.Value;

		var finalPath = GameObject.Scene.NavMesh.CalculatePath( pathReq );
		Agent.SetPath( finalPath );

		return finalPath;
	}

	private LineRenderer pathLine;
	private bool debugPathCreated = false;
	private NavMeshPath debugPath;
	public bool followingForcedMovementPath = false;
	public bool forcedMoveComplete = false;
	public void HandleScripting()
	{
		NavData data = new NavData();

		switch ( _scriptContext )
		{

			case ScriptingContext.SCRIPT_SEQUENCE:
				if ( shouldMoveToCine && !hasReachedCine )
				{

					//	data.Controller = this;
					//data.position = _Cine.WorldPosition;
					//data.goalType = GoalType.GOALTYPE_CINE;
					DoMovement( _Cine.WorldPosition, GoalType.GOALTYPE_CINE );
				}
				break;

			case ScriptingContext.SCRIPT_FORCED_MOVE:
				followingForcedMovementPath = true;
				//data.Controller = this;
				//data.position = forcedMovePosition.Value;
				//data.goalType = GoalType.GOALTYPE_LOCATION;
				DoMovement( forcedMovePosition.Value, GoalType.GOALTYPE_LOCATION );

				if ( !debugPathCreated )
				{
					pathLine = Components.Create<LineRenderer>();
					pathLine.Width = 1;
					pathLine.SplineInterpolation = 1;
					pathLine.EndCap = SceneLineObject.CapStyle.Arrow;
					pathLine.UseVectorPoints = true;
					pathLine.VectorPoints = new List<Vector3>();
					debugPathCreated = true;
				}



				if ( debugPath.IsValid() )
				{

					if ( debugPath.Points != null )
					{
						pathLine.VectorPoints.Clear();
						foreach ( var point in debugPath.Points )
						{
							pathLine.VectorPoints.Add( point.Position );
						}
					}
				}
				else
				{
					var path = CreateNavMeshPath( forcedMovePosition );
					debugPath = path;
				}



				Gizmo.Draw.LineCircle( forcedMovePosition.Value, 15 );


				break;
			case ScriptingContext.SCRIPT_PATH:

				break;

			case ScriptingContext.SCRIPT_CHOREO:
				// Some day!
				break;

			case ScriptingContext.SCRIPT_FOLLOW_ENTITY:
				// Some day!
				break;

			default:
				// No context, just go to default
				aiBrain.aiState = AI_BehaviorState.BEHAVIORSTATE_DEFAULT;
				break;
		}
	}

	/// <summary>
	/// TODO rethink how this works, dont call for now
	/// </summary>
	/// <param name="data"></param>
	/// <returns></returns>
	public bool TryRequestMove( NavData data )
	{
		float now = Time.Now;

		if ( now < NextNavAllowedTime )
			return false;


		float distSq = LastNavTarget.DistanceSquared( data.position );
		if ( distSq < 16f * 16f )
			return false;

		float dist = MathF.Sqrt( distSq );
		float dist01 = Math.Min( dist / _maxNavDistance, 1f );


		float npcPressure = AIManager.Current.GetNavPressureFactor();
		float pressureScale = MathX.Lerp( 0.75f, 1.5f, npcPressure );

		float playerDist = data.position.Distance( Blackboard.playerReference.WorldPosition );
		float playerDist01 = Math.Clamp( (playerDist - _navPlayerNearDist) / (_navPlayerFarDist - _navPlayerNearDist), 0f, 1f );

		float playerScale = MathX.Lerp( 0.1f, 2f, playerDist01 );

		float cooldown = pressureScale * playerScale;

		LastNavTarget = data.position;
		NextNavAllowedTime = now + cooldown;

		Navigation.NavigationDoMovement( data );
		return true;
	}

	/// <summary>
	/// Figure out or relation with a given faction, and if none default to neutral.
	/// </summary>
	/// <param name="self"></param>
	/// <param name="other"></param>
	/// <returns></returns>
	public Relation GetRelationOrNeutral( string self, string other )
	{
		if ( Relationships.Relations.TryGetValue( self, out var inner ) &&
			 inner.TryGetValue( other, out var r ) )
			return r;

		return Relation.NEUTRAL;
	}

	public bool HasArrivedAtGoal() => Agent.WorldPosition == Blackboard._currentMovePos;
	public bool HasArrivedAtCineGoal() => (Agent.WorldPosition - _Cine.WorldPosition).Length <= 5;

	/// <summary>
	/// Checks if we've reached our goal and handles setting the goaltype. 
	/// </summary>
	public void CheckMovement()
	{

		if ( shouldMoveToCine && hasReachedCine ) // TODO move this to navigation as well
		{
			Log.Info( $"Scripted Sequence Reached by NPC" );
			hasReachedCine = true;
			currentMoveGoal = GoalType.GOALTYPE_NONE;
			Navigation.NavigationStopMovement();
		}
		else if ( shouldMoveToCine )
		{
			Navigation.NavigationCheckMovement( shouldMoveToCine );

		}
		else if ( followingForcedMovementPath )
		{
			Navigation.NavigationCheckMovement();

		}

		Navigation.NavigationCheckMovement();

	}



	/// <summary>
	/// This is the method to be used for all AI movement
	/// </summary>
	/// <param name="movePos"></param>
	/// <param name="goal"></param>
	public virtual void DoMovement( Vector3? movePos, GoalType goal )
	{
		if ( !canMove )
			return;

		currentMoveGoal = goal;

		Vector3? finalMovePos = BehaviorModule?.OverrideMove( movePos ); // movement can be overridden, yay

		if ( finalMovePos.HasValue )
		{
			//Blackboard._currentMovePos = finalMovePos.Value;
			//Agent.MoveTo( finalMovePos.Value );

			NavData data = new NavData();

			data.Controller = this;
			data.position = finalMovePos.Value;
			data.goalType = goal;

			Navigation.NavigationDoMovement( data );

			//TryRequestMove( data );
		}
	}

	/// <summary>
	/// Logs the worldstate for this AI instance
	/// </summary>
	public void DebugWorldState()
	{
		Log.Info( $"Logging WorldState for {this}" );
		foreach ( var fact in WorldState.facts )
		{

			Log.Info( $"Fact: {fact.Name} State: {fact.Value}" );

		}
	}

	/// <summary>
	/// Updates perpetual animgraph parameters
	/// </summary>
	public void UpdateAnimgraphParamaters()
	{
		if ( moveType == MoveType.MOVE_GROUND || moveType == MoveType.MOVE_CRAWL )
		{
			BodyModel.Set( "b_IsMoving", Agent.Velocity.Length > 0 );
			BodyModel.Set( "f_MoveVelocity", MathX.Lerp( 0, Agent.Velocity.Length, Time.Delta ) );
		}
	}

	/// <summary>
	/// Sleeps the NPC. This disables rendering and thinking.
	/// </summary>
	public void SleepNPC()
	{
		currentSleepState = SleepState.SLEEPSTATE_WAIT_FOR_INPUT;
		BodyModel.Enabled = false;
	}

	/// <summary>
	/// Pulls an npc out of its current Sleepmode.
	/// </summary>
	public void WakeNPC()
	{
		currentSleepState = SleepState.SLEEPSTATE_NONE;
		BodyModel.Enabled = true;

	}

	private Vector3? _formationTarget;
	private bool _inFormation;

	public void SetFormationTarget( Vector3 worldPos )
	{
		_formationTarget = worldPos;
		_inFormation = true;
	}

	public void ClearFormationTarget()
	{
		_formationTarget = null;
		_inFormation = false;
	}

	protected override void DrawGizmos() => base.DrawGizmos();

	protected override void OnUpdate()
	{
		if ( AIManager.AIDebugVisionSensing )
			targetingSensor.DrawGizmos();


		base.OnUpdate();
	}

	protected override void OnFixedUpdate()
	{
		// this is a switch because ill add more sleep states later
		switch ( currentSleepState )
		{
			case SleepState.SLEEPSTATE_NONE:

				DecayLookTargets( Time.Delta );
				UpdateFacing( lookAtTarget );
				TickAbilities();


				if ( AIManager.AIDebugLogPlanning )
					DebugWorldState();

				break;

			case SleepState.SLEEPSTATE_WAIT_FOR_INPUT:
				return;
		}
	}

	/// <summary>
	/// Reduces the priority of potential look targets over time and prunes targets at zero priority.
	/// </summary>
	/// <param name="dt"></param>
	void DecayLookTargets( float dt )
	{
		for ( int i = PotentialLookTargets.Count - 1; i >= 0; i-- )
		{
			var t = PotentialLookTargets[i];
			t.Priority -= dt;

			if ( t.Priority <= 0f )
				PotentialLookTargets.RemoveAt( i );
			else
				PotentialLookTargets[i] = t;
		}
	}

	/// <summary>
	/// determines if the NPC should be oriented to face a look target, or their movement direction.
	/// </summary>
	void UpdateLookTargetGate()
	{
		if ( IsNPCStatic() )
		{
			currentLookTarget = null; // we cant move, so we dont care to try and watch the player.. yet. Maybe we will need a solution for things like Xen fauna if we ever have them
			return;
		}

		// Any real movement clears look target.. this may need to be reconsidered
		if ( currentLookTarget.HasValue && (isMoving) )
		{
			currentLookTarget = null;
		}
	}

	/// <summary>
	/// Returns true if an NPC is static.
	/// </summary>
	/// <returns></returns>
	public bool IsNPCStatic() => moveType == MoveType.MOVE_NONE && staticEnemy;

	/// <summary>
	/// Gets the eye attachment name
	/// </summary>
	/// <returns></returns>
	public string GetEyeAttachmentName() => Definition.EyeAttachment;

	/// <summary>
	/// Assigns a target vector for the AI to face. Set LookPos to null for it to face the move target instead (default behavior)
	/// </summary>
	/// <param name="LookPos"></param>
	/// <param name="priority"></param>
	/// 
	public void FaceTarget( Vector3? LookPos, float? priority )
	{

		if ( IsNPCStatic() )
			return;

		if ( Agent.Velocity.Length > 1 || !LookPos.HasValue || !priority.HasValue )
		{
			lookAtTarget = false;
			return;
		}

		PotentialLookTargets.Add( new LookTarget
		{
			Position = LookPos.Value,
			Priority = priority.Value
		} );

		lookAtTarget = true;

	}

	/// <summary>
	/// Chooses the highest priority look target from our pool of potential targets.
	/// </summary>
	public void SelectBestLookTarget()
	{
		if ( PotentialLookTargets.Count == 0 )
		{
			currentLookTarget = null;
			return;
		}

		LookTarget best = default;
		float bestScore = float.MinValue;
		bool found = false;

		foreach ( var t in PotentialLookTargets )
		{
			if ( !found || t.Priority > bestScore )
			{
				best = t;
				bestScore = t.Priority;
				found = true;
			}
		}

		currentLookTarget = found ? best : null;
	}

	/// <summary>
	/// Updates the entity's facing direction based on its current state and movement
	/// <param name="lookAtTarget"></param>
	public void UpdateFacing( bool lookAtTarget )
	{
		if ( staticEnemy )
			return; // static npcs do not face enemies

		UpdateLookTargetGate();
		SelectBestLookTarget();

		if ( lookAtTarget && currentLookTarget.HasValue && currentLookTarget is not null && !isMoving )
		{
			var look = currentLookTarget.Value;
			Vector3 toTarget = look.Position - WorldPosition;

			// remoove z component, apparentally i didnt notice this.
			toTarget.z = 0f;
			if ( toTarget.LengthSquared < 0.0001f )
				return;

			toTarget = toTarget.Normal;

			var targetRotation = Rotation.LookAt( toTarget, Vector3.Up );

			WorldRotation = Rotation.Lerp(
				WorldRotation,
				targetRotation,
				Time.Delta * TurnSpeed
			);
		}

		else if ( (moveType == MoveType.MOVE_GROUND) || (moveType == MoveType.MOVE_CRAWL) )
		{
			var velocity = Agent.Velocity;
			velocity.z = 0f;

			if ( velocity.LengthSquared > 0.01f )
			{
				var moveDirection = velocity.Normal;

				var targetRotation = Rotation.LookAt( moveDirection, Vector3.Up );

				WorldRotation = Rotation.Lerp(
					WorldRotation,
					targetRotation,
					Time.Delta * TurnSpeed
				);
			}
		}

	}
	protected override string GetEditorVis()
	{
		if ( Game.IsPlaying )
			return string.Empty;

		return Definition?.Models?.First().ResourcePath ?? "models/humans/basemesh_male.vmdl";
	}

	protected override void EntityDefaultGizmo( string editorVis, bool isModel )
	{
		Gizmo.Draw.Color = Color.White;
		var editorvis = GetEditorVis();

		if ( editorvis is null ) return;

		var vmdl = Model.Load( editorvis );
		Gizmo.Hitbox.Model( vmdl );
		Gizmo.Draw.Model( vmdl ).Flags.CastShadows = true;

		Color currentColor = Gizmo.Draw.Model( vmdl ).ColorTint;
		if ( currentSleepState > 0 )
			currentColor.a = 0.1f;

		Gizmo.Draw.Model( vmdl ).ColorTint = currentColor;

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
}
