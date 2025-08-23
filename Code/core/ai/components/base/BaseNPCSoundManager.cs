using System;

public class NpcSoundManager : Component
{
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

	public static NpcSoundManager StaticRef { get; set; }
	[Property] public bool DrawDebug { get; set; } = false;
	[Property, ReadOnly] public Dictionary<Guid, NpcSound> NpcSounds { get; set; } = new Dictionary<Guid, NpcSound>();

	public NpcSoundManager()
	{
		StaticRef = this;
	}

	public static Guid AddSound( SoundType type, Vector3 position, GameObject owner )
	{
		NpcSound sound = new();
		sound.SoundType = type;
		sound.Position = position;
		sound.Owner = owner;
		sound.MaxRadius = StaticRef.GetMaxRadius( type );
		sound.Timeline = 0f;
		sound.CurrentRadius = StaticRef.GetSoundRadius( type, sound.Timeline ) * sound.MaxRadius;
		sound.Duration = StaticRef.GetDuration( type );

		var id = Guid.NewGuid();
		StaticRef.NpcSounds.Add( id, sound );

		return id;
	}

	private float GetDuration( SoundType type )
	{
		if ( type == SoundType.STENCH_CARCASS )
			return 500f;
		else if ( type == SoundType.ALERT_TALKER_CONCEPT )
			return 1f;
		else if ( type.ToString().StartsWith( "SOUND_" ) )
			return 0.1f;
		else if ( type.ToString().StartsWith( "ALERT_" ) )
			return 0.2f;
		else if ( type.ToString().StartsWith( "STENCH_" ) )
			return 30f;

		return 1f;
	}

	private float GetMaxRadius( SoundType type )
	{
		if ( type == SoundType.SOUND_GUNFIRE )
			return 1500f;
		else if ( type == SoundType.SOUND_BULLET_IMPACT )
			return 90f;
		else if ( type == SoundType.ALERT_TALKER_CONCEPT )
			return 400f;
		else if ( type.ToString().StartsWith( "SOUND_" ) )
			return 512f;
		else if ( type.ToString().StartsWith( "ALERT_" ) )
			return 128f;
		else if ( type.ToString().StartsWith( "STENCH_" ) )
			return 256f;

		return 256f;
	}

	private float GetRegisterTime( SoundType type )
	{
		if ( type.ToString().StartsWith( "STENCH_" ) )
			return 4f;

		return 0f;
	}

	private float GetForgetTime( SoundType type )
	{
		if ( type.ToString().StartsWith( "STENCH_" ) )
			return 10f;

		return 5f;
	}

	private float GetSoundRadius( SoundType type, float timeline )
	{
		if ( type.ToString().StartsWith( "SOUND_" ) )
			return 1f;
		else if ( type.ToString().StartsWith( "ALERT_" ) )
			return MathF.Sqrt( timeline );
		else if ( type.ToString().StartsWith( "STENCH_" ) )
			return MathF.Pow( MathF.Sin( MathF.Pow( timeline, 0.8f ) * MathF.PI ), 0.6f );

		return 1f;
	}

	/*public SoundType GetClosestSound()
	{
		foreach ( var sound in NpcSounds )
		{
			var snd = sound.Value;
			snd.Position;
			return snd;
		}
	}*/

	protected override void OnFixedUpdate()
	{
		List<Guid> kill = new();
		foreach ( var sound in NpcSounds )
		{
			var snd = sound.Value;
			snd.Timeline = Math.Clamp( sound.Value.Timeline + Time.Delta / sound.Value.Duration, 0f, 1f );
			snd.CurrentRadius = GetSoundRadius( snd.SoundType, snd.Timeline ) * snd.MaxRadius;
			//send to npcs
			foreach ( var npc in Scene.Components.GetAll<NpcTargeting>() )
			{
				if ( npc.GetEyeTransform().Position.Distance( snd.Position ) < snd.CurrentRadius && !npc.KnownSounds.ContainsKey( sound.Key ) )
				{
					NpcTargeting.NpcSoundMemory sndmem = new();
					sndmem.SoundType = snd.SoundType;
					sndmem.Position = snd.Position;
					sndmem.Owner = snd.Owner;
					sndmem.TimeToRegister = GetRegisterTime( snd.SoundType );
					sndmem.TimeToForget = GetForgetTime( snd.SoundType );
					sndmem.TimeHeard = Time.Now;
					npc.KnownSounds.Add( sound.Key, sndmem );
				}
			}

			if ( snd.Timeline == 1f )
				kill.Add( sound.Key );
			NpcSounds[sound.Key] = snd;
		}
		foreach ( var sound in kill )
			NpcSounds.Remove( sound );
		base.OnFixedUpdate();
	}

	protected override void OnUpdate()
	{
		if ( DrawDebug )
		{
			foreach ( var sound in NpcSounds )
			{
				Gizmo.Draw.Color = Gizmo.Colors.Blue;
				if ( sound.Value.SoundType.ToString().StartsWith( "STENCH_" ) )
					Gizmo.Draw.Color = Gizmo.Colors.Green;
				else if ( sound.Value.SoundType.ToString().StartsWith( "ALERT_" ) )
					Gizmo.Draw.Color = Gizmo.Colors.Red;
				Gizmo.Draw.IgnoreDepth = true;
				Gizmo.Draw.Text( sound.Value.SoundType.ToString(), new Transform( sound.Value.Position ) );
				Gizmo.Draw.IgnoreDepth = false;
				Gizmo.Draw.LineSphere( sound.Value.Position, sound.Value.CurrentRadius );
			}
		}
		base.OnUpdate();
	}
}
