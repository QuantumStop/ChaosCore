namespace Core;
using Sandbox.Internal;
using System;
using static NpcBrain;
using static BaseNPCConditions;
using static NpcSoundManager;
using static NpcRelations;


[Icon( "Backpack" )]
[Category( "Core" )]
public class BaseNpc : BaseEntity, Component.IDamageable
{
	public enum GoalType
	{
		GOALTYPE_NONE,
		GOALTYPE_TARGETENT,
		GOALTYPE_ENEMY,
		GOALTYPE_PATHCORNER,
		GOALTYPE_CINE,
		GOALTYPE_LOCATION,
		GOALTYPE_FLANK,
		GOALTYPE_COVER,

		GOALTYPE_INVALID
	};

	// to be implemented soon
	public enum AI_Efficiency_t
	{
		// Run at full tilt
		AIE_NORMAL,

		// Run decision process less often
		AIE_EFFICIENT,

		// Run decision process even less often, ignore other NPCs
		AIE_VERY_EFFICIENT,

		// Run decision process even less often, ignore other NPCs
		AIE_SUPER_EFFICIENT,

		// Don't run at all
		AIE_DORMANT,
	};

	public enum AI_MoveEfficiency_t
	{
		AIME_NORMAL,
		AIME_EFFICIENT,
	};

	public enum AI_SleepState
	{
		SLEEPSTATE_AWAKE, // no sleepstate, default
		SLEEPSTATE_WAITING_FOR_ENEMY,
		SLEEPSTATE_WAITING_FOR_PVS,
		SLEEPSTATE_WAITING_FOR_INPUT, // Ignore PVS
		SLEEPSTATE_AUTOPVS,
		SLEEPSTATE_AUTOPVS_AFTER_PVS,
	};

	[Property] public NpcDefinition NpcDef { get; set; }

	public AIManager aiManager { get; set; }

	private Vector3 _lastPosition;
	private Vector3 _actualVelocity;
	private Vector3 vecIdealYaw { get; set; } = Vector3.Zero;
	public PathSingle _currentPathCorner;
	//private float timeSinceLastMove; // an attempt to fix an annoying bug
	public bool canFollowMoveTarget { get; set; } = true; // set to false if were in a sleep state
	public bool hasStartedPathCornerMovement { get; set; } = false;
	public bool shouldWanderOnIdle { get; set; } = true;

	public float _ignoreDangerSoundsUntil { get; set; } = 0f;

	public bool overrideFacing { get; set; } = false;

	public AnimGraphDirectPlayback AGDirectPlayback;
	/// <summary>
	/// Enables debug information in the console.
	/// </summary>
	[Property] public bool DebugMode { get; set; } = false;

	[Property] public AI_SleepState m_sleepstate;
	[Property, ReadOnly] public GoalType currentGoalType; // this is information about our active goal
	public AI_SleepState GetSleepState() { return m_sleepstate; }
	public void SetSleepState( AI_SleepState sleepState ) { m_sleepstate = sleepState; }
	public int wakeRadius;
	public Vector3 lastAttackDir;

	public BasePlayer playerRef { get; set; }
	public Vector3 _currentTarget; // absolute current navigation target
	public bool hasWaypoint { get; set; } = false;
	public bool inCine { get; set; } = false; // For scripted sequences
	public ScriptedSequence _Cine { get; set; } = null; // For scripted sequences
	public bool shouldMoveToCine = false;
	public bool hasReachedCine = false;

	//	model config
	[Button( "Reroll apperance", Icon = "casino" ), Category( "Model Config" )]
	public void RerollModelConfig() { RerollModelIndex(); }
	public void RerollModelIndex() { ModelIndex = new Random().Int( NpcDef.Models.Count - 1 ); }
	[Property, Category( "Model Config" )] protected int ModelIndex { get; set; }
	[Property, ReadOnly, Category( "Model Config" )]
	public Model BodyMesh
	{
		get
		{
			if ( NpcDef == null )
				ModelIndex = -1;

			if ( NpcDef == null || NpcDef.Models.Count == 0 )
				return Model.Load( "models/dev/error.vmdl" );

			if ( ModelIndex < 0 || ModelIndex >= NpcDef.Models.Count )
				RerollModelIndex();

			if ( NpcDef.Models[ModelIndex] == null )
				return Model.Load( "models/dev/error.vmdl" );

			return NpcDef.Models[ModelIndex];
		}
	}
	//[Property, Model.BodyGroupMask(ModelParameter = "BodyModel"), ShowIf("HasBodyGroups", true), Category("Model Config")] public ulong BodyGroups {get; set;}
	public bool HasBodyGroups
	{
		get
		{
			if ( ModelIndex < 0 || ModelIndex >= NpcDef.Models.Count )
				return false;

			Model model = NpcDef.Models[ModelIndex];
			return model != null && model.BodyParts.Sum( ( Model.BodyPart x ) => x.Choices.Count ) > 1;
		}
	}
	//	weapons
	[Button( "Reroll weaponry", Icon = "casino" ), Category( "Weaponry" )]
	public void RerollWeaponSlots()
	{
		//		TODO: set loadout
		OnValidate();
	}
	[Property, Category( "Weaponry" )] public bool RerollWeaponSlotsOnEnabled { get; set; }
	[Property, Category( "Weaponry" )] public List<WeaponParse> WeaponData { get; set; } = new();
	//	components
	[Property, ReadOnly, Category( "Spawned Components" )] public BaseNpcAnimgraphController AnimgraphController { get; set; }
	[Property, ReadOnly, Category( "Spawned Components" )] public SkinnedModelRenderer BodyModel { get; set; }
	[Property, ReadOnly, Category( "Spawned Components" )] public ModelPhysics RagdollPhysics { get; set; }
	[Property, ReadOnly, Category( "Spawned Components" )] public ModelHitboxes Hitboxes { get; set; }
	[Property, ReadOnly, Category( "Spawned Components" )] public List<BaseNpcWeapon> Weapons { get; set; } = new();
	[Property, ReadOnly, Category( "Spawned Components" )] public NpcBrain Brain { get; set; }
	[Property, ReadOnly, Category( "Spawned Components" )] public List<BaseNpcAbility> Abilities { get; set; } = new();
	[Property, ReadOnly, Category( "Spawned Components" )] public NpcTargeting Targeting { get; set; }
	[Property, ReadOnly, Category( "Spawned Components" )] public NpcRelations Relations { get; set; }
	[Property, ReadOnly, Category( "Spawned Components" )] public NavMeshAgent Agent { get; set; } = new();
	[Property, ReadOnly, Category( "Spawned Components" )] public BaseNPCConditions Conditions { get; set; }

	[Property, ReadOnly, Category( "Spawned Components" )] public NpcSoundManager SoundMngr { get; set; }
	//	operating data
	[Property, Category( "Operating Data" )] public int IdealEquippedWeaponSlot { get; set; } = -1;
	[Property, Category( "Operating Data" )] public int IdealOffhandWeaponSlot { get; set; } = -1;
	[Property, ReadOnly, Category( "Operating Data" )] public int EquippedWeaponSlot { get; set; } = -1;
	[Property, ReadOnly, Category( "Operating Data" )] public int OffhandWeaponSlot { get; set; } = -1;
	[Property, ReadOnly, Category( "Operating Data" )] public float Health { get; set; }
	[Property, ReadOnly, Category( "Operating Data" )] public bool IsAlive { get; set; }
	[Property, ReadOnly, Category( "Operating Data" )] public float TurnSpeed { get; set; } = 8f;

	[Property, Category( "Misc NPC" )] public bool UseSimpleRootMotion { get; set; }

	public bool HasEnemy { get; set; } = false;
	public bool HasPathToEnemy { get; set; } = false;

	public List<BaseEntity> ActiveEnemies { get; private set; } = new();
	public BaseEntity CurrentEnemy { get; set; } = null;
	public Vector3 enemyLastKnownPosition { get; private set; }

	public bool shouldChaseEnemy { get; set; } = false;
	public bool chasingEnemy { get; set; } = false;

	public float nextRangeAttackTime { get; set; } = 5.0f;

	public float lastDisturbanceTime { get; set; } = -1f; // test for state transitioning
	public float calmDownTime { get; set; } = 5f; // test for state transitioning
	public bool HasTaskStarted { get; set; }
	public float WaitTime { get; set; }


	private int PreviousWeaponList;
	protected override void OnValidate()
	{
		ModelIndex = Math.Clamp( ModelIndex, 0, NpcDef.Models.Count - 1 );
		if ( Weapons.Count == WeaponData.Count )
		{
			IdealEquippedWeaponSlot = Math.Clamp( IdealEquippedWeaponSlot, -1, Weapons.Count - 1 );
			IdealOffhandWeaponSlot = Math.Clamp( IdealOffhandWeaponSlot, -1, Weapons.Count - 1 );
		}

		base.OnValidate();

		//		TODO: validate loadout


		var hash = WeaponData.GetHashCode();
		foreach ( var weapondata in WeaponData )
		{
			//			cause their has function sucks
			if ( weapondata == null )
				continue;

			hash *= weapondata.GetHashCode();
		}
		if ( !Scene.IsEditor && PreviousWeaponList != hash )
			InitWeapons();

		PreviousWeaponList = hash;

		if ( !Scene.IsEditor && Health <= 0f && IsAlive )
			OnDeath( new DamageInfo() );
	}

	protected override void DrawGizmos()
	{
		if ( Initialized )
			return;

		if ( NpcDef == null || NpcDef.Models.Count == 0 )
		{
			base.DrawGizmos();
			return;
		}

		Gizmo.Hitbox.Model( BodyMesh );
		var model = Gizmo.Draw.Model( BodyMesh );
		model.Flags.CastShadows = true;

		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.LineBBox( BodyMesh.Bounds );
		}
		else if ( Gizmo.IsHovered )
		{
			Gizmo.Draw.Color = Color.Orange.WithAlpha( (((float)Math.Sin( Time.Now * 20f )) * 0.3f) + 0.7f );
			Gizmo.Draw.LineBBox( BodyMesh.Bounds );
		}
	}

	protected override void OnStart()
	{
		aiManager = Scene.GetSystem<AIManager>();

		aiManager.AddAI( this );

		AGDirectPlayback = BodyModel.SceneModel.DirectPlayback;

	}

	public bool CheckRangeAttack1()
	{
		if ( CurrentEnemy == null )
		{
			return false;
		}

		if ( Time.Now <= nextRangeAttackTime )
		{
			return false;
		}

		if ( WorldPosition.Distance( CurrentEnemy.WorldPosition ) < 256 )
		{
			return true;
		}

		return false;
	}
	public void EvaluateKnownSounds()
	{
		if ( DebugMode )
		{
			Log.Info( "EvaluateKnownSounds: Started" );
		}
		foreach ( var known in Targeting.KnownSounds )
		{
			AIConditions condition = 0;

			var snd = known.Value;

			if ( snd.TimeToRegister <= 0f ) // if sound is already registered
			{
				switch ( snd.SoundType )
				{
					case SoundType.SOUND_GUNFIRE:
					case SoundType.SOUND_DANGER:
						if ( Time.Now > _ignoreDangerSoundsUntil )
							condition = AIConditions.COND_HEAR_DANGER;
						break;

					//	case SoundType.SOUND_THUMPER: condition = AIConditions.COND_HEAR_THUMPER; break;
					case SoundType.SOUND_COMBAT:
						/*if ( pCurrentSound->SoundChannel() == SOUNDENT_CHANNEL_SPOOKY_NOISE )
						{
							condition = AIConditions.COND_HEAR_SPOOKY;
						}
						else*/
						{
							condition = AIConditions.COND_HEAR_COMBAT;
						}
						break;

					case SoundType.SOUND_WORLD: condition = AIConditions.COND_HEAR_WORLD; break;
					case SoundType.SOUND_PLAYER: condition = AIConditions.COND_HEAR_PLAYER; break;
					case SoundType.SOUND_BULLET_IMPACT: condition = AIConditions.COND_HEAR_BULLET_IMPACT; break;
					case SoundType.SOUND_PHYSICS_DANGER: condition = AIConditions.COND_HEAR_PHYSICS_DANGER; break;
					//case SoundType.SOUND_DANGER_SNIPERONLY:/* silence warning */					break;
					//case SoundType.SOUND_MOVE_AWAY: condition = AIConditions.COND_HEAR_MOVE_AWAY; break;
					case SoundType.SOUND_PLAYER_VEHICLE: condition = AIConditions.COND_HEAR_PLAYER; break;

					default:

						break;

				}
				if ( condition != AIConditions.COND_NONE )
				{
					if ( DebugMode )
						Log.Info( $"{condition} set from sound" );

					Conditions.SetCondition( condition );
				}
			}
		}
	}


	// This is dum, will fix one day
	public bool OnReachedMoveTarget()
	{

		float distanceThreshold = 25f;
		float speedThreshold = 5f;

		bool closeEnough = BodyModel.WorldPosition.Distance( _currentTarget ) < distanceThreshold;
		bool isSlow = Agent.Velocity.Length < speedThreshold;

		if ( closeEnough )
		{
			Agent.MoveTo( PositionVector() );
			Agent.Stop();
			currentGoalType = GoalType.GOALTYPE_NONE;
		}
		//hasWaypoint = false;

		return closeEnough && isSlow;
	}

	protected override void OnEnabled()
	{
		Health = NpcDef.Health;
		IsAlive = true;
		Tags.Add( "npc" );
		enemyLastKnownPosition = Vector3.Zero;
		//	Brain.SetNextThink( 0.02f );

		playerRef = Scene.GetAllComponents<BasePlayer>().FirstOrDefault(); // I should really implement an AI manager class that holds information about the world, player, and available ais that can be accessed directly.

		_currentTarget = Vector3.Zero;

		if ( !Brain.IsValid() )
			Brain = Components.Create<NpcBrain>();

		Brain.Owner = this;

		//		TODO: give NpcRelations velocity data
		if ( !Relations.IsValid() )
			Relations = Components.Create<NpcRelations>();

		Relations.Faction = NpcDef.Faction;

		if ( !Targeting.IsValid() )
			Targeting = Components.Create<NpcTargeting>();

		Targeting.Owner = this;

		// wtf was this..
		Abilities = new List<BaseNpcAbility>();
		foreach ( var ability in NpcDef.NpcAbilities )
		{
			var typedesc = GlobalGameNamespace.TypeLibrary.GetType( ability.AbilityClassname );
			BaseNpcAbility component;

			if ( typedesc != null )
				component = (BaseNpcAbility)Components.Create( typedesc );
			else
				component = Components.Create<BaseNpcAbility>();

			component.NpcAbility = ability;
			component.Owner = this;

			Abilities.Add( component );
		}

		if ( !BodyModel.IsValid() )
			BodyModel = Components.Create<SkinnedModelRenderer>();

		BodyModel.Model = BodyMesh;
		BodyModel.OnGenericEvent = OnAnimEvent;
		BodyModel.CreateBoneObjects = true;

		if ( !AnimgraphController.IsValid() )
			AnimgraphController = (BaseNpcAnimgraphController)Components.Create( GlobalGameNamespace.TypeLibrary.GetType( NpcDef.ModelInfo.AnimgraphControllerClass ) );

		AnimgraphController.Owner = this;
		AnimgraphController.Skeleton = BodyModel;

		if ( !Hitboxes.IsValid() )
			Hitboxes = Components.Create<ModelHitboxes>();

		Hitboxes.Renderer = BodyModel;
		Hitboxes.Target = GameObject;

		if ( !RagdollPhysics.IsValid() )
			RagdollPhysics = Components.Create<ModelPhysics>();
		RagdollPhysics.Enabled = false;
		RagdollPhysics.Model = BodyModel.Model;
		RagdollPhysics.Renderer = BodyModel;
		RagdollPhysics.MotionEnabled = false;

		if ( RerollWeaponSlotsOnEnabled )
			RerollWeaponSlots();
		InitWeapons();
		//Brain.OnThink += Think;
		base.OnEnabled();
	}

	public BaseNpcWeapon GetEquippedWeapon()
	{
		if ( EquippedWeaponSlot >= 0 )
			return Weapons[EquippedWeaponSlot];
		return null;
	}

	protected virtual void OnAnimEvent( SceneModel.GenericEvent evt )
	{
		switch ( evt.Type )
		{
			case "ATTACK_MELEE_SWING_START":
				if ( GetEquippedWeapon() != null )
					GetEquippedWeapon().MeleeSwing = true;
				break;
			case "ATTACK_MELEE_SWING_END":
				if ( GetEquippedWeapon() != null )
					GetEquippedWeapon().MeleeSwing = false;
				break;
			/*case "ATTACK_SHOVE":
				UnarmedAttack_Shove();
				break;*/
			default:
				break;
		}
		foreach ( var ability in Components.GetAll<BaseNpcAbility>() )
			ability.HandleAnimEvent( evt );
	}

	public void InitWeapons()
	{
		foreach ( var weapon in Weapons )
			weapon.GameObject.Destroy();
		Weapons = new();
		foreach ( var weaponData in WeaponData )
		{
			if ( weaponData == null )
				continue;
			//TODO: keep existing weapon

			var typedesc = GlobalGameNamespace.TypeLibrary.GetType( weaponData.ResourceName + "_npc" );
			BaseNpcWeapon PrimaryWeapon;

			if ( typedesc != null )
				PrimaryWeapon = (BaseNpcWeapon)Scene.CreateObject().Components.Create( typedesc, false );
			else
				PrimaryWeapon = Scene.CreateObject().Components.Create<BaseNpcWeapon>( false );

			PrimaryWeapon.WeaponData = weaponData;
			PrimaryWeapon.Owner = this;
			PrimaryWeapon.Slot = Weapons.Count();
			PrimaryWeapon.GameObject.Name = weaponData.ResourceName;
			PrimaryWeapon.GameObject.SetParent( GameObject, false );
			PrimaryWeapon.Tags.Add( "npcweapon" );
			PrimaryWeapon.Enabled = true;

			Weapons.Add( PrimaryWeapon );
		}
	}

	public void StopTask()
	{
		HasTaskStarted = false;
		WaitTime = 0f;
	}
	public bool IsGoalSet()
	{
		return false;
	}

	public bool IsMoving()
	{
		return IsGoalSet();
	}

	public void NavCheckTarget( float stopDistance = 50f ) // This is because navmesh agent is an evil creature that likes to get stuck in strange nav spots.
	{
		if ( !hasWaypoint || Agent == null )
			return;

		float distance = PositionVector().Distance( in _currentTarget );

		if ( distance <= stopDistance )
		{
			Agent.MoveTo( PositionVector() );
			Agent.Stop();
			hasWaypoint = false;

			if ( DebugMode )
			{
				Log.Info( $"Stopped navigation. Distance to target: {distance}" );
			}

		}
	}

	[Property] public float SeparationRadius = 45f;
	[Property] public float RepulsionStrength = 15f;
	[Property] public float SteeringBlend = 0.5f;

	public void ApplyFlockingAvoidance()
	{
		var neighbors = FindNearbyNpcs();

		if ( neighbors.Count == 0 )
			return;

		Vector3 separationForce = Vector3.Zero;

		foreach ( var npc in neighbors )
		{
			var toSelf = WorldPosition - npc.WorldPosition;
			float distance = toSelf.Length;

			if ( distance < SeparationRadius && distance > 0f )
			{
				separationForce += toSelf.Normal / distance; // Stronger repulsion the closer they are
			}
		}

		if ( separationForce.Length > 0.01f )
		{
			var desiredDirection = (_currentTarget - PositionVector()).Normal;
			var finalDirection = Vector3.Lerp( desiredDirection, desiredDirection + separationForce.Normal, SteeringBlend ).Normal;

			var newTarget = PositionVector() + finalDirection * 50f;

			// grab point off navmesh using our new target
			var navTarget = Scene.NavMesh.GetClosestPoint( newTarget );
			if ( navTarget.HasValue )
			{
				DoMovement( navTarget.Value, GoalType.GOALTYPE_LOCATION );
			}
		}
	}

	private List<BaseNpc> FindNearbyNpcs()
	{
		var nearby = new List<BaseNpc>();

		foreach ( var other in Scene.GetAllComponents<BaseNpc>() )
		{
			if ( other == this ) continue;

			if ( Vector3.DistanceBetween( other.PositionVector(), PositionVector() ) < SeparationRadius )
			{
				nearby.Add( other );
			}
		}

		return nearby;
	}

	// Doesnt work how i want because i am dumb. Will improve!
	public Vector3? GetValidRandomPosition( Vector3? origin, float radius, float wallAvoidRadius, int maxAttempts = 10 )
	{
		if ( !origin.HasValue )
			return null;

		Vector3 actualOrigin = origin.Value;

		for ( int i = 0; i < maxAttempts; i++ )
		{
			var randomDirection = Vector3.Random.Normal * radius;
			var randomPos = actualOrigin + randomDirection;

			var trace = Scene.Trace.Sphere( (radius * 0.25f), randomPos + Vector3.Up * 10, randomPos - Vector3.Up * 10 )
				.WithTag( "solid" )
				.Run();
			var tes = Scene.NavMesh.GetClosestPoint( randomPos );
			if ( !trace.Hit )


				return tes;
		}

		return null;
	}


	public BasePlayer AI_GetPlayer()
	{
		return playerRef;
	}

	public virtual void DoMovement( Vector3? position, GoalType goalType )
	{
		// I should probably set the debug stuff here!
		//timeSinceLastMove = Time.Now;
		Agent.MoveTo( position.Value );
		currentGoalType = goalType;
		_currentTarget = position.Value;
		//	hasWaypoint = true;
	}
	// Search nearby creatures and react accordingly
	public virtual void EvaluateCreatures()
	{
		ActiveEnemies.Clear();

		foreach ( var kvp in Targeting.KnownTargets )
		{
			var target = kvp.Value;

			if ( !target.Tracking || target.Target == null )
				continue;

			var go = target.Target.GameObject;

			if ( go.Components.TryGet<BaseEntity>( out var ent ) )
			{
				var relationship = Relations.GetDisposition( ent );

				if ( relationship == NpcRelations.Relation.HATE )
				{
					if ( DebugMode )
					{
						Log.Info( $"Enemy Spotted: {ent}" );
					}

					ActiveEnemies.Add( ent );
					enemyLastKnownPosition = ent.WorldPosition;
				}
			}
		}
	}

	public Vector3 DestinationVector() // Helper to get the Vector3 of our currentTarget Transform.
	{
		var realDest = _currentTarget;

		Vector3 vecDest = new Vector3( realDest );
		return vecDest;

	}

	public Vector3 PositionVector() // Helper to get the Vector3 of our Transform.Position NOTE: after actually looking at sbox docs, this already existed. Well, I guess we have our own now!
	{
		var realPos = Agent.WorldPosition;

		Vector3 vecPos = new Vector3( realPos );
		return vecPos;

	}

	public Vector3 GetActualMovementVector() // This is used in our debug for now, but may be able to apply elsewhere!
	{
		_actualVelocity = (PositionVector() - _lastPosition) / Time.Delta;
		_lastPosition = PositionVector();
		Vector3 actualMove = _actualVelocity.Normal;

		return actualMove;

	}
	public Vector3 GetWishMovementVector()
	{
		Vector3 wishMove = (DestinationVector() - PositionVector()).Normal;
		return wishMove;
	}

	public void EventKilled()
	{
		// will re-add
		//	var carcass = Components.GetOrCreate<NpcCarcass>();
		//	carcass.Owner = this;
		RagdollPhysics.MotionEnabled = true;
		RagdollPhysics.Enabled = true;
		Brain.SetNextThink( 999 ); // hack to make it stop thinking
		Brain.Destroy();
		Targeting.Destroy();
		Relations.Destroy();
		Agent.Destroy();

		Destroy();

		//		scream out to those that would hear you
		//		TODO: this should be handled within talker
		NpcSoundManager.AddSound( NpcSoundManager.SoundType.ALERT_TALKER_CONCEPT, BodyModel.GetAttachment( NpcDef.ModelInfo.MouthAttachment ).Value.Position, GameObject );
	}
	protected override void OnFixedUpdate()
	{
		// moved here from RunAI For now..
		if ( overrideFacing )
		{
			UpdateFacing( true );

			if ( IsFacingIdeal() )
			{
				if ( DebugMode )
					Log.Info( "Done facing ideal target" );
				overrideFacing = false;
				//vecIdealYaw = Vector3.Forward;


			}
		}
		else
		{
			UpdateFacing( false );
		}

		base.OnFixedUpdate();
	}

	protected override void OnUpdate()
	{

		base.OnUpdate();
	}

	private Angles LastRotation;
	private struct FootInfo
	{
		public bool Planted;
		public Vector3 LastFootPlant;
		public Vector3 FootCenter;
	}
	private Dictionary<string, FootInfo> LastFootPlant = new Dictionary<string, FootInfo>();

	protected void UpdateRootMotion()
	{
		//		remove non root motion angle adjustment for now
		var anglediff = WorldRotation.Angles() - LastRotation;

		WorldPosition += BodyModel.RootMotion.Position.RotateAround( Vector3.Zero, WorldRotation );
		WorldRotation = (WorldRotation.Angles() + BodyModel.RootMotion.Rotation.Angles()).ToRotation();

		//		figure out the state of the feet
		var rotationOrigin = Vector3.Zero;
		var plantedFeet = 0;

		foreach ( var foot in NpcDef.ModelInfo.Feet )
		{
			var heelPos = BodyModel.GetAttachment( foot.Value.HeelAttachment ).Value.Position;
			var toePos = BodyModel.GetAttachment( foot.Value.ToeAttachment ).Value.Position;

			FootInfo footinfo = LastFootPlant.GetOrCreate( foot.Key );
			footinfo.FootCenter = heelPos.LerpTo( toePos, 0.5f );

			//			first check heel
			var position = heelPos;
			if ( position.z - WorldPosition.z < 0.6f )
			{
				rotationOrigin += position;

				footinfo.LastFootPlant = position;
				footinfo.Planted = true;
				LastFootPlant[foot.Key] = footinfo;

				plantedFeet++;
				continue;
			}

			//			then toe
			position = toePos;
			if ( position.z - WorldPosition.z < 0.6f )
			{
				rotationOrigin += position;

				footinfo.LastFootPlant = position;
				footinfo.Planted = true;
				LastFootPlant[foot.Key] = footinfo;

				plantedFeet++;
				continue;
			}

			footinfo.Planted = false;
			LastFootPlant[foot.Key] = footinfo;
		}
		var averagePosition = WorldPosition;
		if ( plantedFeet > 0 )
			averagePosition = (rotationOrigin / plantedFeet).WithZ( WorldPosition.z );

		//		rotation outside of root motion should be applied based on foot positions to minimize sliding
		WorldPosition += (WorldPosition - averagePosition).RotateAround( Vector3.Zero, anglediff.ToRotation() ) - (WorldPosition - averagePosition);

		LastRotation = WorldRotation.Angles();
	}


	public void UpdateFacing( bool specifiedLook )
	{
		if ( specifiedLook && vecIdealYaw != null )
		{
			if ( DebugMode )
				Log.Info( "Updating facing to set ideal" );
			Vector3 direction = (vecIdealYaw - WorldPosition).Normal;
			var targetRotation = Rotation.LookAt( direction );
			WorldRotation = Rotation.Lerp( WorldRotation, targetRotation, Time.Delta * TurnSpeed );
		}
		else
		{
			var velocity = Agent.Velocity;
			if ( velocity.Length > 0.1f )
			{
				var moveDirection = velocity.Normal;
				if ( DebugMode )
					Log.Info( "Updating facing to movement" );
				var targetRotation = Rotation.LookAt( moveDirection );
				WorldRotation = Rotation.Lerp( WorldRotation, targetRotation, Time.Delta * TurnSpeed );
			}
		}
	}


	protected override void OnDestroy()
	{
		base.OnDestroy();
	}

	//	HEALTH
	public void OnDamage( in DamageInfo dmginfo )
	{
		if ( _Cine != null && !_Cine.AllowActorDeath )
			dmginfo.Damage = 0;

		Log.Info( $"{this.TargetName} taking {dmginfo.Damage} damage from {dmginfo.Attacker}" );


		Vector3 vecDir = WorldPosition;
		if ( dmginfo.Attacker != null )
		{
			vecDir = dmginfo.Attacker.WorldPosition - new Vector3( 0, 0, 10 ) - WorldPosition;
			Vector3.Direction( WorldPosition, dmginfo.Attacker.WorldPosition );
		}
		lastAttackDir = vecDir;

		if ( dmginfo.Damage >= 15 )
		{
			Conditions.SetCondition( BaseNPCConditions.AIConditions.COND_HEAVY_DAMAGE );
		}
		else
		{
			Conditions.SetCondition( BaseNPCConditions.AIConditions.COND_LIGHT_DAMAGE );
		}

		Agent.Stop();// Bad call to agent, really this should be done elsewhere

		BodyModel.Set( "b_Interrupt", true ); // flinch test

		/*if ( dmginfo.Hitbox != null && dmginfo.Hitbox.Tags.Has( "HITGROUP_HEAD" ) )
			dmginfo.AdjustDamageScale( 3f );*/ // redo

		Health -= dmginfo.Damage;

		if ( Health <= 0 )
			OnDeath( dmginfo );

	}
	public void OnDeath( DamageInfo dmginfo )
	{
		//		shouldnt be getting any deader
		if ( !IsAlive )
			return;

		IsAlive = false;

		//		send word to animgraph
		AnimgraphController?.OnKilled( dmginfo );

		//		send word to the weapons
		foreach ( var weapon in Weapons )
			weapon.OnOwnerKilled();

		//		scream out to those that would hear you
		//		TODO: this should be handled within talker
		NpcSoundManager.AddSound( NpcSoundManager.SoundType.ALERT_TALKER_CONCEPT, BodyModel.GetAttachment( NpcDef.ModelInfo.MouthAttachment ).Value.Position, GameObject );

		//		spawn corpse manager
		Components.GetOrCreate<NpcCarcass>().Owner = this;

		//		dont need to think or target anymore
		Brain?.Destroy();
		Targeting?.Destroy();
		Agent.Destroy();
		//Enabled = false;
	}

	//		unarmed attacks
	/*public void UnarmedAttack_Shove()
	{
		var baseposition = WorldPosition + WorldRotation.Forward * 25f;
		var capsule = new Capsule( baseposition + Vector3.Up * 30f, baseposition + Vector3.Up * 50f, 25f );
		var damage = new TakeDamageInfo( GameObject, GameObject, WorldRotation.Forward * 180f + Vector3.Up * 100f, baseposition, 0f, DamageType.DMG_CLUB );
		AttackManager.TraceGenericAttack( capsule, damage );
	}*/

	//=========================================================
	//=========================================================
	NpcSoundManager.SoundType GetSoundInterests()
	{
		return NpcSoundManager.SoundType.SOUND_WORLD | NpcSoundManager.SoundType.SOUND_COMBAT | NpcSoundManager.SoundType.SOUND_PLAYER | NpcSoundManager.SoundType.SOUND_PLAYER_VEHICLE |
			NpcSoundManager.SoundType.SOUND_BULLET_IMPACT;
	}

	public void Wake()
	{
		if ( DebugMode )
			Log.Info( "Waking NPC..." );

		if ( GetSleepState() != AI_SleepState.SLEEPSTATE_AWAKE )
		{
			SetSleepState( AI_SleepState.SLEEPSTATE_AWAKE );
			canFollowMoveTarget = true;
		}
	}

	public void CallNPCThink()
	{

		Think();
	}

	public virtual void Think() // ....Just an idea..
	{
		if ( DebugMode )
		{
			Log.Info( $"Thinking in NPC!" );
		}

		EquippedWeaponSlot = IdealEquippedWeaponSlot;
		OffhandWeaponSlot = IdealOffhandWeaponSlot;

		RunAI();
	}

	public void RunAI()
	{
		if ( DebugMode )
		{
			Log.Info( "RunAI: Started" );
		}

		BodyModel.Set( "f_MoveVelocity", MathX.Lerp( 0, Agent.Velocity.Length, Time.Delta ) );

		Conditions.conditionsGathered = false;

		CheckPVSCondition();
		GatherConditions();

		if ( !Conditions.conditionsGathered )
			Conditions.conditionsGathered = true;

		Brain.MaintainSchedule();
		IsInLineOfSight();
		//	NavCheckTarget();





		if ( DebugMode )
		{
			Log.Info( "RunAI: Ended" );
		}
	}

	public void GatherEnemyConditions()
	{
		if ( HasEnemy && nextRangeAttackTime <= Time.Now )
		{
			if ( CheckRangeAttack1() )
			{
				Conditions.SetCondition( AIConditions.COND_CAN_RANGE_ATTACK1 );
			}

		}


	}

	AIState SelectIdealIdleState()
	{
		if ( DebugMode )
		{
			Log.Info( "SelectIdealIdleState: Running..." );
		}


		if ( Conditions.HasCondition( AIConditions.COND_NEW_ENEMY ) ||
			 Conditions.HasCondition( AIConditions.COND_SEE_ENEMY ) )
		{
			// new enemy! This means an idle npc has seen someone it dislikes, or
			// that a npc in combat has found a more suitable target to attack
			if ( DebugMode )
			{
				Log.Info( "SelectIdealIdleState: Initiating Combat" );
			}
			return AIState.COMBAT;
		}

		// Set our ideal yaw if we've taken damage
		if ( Conditions.HasCondition( AIConditions.COND_LIGHT_DAMAGE ) ||
			 Conditions.HasCondition( AIConditions.COND_HEAVY_DAMAGE ) )
		{
			Vector3 vecEnemyLKP;

			// Fill in where we're trying to look
			if ( GetEnemy() != null )
			{
				vecEnemyLKP = GetEnemyLKP();
			}
			else
			{
				/*	if ( GetEnemies()->Find( AI_UNKNOWN_ENEMY ) )
					{
						vecEnemyLKP = GetEnemies()->LastKnownPosition( AI_UNKNOWN_ENEMY );
					}
					else
					{*/
				// Don't have an enemy, so face the direction the last attack came from (don't face north)
				vecEnemyLKP = WorldPosition + (lastAttackDir * 128);
				//}
			}
			if ( DebugMode )
				Log.Info( $"Facing damage at {vecEnemyLKP} " );
			// Set the ideal
			lastDisturbanceTime = Time.Now;
			SetIdealYawToTarget( vecEnemyLKP );

			return AIState.ALERT;
		}

		if ( Conditions.HasCondition( AIConditions.COND_HEAR_DANGER ) ||
			 Conditions.HasCondition( AIConditions.COND_HEAR_COMBAT ) ||
			 Conditions.HasCondition( AIConditions.COND_HEAR_BULLET_IMPACT ) )
		{
			Vector3? pSound = Targeting.GetClosestSoundPosition();
			if ( pSound != null )
			{

				Vector3 direction = (pSound.Value - WorldPosition).WithZ( 0 ).Normal;
				if ( direction.Length > 0.001f )
				{
					Vector3 lookTarget = WorldPosition + direction * 128f;
					lastDisturbanceTime = Time.Now;
					SetIdealYawToTarget( lookTarget );
				}
			}


			return AIState.ALERT;
		}

		if ( DebugMode )
		{
			Log.Info( "SelectIdealIdleState: returned idle" );
		}
		// returned nothing? Invalid state!
		return AIState.IDLE;
	}

	//-----------------------------------------------------------------------------
	// Selecting the alert ideal state
	//-----------------------------------------------------------------------------
	AIState SelectAlertIdealState()
	{
		// ALERT goes to IDLE upon becoming bored
		// ALERT goes to COMBAT upon sighting an enemy
		if ( Conditions.HasCondition( AIConditions.COND_NEW_ENEMY ) ||
			 Conditions.HasCondition( AIConditions.COND_SEE_ENEMY ) ||
			 GetEnemy() != null )
		{
			return AIState.COMBAT;
		}

		// Set our ideal yaw if we've taken damage
		if ( Conditions.HasCondition( AIConditions.COND_LIGHT_DAMAGE ) ||
			 Conditions.HasCondition( AIConditions.COND_HEAVY_DAMAGE ) /*||
			(GetEnemy() == null /*&& Time.Now - GetEnemies()->LastTimeSeen( AI_UNKNOWN_ENEMY ) < TIME_CARE_ABOUT_DAMAGE*/ )
		{
			Vector3 vecEnemyLKP;

			// Fill in where we're trying to look
			if ( GetEnemy() != null )
			{
				vecEnemyLKP = GetEnemyLKP();
			}
			else
			{
				/*	if ( GetEnemies()->Find( AI_UNKNOWN_ENEMY ) )
					{
						vecEnemyLKP = GetEnemies()->LastKnownPosition( AI_UNKNOWN_ENEMY );
					}
					else
					{*/
				// Don't have an enemy, so face the direction the last attack came from (don't face north)

				vecEnemyLKP = WorldPosition + (lastAttackDir * 128);
				if ( DebugMode )
					Log.Info( $"Facing damage at {vecEnemyLKP} " );
				//}
			}
			lastDisturbanceTime = Time.Now;
			// Set the ideal
			SetIdealYawToTarget( vecEnemyLKP );

			return AIState.ALERT;
		}

		if ( Conditions.HasCondition( AIConditions.COND_HEAR_DANGER ) ||
			 Conditions.HasCondition( AIConditions.COND_HEAR_COMBAT ) ||
			 Conditions.HasCondition( AIConditions.COND_HEAR_BULLET_IMPACT )
			 )
		{
			Vector3? pSound = Targeting.GetClosestSoundPosition();
			if ( pSound != null )
			{
				// Convert to a direction like lastDamageDirection
				Vector3 direction = (pSound.Value - WorldPosition).WithZ( 0 ).Normal;
				if ( direction.Length > 0.001f )
				{
					Vector3 lookTarget = WorldPosition + direction * 128f;
					lastDisturbanceTime = Time.Now;
					SetIdealYawToTarget( lookTarget );
				}
			}


			return AIState.ALERT;
		}

		if ( ShouldGoToIdleState() )
		{
			Brain.StartSchedule( AISchedules.SCHED_IDLE_WANDER );
			return AIState.IDLE;
		}

		return AIState.IDLE;
	}

	public bool ShouldGoToIdleState()
	{
		return ((Time.Now - lastDisturbanceTime) >= calmDownTime);
	}

	public void SetIdealYawToTarget( Vector3 targetPosition )
	{
		if ( overrideFacing )
			return; // already facing something

		vecIdealYaw = targetPosition;
		overrideFacing = true;
	}

	public bool IsFacingIdeal( float toleranceDegrees = 5f )
	{

		Vector3 targetDir = (vecIdealYaw - WorldPosition).WithZ( 0 ).Normal;
		Vector3 forwardDir = WorldRotation.Forward.WithZ( 0 ).Normal;

		if ( targetDir.Length < 0.001f || forwardDir.Length < 0.001f )
			return false;

		float angle = Vector3.GetAngle( forwardDir, targetDir );
		return angle <= toleranceDegrees;
	}


	public AIState SelectIdealState()
	{
		switch ( Brain.State )
		{
			case AIState.IDLE:
				{
					AIState nState = SelectIdealIdleState();
					return nState;

				}
			case AIState.ALERT:
				{

					AIState nState = SelectAlertIdealState();
					return nState;
					break;
				}
		}

		return Brain.idealState;

	}

	public Vector3 GetEnemyLKP()
	{
		return enemyLastKnownPosition;
	}

	int Square( int a )
	{
		return a * a;
	}

	public bool IsInLineOfSight()
	{
		foreach ( var kv in Targeting.KnownTargets )
		{
			var targetData = kv.Value;

			if ( !targetData.Tracking || targetData.Type != NpcRelations.TargetType.PLAYER )
				continue;

			if ( targetData.Target is not { } target || !target.IsValid() )
				continue;

			var start = WorldPosition + new Vector3( 0, 0, 45 );
			var end = target.WorldPosition + Vector3.Up * 40f; // Aim for upper body

			var trace = Scene.Trace.Ray( start, end )
				.WithoutTags( "npc", "trigger" )

				.Run();

			/*if ( trace.Hit && trace.GameObject != target )
				continue;*/
			HasEnemy = true;
			shouldChaseEnemy = true;
			Conditions.SetCondition( AIConditions.COND_SEE_ENEMY ); // also probably need a condition for seeing player!
			return true;
		}

		return false;
	}


	public bool CheckPVSCondition()
	{
		bool bInPVS = (WorldPosition.Distance( AI_GetPlayer().WorldPosition ) < 2048); // very basic PVS attempt, works for now

		if ( bInPVS )
		{
			Conditions.SetCondition( AIConditions.COND_IN_PVS );

			if ( DebugMode )
			{
				Log.Info( "Player is in PVS" );
			}
		}

		else
			Conditions.RemoveCondition( AIConditions.COND_IN_PVS );

		return bInPVS;
	}

	public void PerformSensing()
	{
		Targeting.PerformSensing();

	}

	public void EvaluationSenses()
	{
		EvaluateCreatures();
		EvaluateKnownSounds();
	}

	public BaseEntity GetEnemy()
	{
		return CurrentEnemy;
	}

	public void GatherConditions()
	{
		Conditions.conditionsGathered = true;

		if ( Brain.State != AIState.NONE && Brain.State != AIState.DEAD )
		{

			// If we are in PVS OR in combat. Will need efficiency checks once thats implemented aswell
			if ( Conditions.HasCondition( AIConditions.COND_IN_PVS ) &&
				 Brain.State != AIState.COMBAT
				)
			{
				if ( DebugMode )
				{
					Log.Info( "GatherConditions: Started" );
				}
				PerformSensing();
				EvaluationSenses();
				//	GetEnemy();

				if ( HasEnemy )
				{
					GatherEnemyConditions();
				}

			}

		}
		else
		{
			Conditions.RemoveCondition( AIConditions.COND_IN_PVS );
		}
		/*foreach ( var conds in Conditions.conditionQueue )
		{
			Conditions.ActiveConditions.Add( conds );
		}*/
	}


	public void UpdateSleepState( bool bInPVS )
	{
		if ( GetSleepState() > AI_SleepState.SLEEPSTATE_AWAKE )
		{
			canFollowMoveTarget = false;

			if ( wakeRadius > .1 && /*!(pLocalPlayer->GetFlags() & FL_NOTARGET) &&*/ (AI_GetPlayer().WorldPosition - Agent.WorldPosition).LengthSquared <= Square( wakeRadius ) )
			{
				Wake();
			}

			else if ( GetSleepState() == AI_SleepState.SLEEPSTATE_WAITING_FOR_PVS )
			{
				if ( bInPVS )
					Wake();
			}

			else if ( GetSleepState() == AI_SleepState.SLEEPSTATE_WAITING_FOR_ENEMY )
			{
				if ( Conditions.HasCondition( AIConditions.COND_LIGHT_DAMAGE ) || Conditions.HasCondition( AIConditions.COND_HEAVY_DAMAGE ) )
					Wake();
				else
				{
					if ( bInPVS )
					{

						if ( AI_GetPlayer() != null && IsInLineOfSight() )

							Wake();
					}

					// Should check for visible danger sounds
					if ( (GetSoundInterests() == NpcSoundManager.SoundType.SOUND_DANGER) /*&& !(HasSpawnFlags( SF_NPC_WAIT_TILL_SEEN ))*/ )
					{
						var iSound = Targeting.KnownSounds;

						foreach ( var sounds in iSound )
						{
							var snd = sounds.Value;

							if ( (snd.SoundType == NpcSoundManager.SoundType.SOUND_DANGER) ) /*&&*/
							/* GetSenses()->CanHearSound( pCurrentSound ) &&
							 SoundIsVisible( pCurrentSound ) )*/
							{
								Wake();
								break;
							}

							//iSound = sounds.;
						}
					}
				}
			}
		}
		Wake();
	}

	// Cine



}
