using Core;
using Microsoft.VisualBasic;
using Sandbox.Utility;
using System;

public class AttackManager
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
		/// The entire trace, including the last hit object
		/// </summary>
		public SceneTraceResult Last { get; set; }
	}

	/// <summary>
	/// Main bullet function with all the additional effects
	/// </summary>
	/// <param name="transform">Where</param>
	/// <param name="damageinfo">Damage information</param>
	/// <param name="spreadFromWeaponData">Optional spread</param>
	/// <returns>The fired bullet</returns>
	public static AttackResult FireBullet( Transform transform, DamageInfo damageinfo, float spreadFromWeaponData = 0f )
	{
		// calculate spread modifier
		var spread = transform.Forward;
		spread += Vector3.Random * MathF.Round( MathF.Sin( MathX.DegreeToRadian( spreadFromWeaponData / 2 ) ), 5 );
		spread = spread.Normal;

		var attack = TraceGenericAttack( new Ray( transform.Position + spread, transform.Forward + spread ), 4096f, damageinfo );

		// cast regular DamageInfo to our expanded DamageInfo, otherwise we can't pass it to engine stuff that expects the base class (even if its inherited)
		if ( damageinfo is CoreDamageInfo coreDamageInfo )
		{
			NpcSoundManager.AddSound( NpcSoundManager.SoundType.SOUND_GUNFIRE, transform.Position, coreDamageInfo.Inflictor );
			coreDamageInfo.Position = attack.Last.HitPosition;
			coreDamageInfo.Force = -attack.Last.Normal.LerpTo( transform.Backward, 0.5f ).Normal * 50f;
			coreDamageInfo.Hitbox = attack.Last.Hitbox;

			if ( attack.Hit )
			{
				Material mat = null;

				if ( attack.Last.Component is MeshComponent mesh ) mat = mesh.GetMaterial( 1 );
				else if ( attack.Last.Component is ModelRenderer model ) mat = model.GetMaterial();

				GameObject bone = null;
				bool isSkinned = false;

				// if hit a skinned renderer, apply decal to the bone object, instead of anything else
				if ( attack.Last.Component.GameObject.Components.TryGet<SkinnedModelRenderer>( out var skinned, FindMode.EverythingInSelfAndAncestors ) )
				{
					if ( attack.Last.Component.GameObject.Tags.HasAny( "npc", "skinned_collider", "ragdoll" ) )
					{
						var boneobj = skinned.GetBoneObject( attack.Last.Hitbox.Bone ); // a slight mess but triple layer anti-NRE is better than none
						if ( boneobj != null )
						{
							isSkinned = true;
							bone = boneobj;
						}
						else
						{
							Log.Warning( "whatever SkinnedModelRenderer you tried to hit didn't have a hitbox or a bone object" );
						}
					}
				}

				//	Log.Info( isSkinned );
				//	Log.Info( bone );

				var ammo = coreDamageInfo.Ammo;

				// apply force to all rigidbodies in the object that was hit
				var rigid = attack.Last.GameObject.GetComponents<Rigidbody>();
				foreach ( var physics in rigid )
					physics.ApplyForceAt( coreDamageInfo.Position, coreDamageInfo.Force * BulletImpulse( ammo.Grains, ammo.FtPerSec, 1.25f ) * physics.PhysicsBody.Mass );  // the value feels wrong and is not consistent between props at all

				//	Log.Info( attack.Last.Surface );

				// Create all the effects

				DebrisManager.StaticRef.CreateBulletTracer( coreDamageInfo?.Attacker,
															coreDamageInfo?.BaseCombatWeapon.WeaponData,
															attack.Last.EndPosition,
															(-transform.Forward).LerpTo( -attack.Last.Normal, 0.8f ).Normal );

				DebrisManager.StaticRef.CreateMuzzleflash( coreDamageInfo?.BaseCombatWeapon.WeaponData,
														    attack.Last.EndPosition );

				DebrisManager.StaticRef.CreateBulletDecal( attack.Last.EndPosition,
															transform.Forward.LerpTo( -attack.Last.Normal, 0.75f ).Normal,
															attack.Last.Surface,
															isSkinned ? bone : attack.Last.Component.GameObject, ammo.HoleSize );

				DebrisManager.StaticRef.CreateBulletImpact( attack.Last.EndPosition,
															(-transform.Forward).LerpTo( -attack.Last.Normal, 0.8f ).Normal,
															attack.Last.Surface, mat );

				DebrisManager.StaticRef.CreateHitSound( attack.Last.EndPosition,
														attack.Last.Surface, attack.Last.GameObject );


				NpcSoundManager.AddSound( NpcSoundManager.SoundType.SOUND_BULLET_IMPACT, attack.Last.EndPosition, coreDamageInfo.Inflictor );
			}

			// second trace for near misses (sounds or some other effects)
			List<GameObject> npcs = new();
			foreach ( var tr2 in Game.ActiveScene.Trace.Ray( transform.Position, transform.Position + transform.Forward * 5000f ).Radius( 40f ).UseHitboxes().IgnoreGameObjectHierarchy( coreDamageInfo.Attacker ).RunAll() )
			{
				if ( tr2.Hit && !npcs.Contains( tr2.GameObject ) && tr2.GameObject.Components.Get<NpcTargeting>() != null )
				{
					NpcTargeting.NpcSoundMemory sndmem = new();
					sndmem.SoundType = NpcSoundManager.SoundType.ALERT_BULLET_NEAR_MISS;
					sndmem.Position = tr2.StartPosition;
					sndmem.Owner = coreDamageInfo.Inflictor;
					sndmem.TimeToRegister = 0f;
					sndmem.TimeToForget = 6f;

					npcs.Add( tr2.GameObject );
					tr2.GameObject.Components.Get<NpcTargeting>().KnownSounds.Add( Guid.NewGuid(), sndmem );
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
	/// Filter out the stuff we don't want from the Damage
	/// </summary>
	/// <param name="tr">Trace</param>
	/// <param name="damage">Damage information</param>
	/// <returns>Is the damage good (didn't filter anything) or not</returns>
	public static bool IsDamageValid( SceneTraceResult tr, DamageInfo damage )
	{
		if ( damage.Attacker.IsDescendant( tr.GameObject ) ) // dont hit yourself
			return false;

		var collider = tr.Component as Collider;
		if ( collider != null && collider.IsTrigger && !BasePlayer.Local.GameObject.IsDescendant( tr.GameObject ) ) // if its a trigger it has to be the player
			return false;

		return true;
	}
	/// <summary>
	/// The main tracing function
	/// </summary>
	/// <param name="trace">The trace</param>
	/// <param name="damage">Damage information</param>
	/// <returns>AttackResult struct</returns>
	protected static AttackResult RunSceneTrace( SceneTrace trace, DamageInfo damage )
	{
		AttackResult ret = new();

		if ( damage is CoreDamageInfo coreDamageInfo )
		{
			foreach ( var tr in trace
			.UseHitboxes()
			.HitTriggers()
			.WithoutTags( "trigger", "skinned_collider" )
			.IgnoreGameObjectHierarchy( damage.Attacker )
			.RunAll() )
			{
				if ( !IsDamageValid( tr, damage ) )
					continue;

				damage.Position = tr.HitPosition;

				// this damage loop is in every step of a trace including this but im not sure which one to leave in
				var alldmg = tr.GameObject.GetComponents<Component.IDamageable>();
				foreach ( var candamage in alldmg )
					candamage.OnDamage( coreDamageInfo );

				ret.Hit = true;
				ret.Last = tr;
				break;
			}
			return ret;
		}
		return ret;
	}

	/// <summary>
	/// A wrapper for SceneTrace to add debug and simplify input
	/// </summary>
	/// <param name="ray">Direction</param>
	/// <param name="distance">How far</param>
	/// <param name="damage">Damage Information</param>
	/// <returns>The trace</returns>
	public static AttackResult TraceGenericAttack( Ray ray, float distance, DamageInfo damage )
	{
		if ( DebugDamageEvents )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.Line( ray.Position, ray.Project( distance ) );
		}
		return RunSceneTrace( Game.ActiveScene.Trace.Ray( ray, distance ), damage );
	}
	/// <summary>
	/// A wrapper for SceneTrace to add debug and simplify input
	/// </summary>
	/// <param name="capsule">Capsule trace</param>
	/// <param name="damage">Damage information</param>
	/// <returns>The trace</returns>
	public static AttackResult TraceGenericAttack( Capsule capsule, DamageInfo damage )
	{
		if ( DebugDamageEvents )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.LineCapsule( capsule );
		}
		return RunSceneTrace( Game.ActiveScene.Trace.Capsule( capsule ), damage );
	}
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
			foreach ( var tr in Game.ActiveScene.Trace.Sphere( range, position, position ).HitTriggers().UseHitboxes().RunAll() )
			{
				if ( !IsDamageValid( tr, damage ) )
					continue;

				coreDamageInfo.Position = tr.HitPosition;
				coreDamageInfo.Damage = (int)(Easing.ExpoOut( tr.HitPosition.Distance( position ).Remap( 0f, range, 1f, 0f ) ) * maxdamage);
				coreDamageInfo.Force = (tr.GameObject.WorldPosition + Vector3.Up * 40f - position).Normal * 450f;
				coreDamageInfo.Force *= damage.Damage / maxdamage;

				var alldmg = tr.GameObject.GetComponents<Component.IDamageable>();
				foreach ( var candamage in alldmg )
					candamage.OnDamage( coreDamageInfo );
			}
		}
	}

	/// <summary>
	/// Convert pounds to kilos
	/// </summary>
	/// <param name="LBS">Pounds</param>
	/// <returns>Kilos</returns>
	public static float LBStoKG( float LBS )
	{
		return LBS * 0.453f;
	}
	/// <summary>
	/// Straight HL2 port im sorry valve and Jay Stelly
	/// </summary>
	/// <param name="grains">The grains</param>
	/// <returns>Mass in Pounds</returns>
	public static float BulletMassGrainsToLbs( float grains )
	{
		return 0.002285f * (grains) / 16.0f;
	}
	/// <summary>
	/// Straight HL2 port im sorry valve and Jay Stelly
	/// </summary>
	/// <param name="grains">The grains</param>
	/// <returns>Mass in KG</returns>
	public static float BulletMassGrainsToKg( float grains )
	{
		return LBStoKG( BulletMassGrainsToLbs( grains ) );
	}
	/// <summary>
	/// Convert a velocity in ft/sec and a mass in grains to an impulse in kg in/s
	/// </summary>
	/// <param name="grains">Grain amount</param>
	/// <param name="ftpersec">Feet per second</param>
	/// <param name="exaggerate">A force multiplier</param>
	/// <returns></returns>
	public static float BulletImpulse( float grains, float ftpersec, float exaggerate )
	{
		return (ftpersec) * 12 * BulletMassGrainsToKg( grains ) * exaggerate;
	}
}
