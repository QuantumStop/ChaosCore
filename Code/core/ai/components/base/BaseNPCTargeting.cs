using System;
using static NpcSoundManager;

public class NpcTargeting : Component
{
	public struct TargetData
	{
		public NpcRelations.TargetType Type;
		public NpcRelations Target;
		public bool Tracking;
		public Vector3 LastKnownPosition;
		public Vector3 LastKnownVelocity;
		public float LostTime;
	}
	public struct NpcSoundMemory
	{
		public NpcSoundManager.SoundType SoundType;
		public Vector3 Position;
		public GameObject Owner;
		public float TimeToRegister;
		public bool Registered;
		public float TimeToForget;
		public bool Handled;
		public float TimeHeard;

	}

	public BaseNpc Owner { get; set; }
	[Property] public bool DrawDebug { get; set; } = false;
	[Property, ReadOnly] public TargetData PrimaryTarget { get; set; }
	[Property, ReadOnly] public Dictionary<Guid, TargetData> KnownTargets { get; set; } = new();
	[Property, ReadOnly] public Dictionary<Guid, NpcSoundMemory> KnownSounds { get; set; } = new();

	public Transform GetEyeTransform()
	{
		if ( !Owner.IsValid() )
			return Transform.World;
		
		return Owner.BodyModel.GetAttachment( Owner.NpcDef.ModelInfo.EyeAttachment ).GetValueOrDefault( new Transform( WorldPosition ) );
	}

	protected override void OnFixedUpdate()
	{
		// Moved sound and vision memory updating to function called by npc
//		figure out which target we should actually be focusing on
		base.OnFixedUpdate();
	}

	public void PerformSensing()
	{
		UpdateSoundMemory();
		UpdateVisionMemory();
	}



	public Guid? GetClosestSoundKey( float maxDistance = 9999f )
	{
		Guid? closestKey = null;
		float closestDistanceSqr = maxDistance * maxDistance;

		foreach ( var kvp in KnownSounds )
		{
			Vector3 soundPos = kvp.Value.Position;
			float distSqr = Vector3.DistanceBetweenSquared( Owner.WorldPosition, soundPos );

			if ( distSqr < closestDistanceSqr )
			{
				closestDistanceSqr = distSqr;
				closestKey = kvp.Key;
			}
		}

		return closestKey;
	}

	public Vector3? GetClosestSoundPosition( float maxDistance = 9999f )
	{
		var closestKey = GetClosestSoundKey( maxDistance );
		if ( closestKey.HasValue && KnownSounds.TryGetValue( closestKey.Value, out var memory ) )
		{
			return memory.Position;
		}

		return null;
	}


	protected void UpdateSoundMemory()
	{
		List<Guid> kill = new();

		foreach ( var sound in KnownSounds )
		{
			var snd = sound.Value;

			if ( !snd.Registered )
			{
				snd.TimeToRegister -= Time.Delta;
				if ( snd.TimeToRegister <= 0f )
					snd.Registered = true;

			}
			else if ( !NpcSoundManager.StaticRef.NpcSounds.ContainsKey( sound.Key ) || GetEyeTransform().Position.Distance( snd.Position ) > NpcSoundManager.StaticRef.NpcSounds[sound.Key].CurrentRadius )
			{
				snd.TimeToForget -= Time.Delta;

				if ( snd.TimeToForget <= 0f )
					kill.Add( sound.Key );
			}

			KnownSounds[sound.Key] = snd;
		}
		foreach ( var key in kill )
			KnownSounds.Remove( key );
	}

	protected void UpdateVisionMemory()
	{
//		get visible targets
		List<NpcRelations> visibleTargets = new();

		foreach ( var target in Scene.Components.GetAll<NpcRelations>() )
		{
			if ( (GetEyeTransform().Position + GetEyeTransform().Rotation.Forward * 300).Distance( target.WorldPosition ) < 300f )
			{
				var targetpos = target.WorldPosition.LerpTo( target.WorldPosition + Vector3.Up * 50f, new Random().Float() );
				var tr = Scene.Trace.Ray( GetEyeTransform().Position, targetpos ).IgnoreGameObjectHierarchy( GameObject ).IgnoreGameObjectHierarchy( target.GameObject ).Run();
				if ( tr.EndPosition == targetpos )
					visibleTargets.Add( target );
				if ( DrawDebug )
				{
					if ( tr.EndPosition == targetpos )
						Gizmo.Draw.Color = Color.Green;
					else
						Gizmo.Draw.Color = Color.Gray;
					Gizmo.Draw.Line( tr.StartPosition, tr.EndPosition );
				}
			}
		}
//		update untracked targets
		List<Guid> activeTargets = new();
		foreach ( var target in visibleTargets )
		{
			if ( !KnownTargets.ContainsKey( target.Id ) )
			{
				KnownTargets.Add( target.Id, new TargetData
				{
					Type = target.Type,
					Target = target
				} );
			}
			else
				activeTargets.Add( target.Id );
		}

		foreach ( var target in KnownTargets )
		{
			if ( activeTargets.Contains( target.Key ) )
			{
				KnownTargets[target.Key] = new TargetData
				{
					Type = target.Value.Type,
					Target = target.Value.Target,
					Tracking = true,
					LastKnownPosition = target.Value.Target.WorldPosition,
					LastKnownVelocity = target.Value.Target.Velocity,
					LostTime = 0f
				};
			}
			else
			{
				KnownTargets[target.Key] = new TargetData
				{
					Type = target.Value.Type,
					Target = target.Value.Target,
					Tracking = false,
					LastKnownPosition = target.Value.LastKnownPosition + Time.Delta * target.Value.LastKnownVelocity / MathF.Pow( Math.Max( target.Value.LostTime * 5f, 1f ), 1.5f ),
					LastKnownVelocity = target.Value.LastKnownVelocity,
					LostTime = target.Value.LostTime + Time.Delta
				};
			}
		}
	}

	protected override void OnUpdate()
	{
		if ( DrawDebug )
		{
//			SIGHT
			Gizmo.Draw.IgnoreDepth = false;
			Gizmo.Draw.Color = Color.Blue.WithAlpha( 0.1f );
			Gizmo.Transform = Gizmo.Transform.WithRotation( GetEyeTransform().Rotation );
			Gizmo.Draw.LineSphere( Gizmo.Transform.ToLocal( new Transform( GetEyeTransform().Position + GetEyeTransform().Rotation.Forward * 300 ) ).Position, 300, 16 );
			Gizmo.Transform = new global::Transform();
			Gizmo.Draw.Color = Color.Gray;

			foreach ( var target in KnownTargets )
			{
				if ( target.Value.Tracking )
					Gizmo.Draw.Color = Color.Red;
				else
					Gizmo.Draw.Color = Color.Gray;

				Gizmo.Draw.LineCapsule( new Capsule( target.Value.LastKnownPosition, target.Value.LastKnownPosition + Vector3.Up * 50f, 16f ) );
			}

//			SOUNDS, SMELL
			var soundroot = GetEyeTransform().Position - Vector3.Up * 5f;
			var returns = "";
			Gizmo.Draw.IgnoreDepth = true;

			foreach ( var sound in KnownSounds )
			{
				if ( sound.Value.Registered )
				{
					Gizmo.Draw.Color = Gizmo.Colors.Blue;

					if ( sound.Value.SoundType.ToString().StartsWith( "STENCH_" ) )
						Gizmo.Draw.Color = Gizmo.Colors.Green;
					else if ( sound.Value.SoundType.ToString().StartsWith( "ALERT_" ) )
						Gizmo.Draw.Color = Gizmo.Colors.Red;

					Gizmo.Draw.Color = Gizmo.Draw.Color.Desaturate( 1f - Math.Clamp( sound.Value.TimeToForget, 0f, 1f ) );
					Gizmo.Draw.Color = Gizmo.Draw.Color.Darken( 1f - Math.Clamp( sound.Value.TimeToForget + 0.5f, 0.5f, 1f ) );
					Gizmo.Draw.Text( returns + sound.Value.SoundType.ToString(), new Transform( soundroot ), flags: TextFlag.LeftTop );

					returns += "\n";
				}
			}
		}

		base.OnUpdate();
	}
}
