namespace Core.AI;

public class SniffOutScentAction : AIAction
{
	public SniffOutScentAction( AIController owner ) => RegisterActionDefinition( AIActionDefinition.ActionList.SniffOutScent, owner );

	private enum SniffState { Sniffing, Searching, Approaching }

	public bool FoundScentSource = false;

	private SniffState _state;
	private Vector3 _targetPosition;
	private float _stoppingDistance = 12f;
	private float _arrivalThreshold = 64f;       // distance to source counted as found it
	private float _strongScentThreshold = 0.9f;  // perceived intensity counted for being right on top of it

	private float _stateTimer;
	private const float _sniffDuration = 0.6f;
	private float _nextMoveUpdate;

	private DetectedScent? _trackedScent;
	private float _lastLegIntensity;
	private float _bestSampleIntensity;
	private Vector3 _bestSampleDirection;
	private int _samplesTaken;
	private const int _samplesPerSearch = 3;
	private const float _searchSpreadDegrees = 70f;

	public override void OnEnter( AIController agent )
	{
		FoundScentSource = false;
		_trackedScent = GetStrongestScent( agent );
		if ( !_trackedScent.HasValue )
			return; // nothing to track yet,proc precondition will sort this out

		_lastLegIntensity = _trackedScent.Value.PerceivedIntensity;
		EnterSniffing( agent );
	}

	public override void Perform( AIController agent )
	{
		// keep refreshing in case the scent shifts or strengthens mid-action
		var current = GetStrongestScent( agent );
		if ( current.HasValue )
			_trackedScent = current;

		switch ( _state )
		{
			case SniffState.Sniffing: TickSniffing( agent ); break;
			case SniffState.Searching: TickSearching( agent ); break;
			case SniffState.Approaching: TickApproaching( agent ); break;
		}
	}

	/// <summary>
	/// stop, sniff air, take a moment to register it
	/// </summary>
	/// <param name="agent"></param>
	private void EnterSniffing( AIController agent )
	{
		_state = SniffState.Sniffing;
		_stateTimer = WorldTime.Now + _sniffDuration;
		agent.Agent.Stop();


		agent.BodyModel.Set( "b_SniffAir", true );

		if ( _trackedScent.HasValue )
			agent.FaceTarget( _trackedScent.Value.Position, 8 );

	}

	private void TickSniffing( AIController agent )
	{
		if ( WorldTime.Now < _stateTimer ) return;
		if ( !_trackedScent.HasValue ) return; // trail went cold!

		if ( _trackedScent.Value.PerceivedIntensity >= _strongScentThreshold )
		{
			FoundScentSource = true;
			return;
		}


		EnterSearching( agent );
	}

	private void EnterSearching( AIController agent )
	{
		_state = SniffState.Searching;
		_samplesTaken = 0;
		_stateTimer = WorldTime.Now;
	}

	/// <summary>
	/// Look through several samples ant store them
	/// </summary>
	/// <param name="agent"></param>
	private void TickSearching( AIController agent )
	{
		if ( WorldTime.Now < _stateTimer ) return;

		float jitter = Game.Random.Float( -20f, 20f );
		Vector3 sampleDir = agent.WorldRotation.Forward.RotateAround( Vector3.Zero, Rotation.FromYaw( jitter ) );
		agent.FaceTarget( agent.WorldPosition + sampleDir * 128f, 10 );

		_samplesTaken++;
		_stateTimer = WorldTime.Now + 0.35f;

		if ( _samplesTaken >= _samplesPerSearch )
			EnterApproaching( agent );
	}

	/// <summary>
	/// Grab the direction from the scent packet, get the strongest direction, then move towards that direction.
	/// TODO: do not call agent moveto, this is a hack for now because im justtesting this 
	/// </summary>
	/// <param name="agent"></param>
	private void EnterApproaching( AIController agent )
	{
		_state = SniffState.Approaching;

		var packet = agent.ScentSensor.GetOutputPacketData();
		Vector3 dir = packet.AnyDetected ? packet.StrongestDirection : agent.WorldRotation.Forward;

		float stepDistance = MathX.Remap( _bestSampleIntensity, 0f, 1f, 320f, 96f, clamp: true );
		Vector3 roughTarget = agent.WorldPosition + dir * stepDistance;

		var navPoint = agent.Scene.NavMesh.GetRandomPoint( roughTarget, 64 );
		_targetPosition = navPoint ?? roughTarget;

		_nextMoveUpdate = 0f;
		agent.Agent.MoveTo( _targetPosition );
	}

	/// <summary>
	/// Try to follow the scent from the strongest direction and see if we actually have gotten closer
	/// </summary>
	/// <param name="agent"></param>
	private void TickApproaching( AIController agent )
	{
		if ( WorldTime.Now >= _nextMoveUpdate )
		{
			agent.Agent.MoveTo( _targetPosition );
			_nextMoveUpdate = WorldTime.Now + 0.2f;
		}

		float dist = (agent.WorldPosition - _targetPosition).Length;
		if ( dist > _stoppingDistance ) return;

		float intensityNow = _trackedScent?.PerceivedIntensity ?? 0f;
		bool nearSource = _trackedScent.HasValue &&
			(agent.WorldPosition - _trackedScent.Value.Position).Length <= _arrivalThreshold;

		if ( intensityNow >= _strongScentThreshold || nearSource )
		{
			FoundScentSource = true;
			return;
		}

		// repeat
		_lastLegIntensity = intensityNow;
		EnterSniffing( agent );
	}

	private DetectedScent? GetStrongestScent( AIController agent )
	{
		var packet = agent.ScentSensor?.GetOutputPacketData();
		if ( !packet.Value.AnyDetected || packet.Value.DetectedScents.Count == 0 )
			return null;

		var best = packet.Value.DetectedScents[0];
		foreach ( var d in packet.Value.DetectedScents )
			if ( d.PerceivedIntensity > best.PerceivedIntensity )
				best = d;
		return best;
	}

	public override bool IsDone()
	{
		return FoundScentSource;
	}

	public override bool IsFailed()
	{
		return !_trackedScent.HasValue;
	}

	public override bool CheckProceduralPrecondition( AIController agent )
	{

		return !Owner.WorldState.Get( AIFacts.HasEnemy );
	}

	public override void OnExit( AIController agent )
	{
		agent.Agent.Stop();
		agent.Navigation.NavigationStopMovement();
	}
}
