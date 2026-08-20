namespace Core;

using AI;
using Sandbox.Utility;
using System;

public static partial class AttackManager
{
	[ConVar( "debug_damage_events" )] public static bool DebugDamageEvents { get; set; }

	[ConVar( "debug_attack_traces" )] public static bool DebugAttackTraces { get; set; }

	/// <summary>
	/// Main attack struct, has a bool for successful hit and the trace itself
	/// </summary>
	public struct AttackResult
	{
		/// <summary>
		/// Did we hit anything
		/// </summary>
		public bool Hit { get; set; }
		/// <summary>
		/// Whatever we hit
		/// </summary>
		public SceneTraceResult Last { get; set; }
	}

	public struct AttackProjectile
	{
		/// <summary>
		/// Has this projectile hit anything ever so far
		/// </summary>
		public AttackResult Result { get; set; }
		/// <summary>
		/// Doesn't allow to call it "projectile" but it is it
		/// </summary>
		public BaseProjectile Object { get; set; }
	}

	/// <summary>
	/// Main projectile function to start the projectile
	/// </summary>
	/// <param name="transform"></param>
	/// <param name="damageinfo"></param>
	/// <param name="spreadFromWeaponData"></param>
	/// <returns></returns>
	public static AttackResult FireProjectile( Transform transform, DamageInfo damageinfo, float spreadFromWeaponData = 0f )
	{
		Vector3 spread = CalculateSpread( transform, spreadFromWeaponData );

		AttackProjectile attack = new();

		if ( damageinfo is CoreDamageInfo coreDamageInfo )
		{
			NpcSoundManager.AddSound( NpcSoundManager.SoundType.SOUND_GUNFIRE, transform.Position, coreDamageInfo.Inflictor );

			transform.Rotation = (transform.Forward + spread).EulerAngles;

			DebrisManager.CreateProjectileObject( coreDamageInfo, transform, out var bullet, spread );
			attack.Object = bullet;
			attack.Result = bullet.Result;

			// TODO: make this not BasePlayer.Local
			Transform muzzleTrans = BaseCombatWeapon.GetMuzzleAttachObject( BasePlayer.Local, "muzzle", out var attachmentObj );

			if ( attachmentObj.IsValid() )
			{
				DebrisManager.Instance.CreateBulletTracer( coreDamageInfo?.Attacker,
								coreDamageInfo?.BaseCombatWeapon.WeaponData,
								muzzleTrans.Position,
								muzzleTrans.Forward + spread );
			}
		}

		return attack.Result;
	}

	/////////////////////////////////////////////////////////////////////////////////////
	// 
	//	RunSceneTrace() -> TraceGenericAttack() -> FireHitscan() / FireProjectile()
	//
	/////////////////////////////////////////////////////////////////////////////////////

	/// <summary>
	/// Main hitscan bullet function with all the additional effects
	/// </summary>
	/// <param name="transform">Where</param>
	/// <param name="damageinfo">Damage information</param>
	/// <param name="spreadFromWeaponData">Optional spread</param>
	/// <returns>The fired bullet</returns>
	public static AttackResult FireHitscan( Transform transform, DamageInfo damageinfo, float spreadFromWeaponData = 0f )
	{
		Vector3 spread = CalculateSpread( transform, spreadFromWeaponData );
		AttackResult attack = TraceGenericAttack( new Ray( transform.Position, transform.Forward + spread ), 4096f, damageinfo );

		// cast regular DamageInfo to our expanded DamageInfo, otherwise we can't pass it to engine stuff that expects the base class (even if its inherited)
		if ( damageinfo is CoreDamageInfo coreDamageInfo )
		{
			NpcSoundManager.AddSound( NpcSoundManager.SoundType.SOUND_GUNFIRE, transform.Position, coreDamageInfo.Inflictor );

			if ( attack.Hit )
			{
				HandleHitBullet( attack, coreDamageInfo, transform );

				DebrisManager.Instance.CreateBulletTracer( coreDamageInfo?.Attacker,
											coreDamageInfo?.BaseCombatWeapon.WeaponData,
											attack.Last.EndPosition,
											(-transform.Forward).LerpTo( -attack.Last.Normal, 0.8f ).Normal );

				//	DebrisManager.Instance.CreateMuzzleflash( coreDamageInfo?.BaseCombatWeapon.WeaponData,
				//												attack.Last.EndPosition ); // why is MUZZLE flash created at bullet hit position 🤨
			}

			// second trace for near misses (sounds or some other effects)
			List<GameObject> npcs = [];
			foreach ( var tr2 in Game.ActiveScene.Trace.Ray( transform.Position, transform.Position + transform.Forward * 5000f ).Radius( 40f ).UseHitboxes().IgnoreGameObjectHierarchy( coreDamageInfo.Attacker ).RunAll() )
			{
				if ( tr2.Hit && !npcs.Contains( tr2.GameObject ) && tr2.GameObject.Components.Get<AIController>().IsValid() )
				{
					NpcTargetingSensor.AISoundMemory sndmem = new()
					{
						SoundType = NpcSoundManager.SoundType.ALERT_BULLET_NEAR_MISS,
						Position = tr2.StartPosition,
						Owner = coreDamageInfo.Inflictor,
						//	TimeToRegister = 0f,
						TimeToForget = 6f
					};

					npcs.Add( tr2.GameObject );
					tr2.GameObject.Components.Get<NpcTargetingSensor>().KnownSounds.Add( Guid.NewGuid(), sndmem );
				}
			}
			return attack;
		}
		else
		{
			// have to do this because of casting
			// pass at least something in case if it is for some reason default class
			damageinfo.Position = attack.Last.HitPosition;
			damageinfo.Hitbox = attack.Last.Hitbox;
			return attack;
		}
	}

	/// <summary>
	/// A wrapper for SceneTrace to add debug and simplify input
	/// </summary>
	/// <param name="ray">Direction</param>
	/// <param name="distance">How far</param>
	/// <param name="damage">Damage Information</param>
	/// <returns>The trace</returns>
	public static AttackResult TraceGenericAttack( Ray ray, float distance, DamageInfo damage ) => RunSceneTrace( Game.ActiveScene.Trace.Ray( ray, distance ), damage );
	/// <summary>
	/// A wrapper for SceneTrace to add debug and simplify input
	/// </summary>
	/// <param name="capsule">Capsule trace</param>
	/// <param name="damage">Damage information</param>
	/// <returns>The trace</returns>
	public static AttackResult TraceGenericAttack( Capsule capsule, DamageInfo damage )
	{
		if ( DebugDamageEvents ) DebugOverlaySystem.Current.Capsule( capsule, Color.Green, 10, default, true );
		return RunSceneTrace( Game.ActiveScene.Trace.Capsule( capsule ), damage );
	}

	/// <summary>
	/// The main tracing function
	/// </summary>
	/// <param name="trace">The trace</param>
	/// <param name="damage">Damage information</param>
	/// <returns>AttackResult struct</returns>
	private static AttackResult RunSceneTrace( SceneTrace trace, DamageInfo damage )
	{
		AttackResult result = new() // make an empty default one
		{
			Hit = false,
			Last = new() { Scene = Game.ActiveScene }
		};

		var tr = trace // do the trace
		.UseHitboxes()
		.HitTriggers()
		.WithoutTags( "trigger", "skinned_collider", "passbullets" )
		.IgnoreGameObjectHierarchy( damage.Attacker )
		.UseHitPosition()
		.RunAll();

		if ( tr.Any() )
		{
			//			tr = tr.OrderByDescending( x => x.Hitbox is not null );
			var list = tr.ToList();
			list.Sort( ( yesnull, nonull ) => (nonull.Hitbox is not null).CompareTo( yesnull.Hitbox is not null ) ); // better than linq?

			foreach ( var traceHit in list )
			{
				// filter the bad first
				if ( !IsDamageValid( traceHit, damage ) ) continue;

				if ( DebugHitResult )
				{
					Log.Info( $"Struct: {traceHit} / Component: {traceHit.Component} / Hitbox: {traceHit.Hitbox}" );
					Log.Info( " " );
				}

				result.Hit = true; // because of RunAll, if we are here then its true anyway
				result.Last = traceHit;

				if ( traceHit.Hitbox is not null || traceHit.Component is Collider || traceHit.Component is Rigidbody ) break;
			}
		}

		if ( result.Hit ) // we hit, fill us with data and do the things
		{
			if ( DebugDamageEvents ) DebrisManager.Instance.DebugOverlay.Trace( result.Last, 15, false );

			damage.Position = result.Last.HitPosition;

			if ( damage is CoreDamageInfo coreDamageInfo )
			{
				foreach ( var candamage in result.Last.GameObject.Components.GetAll<Component.IDamageable>( FindMode.EverythingInSelfAndAncestors ) ) candamage.OnDamage( coreDamageInfo );
			}
		}

		return result;
	}

	[ConVar( "debug_hit_result" )] public static bool DebugHitResult { get; set; }

	/// <summary>
	/// Trace an explosion instead of a ray, apply damage and force to all objects
	/// </summary>
	/// <param name="position">Where was the explosion</param>
	/// <param name="range">Radius of the explosion</param>
	/// <param name="damage">Damage Information</param>
	/// <param name="attackerIgnoresDamage">Does this affect the bomb</param>
	public static void TraceExplosion( Vector3 position, float range, DamageInfo damage, bool attackerIgnoresDamage = false )
	{
		if ( DebugDamageEvents )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.LineSphere( position, range );
		}

		var maxdamage = damage.Damage;

		if ( damage is CoreDamageInfo coreDamageInfo )
		{
			foreach ( var tr in Game.ActiveScene.Trace.Sphere( range, position, position )
			.HitTriggers()
			.WithoutTags( "trigger", "skinned_collider" )
			.UseHitboxes()
			.RunAll() )
			{
				if ( !IsDamageValid( tr, damage ) )
					continue;

				coreDamageInfo.Position = tr.HitPosition;
				coreDamageInfo.Damage = (int)(Easing.ExpoOut( tr.HitPosition.Distance( position ).Remap( 0f, range, 1f, 0f ) ) * maxdamage);
				coreDamageInfo.Force = (tr.GameObject.WorldPosition + Vector3.Up * 40f - position).Normal * 450f;
				coreDamageInfo.Force *= damage.Damage / maxdamage;

				foreach ( var candamage in tr.GameObject.Components.GetAll<Component.IDamageable>( FindMode.EverythingInSelfAndAncestors ) ) candamage.OnDamage( coreDamageInfo );
			}
		}
	}
}
