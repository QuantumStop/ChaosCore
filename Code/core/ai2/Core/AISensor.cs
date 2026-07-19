
#if OLD_SENSOR
namespace Core.AI;

using Core;
using System;
using System.Collections.Generic;


/// <summary>
/// This is all messy right now. Its more of a working concept, refinement shall come one day!
/// </summary>
/// 

public class TouchSensor
{
	public TouchSensor( AIController controller ) { agent = controller; }
	private AIController agent;

	public void HandleTouch()
	{
		agent.WorldState.Set( "isTouchingPlayer", agent.touchingPlayer );
		agent.WorldState.Set( "isTouchingFriend", agent.touchingAlly );
		agent.WorldState.Set( "isTouchingEnemy", agent.touchingEnemy );
	}

}

public class PainSensor
{
	public PainSensor( AIController owner ) { Agent = owner; }
	private AIController Agent;

	float PainScore; // 0 -> 1 
	//float PainDecaySpeed;

	bool PainIsLow; // look around, maybe move a bit
	bool PainIsMedium; // stop what we're doing, run around a bit
	bool PainIsHigh; // take cover

	public float PainTime; // how long we feel pain and thus will react for

	public bool ShouldUpdateWorldState;

	TimeSince TimeSinceLastInjury;

	public float DeterminePainScore( DamageInfo dmgInfo )
	{
		var damageAmount = dmgInfo.Damage;
		var currentHealth = Agent.curHealth;
		var maxHealth = Agent.maxHealth;

		// Higher damage + lower health = more pain
		float healthRatio = 1f - (currentHealth / maxHealth); // 0 = full health, 1 = near death
		float painScore = (damageAmount / maxHealth) + healthRatio * 0.5f; // weight health state

		float painFinal = MathX.Clamp( painScore, 0, 1 );

		return painFinal;
	}

	public void InflictPain( DamageInfo dmgInfo )
	{

		PainScore = DeterminePainScore( dmgInfo );

		if ( PainScore <= 0.3 )
		{
			PainIsLow = true;
			//	Log.Info("WTF was that? low pain detected");
		}
		else if ( PainScore <= 0.6 )
		{
			PainIsMedium = true;
			//	Log.Info( "That hurt. Medium pain detected" );
		}
		else
		{
			//	Log.Info( "YYYEAAAAAAAAAAAAOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOOUUUUCCCHHHH high pain detected" );
			PainIsHigh = true;
		}

		TimeSinceLastInjury = 0;
		ShouldUpdateWorldState = true;
	}

	public void UpdateWorldState()
	{
		if ( TimeSinceLastInjury > (Time.Now + PainTime) )
		{
			ShouldUpdateWorldState = false;
			return;
		}

		PainTime = PainScore * 2 * (1 + PainScore);

		var painHigh = PainIsHigh && TimeSinceLastInjury < PainTime;
		var painMedium = PainIsMedium && TimeSinceLastInjury < PainTime;
		var painLow = PainIsLow && TimeSinceLastInjury < PainTime;

		//Log.Info($"Paintime: {PainTime} | TimeSinceLastInjury: {TimeSinceLastInjury}");

		Agent.WorldState.Set( AIFacts.HighPain, painHigh );
		Agent.WorldState.Set( AIFacts.MediumPain, painMedium );
		Agent.WorldState.Set( AIFacts.LowPain, painLow );
	}

	public void Tick()
	{
		if ( ShouldUpdateWorldState )
			UpdateWorldState();
	}

}
public class SquadContext
{
	public BaseEntity activeEnemy;
	public bool enemyVisible;
	public TimeSince lastSeenEnemyTime;
	public Vector3 lastKnownPosition;
	public bool shouldFlock;

}
public sealed class SquadSensor
{

	private readonly AIController agent;
	private readonly AISquad squad;

	private float scanInterval = 0.2f;
	private float scanTimer;

	public SquadContext context;

	public SquadSensor( AIController agent )
	{
		this.agent = agent;
		this.squad = agent.aiSquad;
	}

	public void Tick()
	{
		if ( squad is null )
			return;

		scanTimer += Time.Delta;
		if ( scanTimer < scanInterval )
			return;

		scanTimer = 0f;
		UpdateSquadState();
	}
	private void UpdateSquadState()
	{
		int aliveCount = 0;
		int enemyContactCount = 0;
		bool leaderAlive = false;
		var isSquadLeader = false;

		// Ensure context exists
		context ??= new SquadContext();

		BaseEntity bestEnemy = null;
		float bestEnemyDist = float.MaxValue;

		foreach ( var member in squad.members )
		{
			if ( !member.IsValid() )
				continue;

			if ( member.IsAlive )
				aliveCount++;

			if ( member == squad.Leader && member.IsAlive )
				leaderAlive = true;

			var enemy = member.Blackboard.activeEnemy;

			if ( squad.Leader == agent )
				isSquadLeader = true;

			if ( !enemy.IsValid() || !enemy.IsValid )
				continue;

			enemyContactCount++;

			float dist = member.WorldPosition.Distance( enemy.WorldPosition );
			if ( dist < bestEnemyDist )
			{
				bestEnemyDist = dist;
				bestEnemy = enemy;
			}
		}

		// TODO: update this part with the new squad context, and make a method for transmitting a squad context message
		context.activeEnemy = bestEnemy;
		// send out context to our squad
		if ( context.activeEnemy.IsValid() )
		{
			foreach ( var member in squad.members )
			{
				if ( !member.IsValid() || !member.IsAlive )
					continue;

				member.Blackboard.activeEnemy = context.activeEnemy;

			}
		}

		float distToLeader = float.MaxValue;
		if ( agent.aiSquad?.Leader is not null )
			distToLeader = agent.WorldPosition.Distance( agent.aiSquad.Leader.WorldPosition );

		agent.WorldState.Set( AIFacts.IsSquadLeader, isSquadLeader );
		agent.WorldState.Set( AIFacts.SquadHasEnemyContact, enemyContactCount > 0 );
		agent.WorldState.Set( AIFacts.LeaderDistanceOk, leaderAlive && distToLeader < 400f );
		agent.WorldState.Set( AIFacts.SquadCohesionOK, CheckSquadCohesion() );
		agent.WorldState.Set( AIFacts.SquadLeaderAlive, leaderAlive );
		agent.WorldState.Set( AIFacts.SquadIsBroken, aliveCount <= 1 );
	}

	private bool CheckSquadCohesion()
	{
		float radius = 400;
		int closeMembers = 0;
		foreach ( var member in agent.aiSquad.members )
		{
			if ( member == agent )
				continue;

			if ( member.WorldPosition.Distance( agent.WorldPosition ) <= radius )
				closeMembers++;

		}

		if ( closeMembers >= (int)agent.aiSquad.MemberCount * .5 )
			return true;

		return false;
	}
}


public sealed class ThreatEvaluator
{
	public float ThreatScore { get; private set; }

	public bool ThreatHigh => ThreatScore >= 0.4f;
	public bool ThreatLow => ThreatScore <= 0.2f;

	public void Update( AIController agent )
	{
		var enemy = agent.Blackboard.activeEnemy;

		if ( !enemy.IsValid() || !enemy.IsValid )
		{
			agent.Blackboard.activeEnemy = null;
			ThreatScore = 0f;
			return;
		}

		float dist = agent.WorldPosition.Distance( enemy.WorldPosition );

		float healthRatio = 0f;
		if ( agent.Blackboard.activeEnemy is BasePlayer player )
		{
			healthRatio = player.Health / 100;
		}
		else if ( agent.Blackboard.activeEnemy is AIController AI )
		{
			healthRatio = AI.curHealth / 100;
		}

		float distanceFactor = 1f - Math.Clamp( dist / 600f, 0f, 1f );
		float healthFactor = 1f - healthRatio;

		ThreatScore =
			(distanceFactor * 0.5f) +
			(healthFactor * 0.5f);
	}
}


public class NpcTargetingSensor
{
	public readonly struct SensorData
	{
		public readonly NpcRelations.TargetType Type;
		public readonly BaseEntity Target;
		public readonly bool Tracking;       // currently in LOS
		public readonly bool WasEverSeen;    // have we had LOS at least once
		public readonly Vector3 LastKnownPosition;
		public readonly Vector3 LastKnownVelocity;
		public readonly float LostTime;
		public readonly float ThreatScore;

		public SensorData(
			NpcRelations.TargetType type, BaseEntity target, bool tracking,
			bool wasEverSeen, Vector3 lkp, Vector3 lkv, float lostTime, float threatScore )
		{
			Type = type;
			Target = target;
			Tracking = tracking;
			WasEverSeen = wasEverSeen;
			LastKnownPosition = lkp;
			LastKnownVelocity = lkv;
			LostTime = lostTime;
			ThreatScore = threatScore;
		}

		public SensorData Decayed( float dt, float decayRate ) => new(
			Type, Target, false, WasEverSeen,
			LastKnownPosition + LastKnownVelocity * dt,
			LastKnownVelocity,
			LostTime + dt * decayRate,
			ThreatScore
		);

		public bool IsExpired => LostTime > 20f || !Target.IsValid() || !Target.IsValid;
	}

	public struct AISoundMemory
	{
		public NpcSoundManager.SoundType SoundType;
		public Vector3 Position;
		public GameObject Owner;
		public float TimeHeard;
		public float TimeToForget;
		public bool Registered;
	}

	public AIController Owner { get; set; }

	[Property] public float VisionRange => Owner.Definition.VisionRange;
	[Property] public float VisionFOV => Owner.Definition.VisionFOV;
	[Property] public float PeripheralFOV => Owner.Definition.PeripheralFOV;   //wider but shorter range
	[Property] public float PeripheralRange => Owner.Definition.PeripheralRange;   // close peripheral detection
	[Property] public float MemoryDecayRate => Owner.Definition.MemoryDecayRate;
	[Property] public float LostTargetTimeout => Owner.Definition.LostTargetTimeout;    // seconds before alert drops

	[Property, ReadOnly] public SensorData? PrimaryTarget { get; private set; }
	[Property, ReadOnly] public Dictionary<Guid, SensorData> KnownTargets { get; private set; } = new();
	[Property, ReadOnly] public Dictionary<Guid, AISoundMemory> KnownSounds { get; private set; } = new();

	// Is the primary target actually in LOS right now?
	public bool PrimaryTargetVisible => PrimaryTarget?.Tracking ?? false;

	// How long since we last had eyes on the primary target?
	public float TimeSinceLastSeen => PrimaryTarget.HasValue
		? PrimaryTarget.Value.LostTime
		: float.MaxValue;

	private readonly List<TargetSignature> _visibleScratch = new();
	private readonly List<Guid> _activeScratch = new();
	private List<Guid> _expiredScratch;

	public void PerformSensing()
	{

		UpdateSoundState();
		UpdateVisionMemory();
		UpdatePrimaryTarget();

		PruneExpiredSounds();
		UpdateWorldState();
	}

	public BaseEntity GetPrimaryTarget() => PrimaryTarget?.Target;

	public bool IsVisible( TargetSignature target ) => _visibleScratch.Contains( target );

	public Vector3? GetClosestSoundPosition( float maxDistance = 9999f )
	{
		float closest = maxDistance * maxDistance;
		Vector3? result = null;

		foreach ( var snd in KnownSounds.Values )
		{
			float d = Vector3.DistanceBetweenSquared( Owner.WorldPosition, snd.Position );
			if ( d < closest ) { closest = d; result = snd.Position; }
		}

		return result;
	}

	private bool CanHearSound( Vector3 position )
	{
		float dist = Owner.WorldPosition.Distance( position );
		return dist <= Owner.Definition.HearingRange;
	}

	private void UpdateSoundState()
	{
		bool heardEnemy = false;
		bool heardAlly = false;
		bool heardSus = false;
		Vector3? bestEnemySoundPos = null;
		float bestEnemyScore = float.MinValue;
		float bestSusScore = float.MinValue;
		var investigated = false;

		foreach ( var kvp in KnownSounds.Values )
		{
			//Log.Info("SOUND REGISTERED");

			if ( !kvp.Registered ) continue;

			float recency = 1f - ((Time.Now - kvp.TimeHeard) / kvp.TimeToForget);
			float dist = Owner.WorldPosition.Distance( kvp.Position );
			float score = recency / MathF.Max( dist, 1f );

			bool isEnemySound = kvp.SoundType == NpcSoundManager.SoundType.SOUND_GUNFIRE;

			// This is a quieter sound, but still worth looking into. Footstep, door opening, prop being knocked over etc
			bool isSuspiciousSound = kvp.SoundType == NpcSoundManager.SoundType.SOUND_FOOTSTEP
				|| kvp.SoundType == NpcSoundManager.SoundType.SOUND_PHYSICS;

			bool isAllySound = NpcSoundManager.IsAllySound( kvp.SoundType ); // there are no types with ally_, idk

			if ( isEnemySound && score > bestEnemyScore )
			{
				bestEnemyScore = score;
				bestEnemySoundPos = kvp.Position;
				heardEnemy = true;
				Owner.Blackboard.lastCombatSoundHeard = 0;
				Owner.LastSoundHeardPosition = bestEnemySoundPos.Value;
			}

			if ( isSuspiciousSound && score > bestSusScore )
			{
				bestSusScore = score;
				bestEnemySoundPos = kvp.Position;
				heardSus = true;

				Owner.LastSoundHeardPosition = bestEnemySoundPos.Value;
			}

			if ( isAllySound )
				heardAlly = true;
		}

		investigated = (Owner.LastSoundHeardPosition.Distance( Owner.WorldPosition ) < 200);

		Owner.WorldState.Set( AIFacts.HeardSound, KnownSounds.Count > 0 );
		Owner.WorldState.Set( AIFacts.HeardEnemySound, heardEnemy );
		Owner.WorldState.Set( AIFacts.SoundInvestigated, investigated );
		Owner.WorldState.Set( AIFacts.HeardAllySound, heardAlly );
		Owner.WorldState.Set( AIFacts.HeardSuspiciousSound, heardSus );

		if ( Owner.WorldState.Get( AIFacts.SearchingForEnemy ) && heardEnemy )
		{
			Owner.enemyLKP = Owner.LastSoundHeardPosition;
		}


	}

	private void UpdateVisionMemory()
	{
		_visibleScratch.Clear();
		_activeScratch.Clear();

		var eye = Owner.Transform.World;
		var eyePos = eye.Position + Vector3.Up * Owner.Definition.AgentHeight; // we will properly use an eye attachment at some point. but not today!
		var eyeFwd = eye.Rotation.Forward;

		DecayCombatMemory();

		foreach ( var sig in Owner.Scene.Components.GetAll<TargetSignature>() )
		{
			if ( !sig.IsValid ) continue;
			if ( sig.GameObject == Owner.GameObject ) continue;
			if ( sig is PlayerTargetSignature ps && ps.shouldIgnore ) continue;
			if ( sig is PlayerTargetSignature && AIController.NoTarget ) continue;

			var targetEntity = sig.GameObject.GetComponent<BaseEntity>();
			if ( IsAlly( targetEntity ) ) continue;

			Vector3 toTarget = sig.WorldPos - eyePos;
			float distSq = toTarget.LengthSquared;
			float dist = MathF.Sqrt( distSq );
			if ( dist < 0.001f ) continue;

			Vector3 toTargetNorm = toTarget / dist;
			float angle = Vector3.GetAngle( eyeFwd, toTargetNorm );

			bool inPrimaryCone = dist <= VisionRange && angle <= VisionFOV * 0.5f;
			bool inPeripheral = dist <= PeripheralRange && angle <= PeripheralFOV * 0.5f;

			if ( !inPrimaryCone && !inPeripheral ) continue;
			if ( !HasLineOfSight( eyePos, sig ) ) continue;

			var bullseye = Owner.currentBullseye;
			if ( bullseye.IsValid() )
			{
				if ( bullseye.Health <= 0 || !Owner.shouldAttackBullseye ) continue; // ignore inactive bullseyes
			}

			_visibleScratch.Add( sig );
		}

		// Refresh memory for visible targets
		foreach ( var sig in _visibleScratch )
		{
			var entity = sig.GameObject.GetComponent<BaseEntity>();
			_activeScratch.Add( sig.GameObject.Id );

			// Compute threat score at acquisition time so scoring stays stable mid-tick, hopefully
			float score = ComputeThreatScore( sig.WorldPos, sig.Velocity, 0f, sig.Type );

			var data = new SensorData(
				sig.Type, entity, true, true,
				sig.WorldPos, sig.Velocity, 0f, score
			);

			RefreshCombatMemory( data );
			KnownTargets[sig.GameObject.Id] = data;
		}

		// decay targets no longer visible
		foreach ( var kvp in KnownTargets.ToList() )
		{
			if ( _activeScratch.Contains( kvp.Key ) ) continue;

			var decayed = kvp.Value.Decayed( Time.Delta, MemoryDecayRate );

			if ( decayed.IsExpired )
			{
				KnownTargets.Remove( kvp.Key );
				continue;
			}

			KnownTargets[kvp.Key] = decayed;
		}
	}

	private bool HasLineOfSight( Vector3 eyePos, TargetSignature sig )
	{
		// Try both body and head positions. if either is clear, we have LOS
		var targetCenter = sig.WorldPos + Vector3.Up * 40f; // 
		var targetHead = sig.WorldPos + Vector3.Up * 70f;

		var bodyTrace = Owner.Scene.Trace
			.Ray( eyePos, targetCenter )
			.IgnoreGameObjectHierarchy( Owner.GameObject )
			.IgnoreGameObjectHierarchy( sig.GameObject )
			.Run();

		if ( !bodyTrace.Hit ) return true;

		var headTrace = Owner.Scene.Trace
			.Ray( eyePos, targetHead )
			.IgnoreGameObjectHierarchy( Owner.GameObject )
			.IgnoreGameObjectHierarchy( sig.GameObject )
			.Run();

		return !headTrace.Hit;
	}

	private bool IsAlly( BaseEntity entity )
	{
		if ( !entity.IsValid() ) return false;
		if ( entity is AIController ai )
			return ai.Relationships.Faction == Owner.Relationships.Faction;
		return false;
	}
	private void UpdatePrimaryTarget()
	{

		foreach ( var key in KnownTargets.Keys.ToList() )
		{
			if ( KnownTargets[key].IsExpired )
				KnownTargets.Remove( key );
		}

		Owner.enemyLKP = Owner.Blackboard.combatMemory?.LastKnownPosition;
		Owner.WorldState.Set( "hasEnemyLKP", Owner.enemyLKP.HasValue );
		Owner.Blackboard.lastSeenEnemyTime = Time.Now;
		if ( KnownTargets.Count == 0 )
		{
			PrimaryTarget = null;
			Owner.Blackboard.activeEnemy = null;
			Owner.Blackboard.enemyDistance = 0f;
			return;
		}

		float bestScore = float.MinValue;
		SensorData? bestTarget = null;

		foreach ( var kvp in KnownTargets )
		{
			var t = kvp.Value;
			if ( IsAlly( t.Target ) ) continue;

			// recompute score with current lost time so it decays as well
			float score = ComputeThreatScore(
				t.LastKnownPosition, t.LastKnownVelocity, t.LostTime, t.Type );

			if ( score > bestScore )
			{
				bestScore = score;
				bestTarget = t;
			}
		}

		PrimaryTarget = bestTarget;

		if ( !bestTarget.HasValue )
		{
			Owner.Blackboard.activeEnemy = null;
			Owner.Blackboard.enemyDistance = 0f;
			return;
		}

		var best = bestTarget.Value;

		Owner.Blackboard.activeEnemy = best.Target;
		Owner.Blackboard.enemyDistance = Owner.WorldPosition.Distance( best.LastKnownPosition );
		Owner.enemyLKP = best.LastKnownPosition;

		if ( best.Tracking )
			Owner.lastSeenEnemyTime = Time.Now;
	}

	private float ComputeThreatScore(
		Vector3 lkp, Vector3 lkv, float lostTime,
		NpcRelations.TargetType type )
	{
		float dist = (Owner.WorldPosition - lkp).Length;
		float distFactor = 1f / MathF.Max( dist, 1f );
		float lostFactor = MathF.Max( 1f - lostTime / 10f, 0f ); // degrades over 10s
		float typeFactor = type == NpcRelations.TargetType.PLAYER ? 2f : 1f;

		return distFactor * lostFactor * typeFactor;
	}

	private void UpdateWorldState()
	{
		bool hasEnemy = PrimaryTarget.HasValue;
		bool tracking = PrimaryTargetVisible;
		float lostTime = hasEnemy ? PrimaryTarget.Value.LostTime : float.MaxValue;
		float dist = Owner.Blackboard.enemyDistance;

		Owner.WorldState.Set( "hasEnemy", hasEnemy );

		Owner.WorldState.Set( "threatEliminated", !hasEnemy );
		Owner.WorldState.Set( "enemyVisible", tracking );

		// Alert stays true for LostTargetTimeout seconds after losing sight
		bool alert = hasEnemy && lostTime <= LostTargetTimeout || Owner.Blackboard.lastCombatSoundHeard < 15 && Owner.Blackboard.lastCombatSoundHeard is not null || (Owner.aiSquad is not null && Owner.WorldState.Get( "squadHasEnemyContact" ));
		Owner.WorldState.Set( "alert", alert );
		Owner.BodyModel.Set( "b_IsMad", alert );

		// we have a last known position even if not currently visible
		bool hasLKP = hasEnemy && !tracking && Owner.enemyLKP.HasValue && Owner.enemyLKP is not null;
		Owner.WorldState.Set( "hasEnemyLKP", hasLKP );
		Owner.WorldState.Set( "searchingForEnemy", hasLKP && !tracking );

		// attack and self preservation facts. only meaningful when tracking
		if ( tracking )
		{
			var def = Owner.Definition;
			Owner.WorldState.Set( "enemyInRangeAttack1", dist <= def.rangeAttack1_Distance && Owner.CanRangeAttack1() );
			Owner.WorldState.Set( "enemyInRangeAttack2", dist <= def.rangeAttack2_Distance && Owner.CanRangeAttack2() );
			Owner.WorldState.Set( "enemyInMeleeAttack1", dist <= def.meleeAttack1_Distance );
			Owner.WorldState.Set( "enemyInMeleeAttack2", dist <= def.meleeAttack2_Distance && Owner.CanMeleeAttack2() );
			Owner.WorldState.Set( "enemyTooClose", dist <= 80f );
		}
		else
		{
			Owner.WorldState.Set( "enemyInRangeAttack1", false );
			Owner.WorldState.Set( "enemyInRangeAttack2", false );
			Owner.WorldState.Set( "enemyInMeleeAttack1", false );
			Owner.WorldState.Set( "enemyInMeleeAttack2", false );
			Owner.WorldState.Set( "enemyTooClose", false );
		}


		Owner.WorldState.Set( AIFacts.InRange1Cooldown, Time.Now <= Owner.NextRange1AttackTime );

		float healthPct = Owner.curHealth / MathF.Max( Owner.maxHealth, 1f );
		Owner.WorldState.Set( "lowHealth", healthPct <= 0.35f );
		Owner.WorldState.Set( "criticalHealth", healthPct <= 0.15f );
	}


	private void RefreshCombatMemory( SensorData t )
	{
		var mem = Owner.Blackboard.combatMemory ??= new CombatMemory();
		mem.Enemy = t.Target;
		mem.LastKnownPosition = t.LastKnownPosition;
		mem.LastKnownVelocity = t.LastKnownVelocity;
		mem.LastSeenTime = Time.Now;
		mem.Confidence = 1f;
	}

	private void DecayCombatMemory()
	{
		var mem = Owner.Blackboard.combatMemory;
		if ( mem is null || !mem.IsValid ) return;

		mem.Confidence -= 0.15f * Time.Delta;
		mem.LastKnownPosition += mem.LastKnownVelocity * Time.Delta;

		if ( mem.Confidence <= 0f || !mem.Enemy.IsValid )
			Owner.Blackboard.combatMemory = null;
	}


	private void PruneExpiredSounds()
	{
		if ( KnownSounds.Count == 0 ) return;

		foreach ( var kvp in KnownSounds )
		{
			if ( Time.Now - kvp.Value.TimeHeard > kvp.Value.TimeToForget ||
				 !kvp.Value.Owner.IsValid() )
			{
				_expiredScratch ??= new List<Guid>();
				_expiredScratch.Add( kvp.Key );
			}
		}

		if ( _expiredScratch is null ) return;
		foreach ( var key in _expiredScratch ) KnownSounds.Remove( key );
		_expiredScratch.Clear();
	}

	public void DrawGizmos()
	{
		if ( !AIManager.AIDebugVisionSensing ) return;

		var eye = Owner.Transform.World;
		var eyePos = eye.Position;
		var eyeFwd = eye.Rotation.Forward;
		var eyeUp = eye.Rotation.Up;
		var eyeRight = eye.Rotation.Right;

		DrawCone( eyePos, eyeFwd, eyeUp, eyeRight,
			VisionRange, VisionFOV,
			Color.Cyan.WithAlpha( 0.15f ),
			segments: 32 );

		DrawCone( eyePos, eyeFwd, eyeUp, eyeRight,
			PeripheralRange, PeripheralFOV,
			Color.Yellow.WithAlpha( 0.08f ),
			segments: 24 );

		foreach ( var kvp in KnownTargets )
		{
			var t = kvp.Value;

			Gizmo.Draw.IgnoreDepth = true;


			Gizmo.Draw.Color = t.Tracking ? Color.Red
							 : t.LostTime < 5f ? Color.Orange
							 : Color.Gray;

			Gizmo.Draw.LineCapsule(
				new Capsule( t.LastKnownPosition,
							 t.LastKnownPosition + Vector3.Up * 70f, 18f ) );

			// Line from eye to target
			if ( t.Tracking )
			{
				Gizmo.Draw.Color = Color.Red.WithAlpha( 0.5f );
				Gizmo.Draw.Line( eyePos, t.LastKnownPosition );
			}

			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.Text(
				$"{t.Target?.GetType().Name ?? "?"}\n" +
				$"lost:{t.LostTime:F1}s\n" +
				$"score:{t.ThreatScore:F2}",
				new Transform( t.LastKnownPosition + Vector3.Up * 80f ),
				flags: TextFlag.LeftTop );
		}

		foreach ( var snd in KnownSounds.Values )
		{
			if ( !snd.Registered ) continue;

			Gizmo.Draw.Color = snd.SoundType switch
			{
				var s when s.ToString().StartsWith( "STENCH_" ) => Gizmo.Colors.Green,
				var s when s.ToString().StartsWith( "ALERT_" ) => Gizmo.Colors.Red,
				_ => Gizmo.Colors.Blue,
			};

			Gizmo.Draw.LineSphere( snd.Position, 16f );
			Gizmo.Draw.Text( $"{snd.SoundType}",
				new Transform( snd.Position + Vector3.Up * 20f ),
				flags: TextFlag.LeftTop );
		}

		if ( PrimaryTarget.HasValue )
		{
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.Line( eyePos, PrimaryTarget.Value.LastKnownPosition );
		}
	}

	private static void DrawCone(
		Vector3 origin, Vector3 forward, Vector3 up, Vector3 right,
		float range, float fovDegrees, Color color, int segments = 12 )
	{
		Gizmo.Draw.Color = color;

		float halfFov = fovDegrees * 0.5f * MathF.PI / 180f;
		float step = (MathF.PI * 2f) / segments;

		Vector3 prev = Vector3.Zero;

		for ( int i = 0; i <= segments; i++ )
		{
			float angle = i * step;
			float x = MathF.Cos( angle ) * MathF.Sin( halfFov );
			float y = MathF.Sin( angle ) * MathF.Sin( halfFov );
			float z = MathF.Cos( halfFov );

			Vector3 dir = (forward * z + right * x + up * y).Normal;
			Vector3 tip = origin + dir * range;


			if ( i > 0 )
				Gizmo.Draw.Line( prev, tip );

			// Spoke from origin to rim every 8 segments
			if ( i % 8 == 0 )
				Gizmo.Draw.Line( origin, tip );

			prev = tip;
		}

		// Close the base circle and pray to god this is correct now
		Gizmo.Draw.Line( prev, origin + forward * range );
	}
}
#endif
