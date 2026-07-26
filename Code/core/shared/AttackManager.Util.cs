namespace Core;

using System;
using AI;

partial class AttackManager
{
	/// <summary>
	/// Convert pounds to kilos
	/// </summary>
	/// <param name="LBS">Pounds</param>
	/// <returns>Kilos</returns>
	public static float LBStoKG( float LBS ) => LBS * 0.453f;
	/// <summary>
	/// Straight HL2 port im sorry valve and Jay Stelly
	/// </summary>
	/// <param name="grains">The grains</param>
	/// <returns>Mass in Pounds</returns>
	public static float BulletMassGrainsToLbs( float grains ) => 0.002285f * grains / 16.0f;
	/// <summary>
	/// Straight HL2 port im sorry valve and Jay Stelly
	/// </summary>
	/// <param name="grains">The grains</param>
	/// <returns>Mass in KG</returns>
	public static float BulletMassGrainsToKg( float grains ) => LBStoKG( BulletMassGrainsToLbs( grains ) );
	/// <summary>
	/// Convert a velocity in ft/sec and a mass in grains to an impulse in kg in/s
	/// </summary>
	/// <param name="grains">Grain amount</param>
	/// <param name="ftpersec">Feet per second</param>
	/// <param name="exaggerate">A force multiplier</param>
	/// <returns></returns>
	public static float BulletImpulse( float grains, float ftpersec, float exaggerate ) => ftpersec * 12 * BulletMassGrainsToKg( grains ) * exaggerate;

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

		if ( tr.Component is Collider collider && collider.IsTrigger && !BasePlayer.Local.GameObject.IsDescendant( tr.GameObject ) ) // if its a trigger it has to be the player
			return false;

		return true;
	}

	public static Vector3 CalculateSpread( Transform transform, float spreadFromWeaponData = 0f )
	{
		// calculate spread modifier
		Vector3 spread = transform.Forward;
		spread += Vector3.Random * MathF.Round( MathF.Sin( MathX.DegreeToRadian( spreadFromWeaponData * 0.5f ) ), 5 );
		return spread.Normal;
	}

	public static void HandleHitBullet( AttackResult attack, CoreDamageInfo coreDamageInfo, Transform transform )
	{
		coreDamageInfo.Position = attack.Last.HitPosition;
		coreDamageInfo.Force = -attack.Last.Normal.LerpTo( transform.Backward, 0.5f ).Normal;

		GameObject bone = null;
		bool isSkinned = false;
		bool isNPC = false;
		Color color = Color.White;

		// if hit a skinned renderer, apply decal to the bone object, instead of anything else
		if ( attack.Last.GameObject.Components.TryGet<SkinnedModelRenderer>( out var skinned, FindMode.EverythingInSelfAndAncestors ) )
		{
			var boneobj = skinned.GetBoneObject( attack.Last.Hitbox?.Bone );

			if ( boneobj.IsValid() )
			{
				isSkinned = true;
				bone = boneobj;
			}
			//	else { Log.Warning( "whatever SkinnedModelRenderer you tried to hit didn't have a hitbox or a bone object" ); }
		}

		// if hit an npc, check what color its blood is
		if ( attack.Last.GameObject.Components.TryGet<AIController>( out var ai, FindMode.EverythingInSelfAndAncestors ) )
		{
			isNPC = true;
			color = Effects.BloodColor.ConvertColor( ai.Definition.BloodColor, !ai.IsAlive );
		}

		var ammo = coreDamageInfo.Ammo;

		// apply force to all rigidbodies in the object that was hit
		var rigid = attack.Last.GameObject.GetComponents<Rigidbody>();
		foreach ( var physics in rigid )
			physics.ApplyImpulseAt( coreDamageInfo.Position, coreDamageInfo.Force * BulletImpulse( ammo.Grains, ammo.FtPerSec, 1.25f ) * 2.2f );  // magic bullshit lbs/kg number

		// Create all the effects
		DebrisManager.CreateBulletDecal( attack.Last.EndPosition,
													transform.Forward.LerpTo( -attack.Last.Normal, 0.75f ).Normal,
													attack.Last.Surface,
													isSkinned ? bone : attack.Last.Component.GameObject, ammo.HoleSize );

		DebrisManager.CreateBulletImpact( attack.Last.EndPosition,
													(-transform.Forward).LerpTo( -attack.Last.Normal, 0.8f ).Normal,
													attack.Last.Surface, isNPC, color );

		DebrisManager.CreateHitSound( attack.Last.EndPosition,
												attack.Last.Surface, attack.Last.GameObject );


		NpcSoundManager.AddSound( NpcSoundManager.SoundType.SOUND_GUNFIRE, transform.Position, coreDamageInfo.Inflictor );
	}
}
