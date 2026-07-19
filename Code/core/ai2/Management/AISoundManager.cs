using System;
namespace Core.AI;

public class NpcSoundManager : GameObjectSystem<NpcSoundManager>
{
	public NpcSoundManager( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishFixedUpdate, 0, Tick, "AISoundManager Tick" );
	}

	public enum SoundType
	{
		SOUND_WORLD,
		SOUND_PLAYER,
		SOUND_PLAYER_VEHICLE,
		SOUND_PHYSICS,
		SOUND_GUNFIRE,
		SOUND_FOOTSTEP,
		SOUND_COMBAT,
		SOUND_BULLET_IMPACT,
		SOUND_PHYSICS_DANGER,
		SOUND_GRENADE,
		SOUND_DANGER,
		SOUND_THUMPER,
		STENCH_CARCASS,
		STENCH_MEAT,
		STENCH_GARBAGE,
		STENCH_ZOMBIE,
		ALERT_DANGER_CLOSE,
		ALERT_TALKER_CONCEPT,
		ALERT_BULLET_NEAR_MISS,
	}
	public struct NpcSound
	{
		public SoundType SoundType;
		public Vector3 Position;
		public GameObject Owner;
		public float MaxRadius;
		public float Duration;

		public float CurrentRadius;
		public float Timeline;
	}
	[Property] public bool DrawDebug { get; set; } = false;
	[Property, ReadOnly] public Dictionary<Guid, NpcSound> NpcSounds { get; set; } = [];

	public static Guid AddSound( SoundType type, Vector3 position, GameObject owner )
	{
		NpcSound sound = new()
		{
			SoundType = type,
			Position = position,
			Owner = owner,
			MaxRadius = GetMaxRadius( type ),
			Timeline = 0f
		};

		sound.CurrentRadius = GetSoundRadius( type, sound.Timeline ) * sound.MaxRadius;
		sound.Duration = GetDuration( type );

		var id = Guid.NewGuid();
		Current.NpcSounds.Add( id, sound );

		return id;
	}

	/// <summary>
	/// Is this a sound made by an Ally?
	/// </summary>
	/// <param name="type"></param>
	/// <returns></returns>
	public static bool IsAllySound( SoundType type )
	{
		switch ( type )
		{
			default:
				return false; // there are no ally sounds?
		}
	}

	private static float GetDuration( SoundType type )
	{
		return type switch
		{
			SoundType.STENCH_CARCASS => 500f,
			SoundType.ALERT_TALKER_CONCEPT => 1f,
			// SOUND_
			SoundType.SOUND_WORLD or
			SoundType.SOUND_PLAYER or
			SoundType.SOUND_PLAYER_VEHICLE or
			SoundType.SOUND_PHYSICS or
			SoundType.SOUND_GUNFIRE or
			SoundType.SOUND_FOOTSTEP or
			SoundType.SOUND_COMBAT or
			SoundType.SOUND_BULLET_IMPACT or
			SoundType.SOUND_PHYSICS_DANGER or
			SoundType.SOUND_GRENADE or
			SoundType.SOUND_DANGER or
			SoundType.SOUND_THUMPER => 1f,
			// ALERT_
			SoundType.ALERT_DANGER_CLOSE or
			SoundType.ALERT_BULLET_NEAR_MISS => 2f,
			// STENCH_
			SoundType.STENCH_MEAT or
			SoundType.STENCH_GARBAGE or
			SoundType.STENCH_ZOMBIE => 30f,
			_ => 1f,
		};
	}

	private static float GetMaxRadius( SoundType type )
	{
		return type switch
		{
			SoundType.SOUND_GUNFIRE => 2500f,
			SoundType.SOUND_FOOTSTEP => 420f,
			SoundType.SOUND_BULLET_IMPACT => 90f,
			SoundType.ALERT_TALKER_CONCEPT => 400f,
			// SOUND
			SoundType.SOUND_WORLD or
			SoundType.SOUND_PLAYER or
			SoundType.SOUND_PLAYER_VEHICLE or
			SoundType.SOUND_PHYSICS or
			SoundType.SOUND_COMBAT or
			SoundType.SOUND_PHYSICS_DANGER or
			SoundType.SOUND_GRENADE or
			SoundType.SOUND_DANGER or
			SoundType.SOUND_THUMPER => 512f,
			// ALERT
			SoundType.ALERT_DANGER_CLOSE or
			SoundType.ALERT_BULLET_NEAR_MISS => 128f,
			// STENCH
			SoundType.STENCH_CARCASS or
			SoundType.STENCH_MEAT or
			SoundType.STENCH_GARBAGE or
			SoundType.STENCH_ZOMBIE => 256f,
			_ => 256f,
		};
	}

	private static float GetRegisterTime( SoundType type )
	{
		return type switch
		{
			SoundType.STENCH_CARCASS or
			SoundType.STENCH_MEAT or
			SoundType.STENCH_GARBAGE or
			SoundType.STENCH_ZOMBIE => 4f,
			_ => 0f
		};
	}

	private static float GetForgetTime( SoundType type )
	{
		return type switch
		{
			SoundType.STENCH_CARCASS or
			SoundType.STENCH_MEAT or
			SoundType.STENCH_GARBAGE or
			SoundType.STENCH_ZOMBIE => 10f,
			_ => 5f,
		};
	}

	private static float GetSoundRadius( SoundType type, float timeline )
	{
		return type switch
		{
			// SOUND
			SoundType.SOUND_WORLD or
			SoundType.SOUND_PLAYER or
			SoundType.SOUND_PLAYER_VEHICLE or
			SoundType.SOUND_PHYSICS or
			SoundType.SOUND_GUNFIRE or
			SoundType.SOUND_FOOTSTEP or
			SoundType.SOUND_COMBAT or
			SoundType.SOUND_BULLET_IMPACT or
			SoundType.SOUND_PHYSICS_DANGER or
			SoundType.SOUND_GRENADE or
			SoundType.SOUND_DANGER or
			SoundType.SOUND_THUMPER => 1f,
			// ALERT
			SoundType.ALERT_DANGER_CLOSE or
			SoundType.ALERT_TALKER_CONCEPT or
			SoundType.ALERT_BULLET_NEAR_MISS => MathF.Sqrt( timeline ),
			// STENCH
			SoundType.STENCH_CARCASS or
			SoundType.STENCH_MEAT or
			SoundType.STENCH_GARBAGE or
			SoundType.STENCH_ZOMBIE => MathF.Pow( MathF.Sin( MathF.Pow( timeline, 0.8f ) * MathF.PI ), 0.6f ),
			_ => 1f,
		};
	}

	protected void Tick()
	{
		List<Guid> kill = [];
		foreach ( var sound in NpcSounds )
		{
			var snd = sound.Value;
			snd.Timeline = Math.Clamp( sound.Value.Timeline + Time.Delta / sound.Value.Duration, 0f, 1f );
			snd.CurrentRadius = GetSoundRadius( snd.SoundType, snd.Timeline ) * snd.MaxRadius;
			// send to npcs
			foreach ( var npc in Scene.Components.GetAll<AIController>() )
			{
				if ( npc.WorldPosition.Distance( snd.Position ) < snd.CurrentRadius && !npc.TargetingSensor.KnownSounds.ContainsKey( sound.Key ) )
				{
					NpcTargetingSensor.AISoundMemory sndmem = new()
					{
						SoundType = snd.SoundType,
						Position = snd.Position,
						Owner = snd.Owner,
						Registered = true,
						TimeToForget = GetForgetTime( snd.SoundType ),
						TimeHeard = WorldTime.Now
					};
					npc.TargetingSensor.KnownSounds.Add( sound.Key, sndmem );
				}
			}

			if ( snd.Timeline == 1f )
				kill.Add( sound.Key );
			NpcSounds[sound.Key] = snd;

		}

		foreach ( var sound in kill )
			NpcSounds.Remove( sound );
	}

	protected void DebugDraw()
	{
		if ( DrawDebug )
		{
			foreach ( var sound in NpcSounds )
			{
				var type = sound.Value.SoundType;

				Gizmo.Draw.Color = Gizmo.Colors.Blue;

				switch ( type )
				{
					// STENCH
					case SoundType.STENCH_CARCASS:
					case SoundType.STENCH_MEAT:
					case SoundType.STENCH_GARBAGE:
					case SoundType.STENCH_ZOMBIE:
						Gizmo.Draw.Color = Gizmo.Colors.Green;
						break;
					// ALERT
					case SoundType.ALERT_DANGER_CLOSE:
					case SoundType.ALERT_TALKER_CONCEPT:
					case SoundType.ALERT_BULLET_NEAR_MISS:
						Gizmo.Draw.Color = Gizmo.Colors.Red;
						break;
				}

				Gizmo.Draw.IgnoreDepth = true;

				Gizmo.Draw.Text( type.ToString(), new Transform( sound.Value.Position ) );

				Gizmo.Draw.IgnoreDepth = false;
				Gizmo.Draw.LineSphere( sound.Value.Position, sound.Value.CurrentRadius );
			}
		}
	}
}
