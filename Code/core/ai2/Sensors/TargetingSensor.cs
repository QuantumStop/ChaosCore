namespace Core.AI;

using System;

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

			float recency = 1f - ((WorldTime.Now - kvp.TimeHeard) / kvp.TimeToForget);
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

		investigated = Owner.LastSoundHeardPosition.Distance( Owner.WorldPosition ) < 200;

		Owner.WorldState.Set( AIFacts.HeardSound, KnownSounds.Count > 0 );
		Owner.WorldState.Set( AIFacts.HeardEnemySound, heardEnemy );
		Owner.WorldState.Set( AIFacts.SoundInvestigated, investigated );
		Owner.WorldState.Set( AIFacts.HeardAllySound, heardAlly );
		Owner.WorldState.Set( AIFacts.HeardSuspiciousSound, heardSus );

		if ( Owner.WorldState.Get( AIFacts.SearchingForEnemy ) && heardEnemy )
		{
			Owner.EnemyLKP = Owner.LastSoundHeardPosition;
		}


	}

	public struct VisionPacket
	{
		public bool HasVisibleTarget;

		public BaseEntity PrimaryTarget;

		public Vector3 LastSeenPosition;
		public Vector3 LastSeenVelocity;

		public float TargetDistance;
		public float ThreatScore;

		public bool TargetInPrimaryCone;
		public bool TargetInPeripheralVision;

		public float TimeSinceSeen;
	}

	private void UpdateVisionMemory()
	{
		_visibleScratch.Clear();
		_activeScratch.Clear();

		Vector3 eyePos;
		Vector3 eyeFwd;


		eyePos = Owner.WorldPosition + (Vector3.Up * (Owner.BodyModel.Bounds.Size.z * 0.8f)); // approximate
		eyeFwd = Owner.WorldRotation.Forward;

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

			var bullseye = Owner.CurrentBullseye;
			if ( bullseye.IsValid() )
			{
				if ( bullseye.Health <= 0 || !Owner.ShouldAttackBullseye ) continue; // ignore inactive bullseyes
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

		Owner.EnemyLKP = Owner.Blackboard.combatMemory?.LastKnownPosition;
		Owner.EnemyLKV = Owner.Blackboard.combatMemory?.LastKnownVelocity;
		Owner.WorldState.Set( "hasEnemyLKP", Owner.EnemyLKP.HasValue );

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
		Owner.EnemyLKP = best.LastKnownPosition;
		Owner.EnemyLKV = best.LastKnownVelocity;

		if ( best.Tracking )
			Owner.LastSeenEnemyTime = WorldTime.Now;
	}

	private float ComputeThreatScore(
		Vector3 lkp, Vector3 lkv, float lostTime,
		NpcRelations.TargetType type )
	{
		float dist = (Owner.WorldPosition - lkp).Length;
		float distFactor = 1f / MathF.Max( dist, 1f );
		float lostFactor = MathF.Max( 1f - lostTime / 10f, 0f ); // degrades over 10s
		float typeFactor = type == NpcRelations.TargetType.PLAYER ? 10f : 1f;

		return distFactor * lostFactor * typeFactor;
	}

	private void UpdateWorldState()
	{
		bool enemyIsPlayer = false;
		bool enemyIsNPC = false;
		bool enemyIsDead = false;

		if ( PrimaryTarget.HasValue && PrimaryTarget.Value.Target is BasePlayer ply )
		{
			enemyIsPlayer = true;

			if ( ply.Health == 0 )
			{
				enemyIsDead = true;
				ply.GetComponentInChildren<PlayerTargetSignature>()?.Destroy();
			}
		}
		else if ( PrimaryTarget.HasValue && PrimaryTarget.Value.Target is AIController npc )
		{
			enemyIsNPC = true;

			if ( npc.CurHealth == 0 )
				enemyIsDead = true;
		}

		bool hasEnemy = PrimaryTarget.HasValue && !enemyIsDead;
		bool tracking = PrimaryTargetVisible;
		float lostTime = hasEnemy ? PrimaryTarget.Value.LostTime : float.MaxValue;
		float dist = Owner.Blackboard.enemyDistance;

		Owner.WorldState.Set( AIFacts.HasEnemy, hasEnemy );
		Owner.WorldState.Set( AIFacts.EnemyIsNPC, enemyIsNPC );
		Owner.WorldState.Set( AIFacts.EnemyIsPlayer, enemyIsPlayer );

		Owner.WorldState.Set( AIFacts.ThreatEliminated, !hasEnemy );
		Owner.WorldState.Set( AIFacts.EnemyVisible, tracking );

		// Alert stays true for LostTargetTimeout seconds after losing sight
		bool alert = hasEnemy && lostTime <= LostTargetTimeout || Owner.Blackboard.lastCombatSoundHeard < 15 && Owner.Blackboard.lastCombatSoundHeard is not null || (Owner.AISquad is not null && Owner.WorldState.Get( "squadHasEnemyContact" ));
		Owner.WorldState.Set( AIFacts.Alert, alert );
		Owner.BodyModel.Set( "b_IsMad", alert );

		// we have a last known position even if not currently visible
		bool hasLKP = hasEnemy && !tracking && Owner.EnemyLKP.HasValue && Owner.EnemyLKP is not null;
		Owner.WorldState.Set( AIFacts.HasEnemyLKP, hasLKP );
		Owner.WorldState.Set( AIFacts.SearchingForEnemy, hasLKP && !tracking );

		// attack and self preservation facts. only meaningful when tracking
		if ( tracking )
		{
			var def = Owner.Definition;
			Owner.WorldState.Set( AIFacts.EnemyInRangeAttack1, dist <= def.RangeAttack1_Distance && Owner.CanRangeAttack1() );
			Owner.WorldState.Set( AIFacts.EnemyInRangeAttack2, dist <= def.RangeAttack2_Distance && Owner.CanRangeAttack2() );
			Owner.WorldState.Set( AIFacts.EnemyInMeleeAttack1, dist <= def.MeleeAttack1_Distance );
			Owner.WorldState.Set( AIFacts.EnemyInMeleeAttack2, dist <= def.MeleeAttack2_Distance && Owner.CanMeleeAttack2() );
			Owner.WorldState.Set( AIFacts.EnemyTooClose, dist <= 80f );
		}
		else
		{
			Owner.WorldState.Set( AIFacts.EnemyInRangeAttack1, false );
			Owner.WorldState.Set( AIFacts.EnemyInRangeAttack2, false );
			Owner.WorldState.Set( AIFacts.EnemyInMeleeAttack1, false );
			Owner.WorldState.Set( AIFacts.EnemyInMeleeAttack2, false );
			Owner.WorldState.Set( AIFacts.EnemyTooClose, false );
		}


		Owner.WorldState.Set( AIFacts.InRange1Cooldown, WorldTime.Now <= Owner.NextRange1AttackTime );

		float healthPct = Owner.CurHealth / MathF.Max( Owner.MaxHealth, 1f );
		Owner.WorldState.Set( AIFacts.LowHealth, healthPct <= 0.35f );
		Owner.WorldState.Set( AIFacts.CriticalHealth, healthPct <= 0.15f );
	}


	private void RefreshCombatMemory( SensorData t )
	{
		var mem = Owner.Blackboard.combatMemory ??= new CombatMemory();
		mem.Enemy = t.Target;
		mem.LastKnownPosition = t.LastKnownPosition;
		mem.LastKnownVelocity = t.LastKnownVelocity;
		mem.LastSeenTime = WorldTime.Now;
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
			if ( WorldTime.Now - kvp.Value.TimeHeard > kvp.Value.TimeToForget ||
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

	private static Material _ghost => Material.Load( "materials/dev/ghost.vmat" );

	public void DrawGizmos()
	{
		if ( !AIManager.AIDebugVisionSensing ) return;

		var eye = Owner.BodyModel.GetAttachment( Owner.GetEyeAttachmentName() );
		var eyePos = new Vector3();
		var eyeFwd = new Vector3();
		var eyeUp = new Vector3();
		var eyeRight = new Vector3();



		eyePos = Owner.WorldPosition + (Vector3.Up * (Owner.BodyModel.Bounds.Size.z * 0.8f)); // approximate
		eyeFwd = Owner.WorldRotation.Forward;
		eyeUp = Owner.WorldRotation.Up;
		eyeRight = Owner.WorldRotation.Right;


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
			if ( t.Target is BasePlayer ply )
			{
				var velocity = t.LastKnownVelocity;

				var rotation = velocity.IsNearZeroLength
					? t.Target.WorldRotation
					: Rotation.LookAt( velocity.Normal );

				Gizmo.Draw.Model(
					"models/editor/playerstart.vmdl",
					new Transform( t.LastKnownPosition, rotation )
#if IGNIS
					,
					_ghost
#endif
				);
			}
			else if ( t.Target is AIController npc )
			{
				Gizmo.Draw.Model( npc.Definition.Models.FirstOrDefault().Name );
			}

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
		float step = MathF.PI * 2f / segments;

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
