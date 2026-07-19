using System;

namespace Core.AI;

public struct DetectedScent
{
	public ScentCategory Category;
	public Vector3 Position;          // last known position of the source
	public float PerceivedIntensity;  // post-falloff, post-wind, post-habituation
	public float TimeLastSensed;
}



public class ScentSensor : BaseSensor<ScentPacket>
{
	public ScentSensor( AIController agent ) : base( agent ) { }

	// 0 = cant smell, 1 = human baseline, 2 = bullsquid
	public float smellStrength = 1f;

	public float baseDetectionThreshold = 0.05f;
	public float habituationRate = 0.15f;     // adaptation speed while exposed. how long until we go noseblind
	public float dishabituationRate = 0.5f;   // recovery speed once removed from the smell
	public float nostrilSeparation = 4f;       // for stereo gradient sampling
	public float memoryDuration = 20f;         // how long a lost scent's last position is remembered

	private readonly Dictionary<ScentEmitter, float> _habituation = new();
	private readonly Dictionary<ScentEmitter, DetectedScent> _memory = new();

	// there were a variety of sources for how this sort of stuff works. this is a really basic version i came up with the handle habituation
	private void CollectPacketData()
	{
		var agentPos = Agent.WorldPosition;
		var detected = new List<DetectedScent>();
		var stillTracked = new HashSet<ScentEmitter>();

		foreach ( var emitter in AIScentManager.Current.All )
		{
			float dist = Vector3.DistanceBetween( agentPos, emitter.Position );
			float effectiveRadius = emitter.Radius * smellStrength;

			if ( dist > effectiveRadius )
				continue;

			// falloff, squared so it's steep near the edge of range
			// and strong close to the source, like a real diffusing scent cloud. yuck.
			float falloff = 1f - (dist / effectiveRadius);
			falloff *= falloff;

			float raw = emitter.Intensity * falloff;

			// constant exposure dulls the perceived intensity, 
			// this is only to one source for now but would be cool if it dulled an entire category
			float adaptation = _habituation.TryGetValue( emitter, out var a ) ? a : 0f;
			float perceived = raw * (1f - adaptation) * smellStrength;

			if ( perceived < baseDetectionThreshold )
				continue;

			var entry = new DetectedScent
			{
				Category = emitter.Category,
				Position = emitter.Position,
				PerceivedIntensity = perceived,
				TimeLastSensed = WorldTime.Now
			};
			//Log.Info($"I smell a {entry.Category.ToString()}! adaptation: {adaptation}");
			//if ( adaptation >= 0.8f )
			//	Log.Info( "But im used to the scent now" );
			detected.Add( entry );
			_memory[emitter] = entry;
			stillTracked.Add( emitter );

			// higher smellStrength resists habituation (a bullsquid doesn't tune out the scent of prey as fast)
			float adaptRate = habituationRate / MathF.Max( smellStrength, 0.25f );
			_habituation[emitter] = MathX.Approach( adaptation, 1f, adaptRate * Time.Delta );
		}

		// recover sensitivity for anything no longer in range
		foreach ( var key in new List<ScentEmitter>( _habituation.Keys ) )
		{
			if ( stillTracked.Contains( key ) ) continue;
			_habituation[key] = MathX.Approach( _habituation[key], 0f, dishabituationRate * Time.Delta );
			if ( _habituation[key] <= 0f ) _habituation.Remove( key );
		}

		// prune stale memories, and fold fresh but not detected
		// memories back in as low confidence "I think it went this way" data —
		// this is what lets the AI follow a trail after losing direct scent contact.
		foreach ( var key in new List<ScentEmitter>( _memory.Keys ) )
		{
			var mem = _memory[key];
			if ( WorldTime.Now - mem.TimeLastSensed > memoryDuration )
			{
				_memory.Remove( key );
				continue;
			}
			if ( !stillTracked.Contains( key ) )
				detected.Add( mem );
		}

		// sample two virtual nostrils left and right of forward facing
		// steer toward whichever side smells stronger, rather than teleporting
		// awareness straight to the source position.
		Vector3 gradientDir = Vector3.Zero;
		if ( detected.Count > 0 )
		{
			var strongest = detected[0];
			foreach ( var d in detected )
				if ( d.PerceivedIntensity > strongest.PerceivedIntensity )
					strongest = d;

			var left = agentPos - Agent.WorldRotation.Right * (nostrilSeparation * 0.5f);
			var right = agentPos + Agent.WorldRotation.Right * (nostrilSeparation * 0.5f);

			float distL = Vector3.DistanceBetween( left, strongest.Position );
			float distR = Vector3.DistanceBetween( right, strongest.Position );

			gradientDir = (distL < distR ? Agent.WorldRotation.Left : Agent.WorldRotation.Right)
						  + Agent.WorldRotation.Forward;
			gradientDir = gradientDir.Normal;
		}

		Packet.Owner = Agent;
		Packet.DetectedScents = detected;
		Packet.StrongestDirection = gradientDir;
		Packet.AnyDetected = detected.Count > 0;

	}

	public ScentPacket GetOutputPacketData() => Packet;

	public override void UpdatePacket()
	{
		CollectPacketData();
	}
}
