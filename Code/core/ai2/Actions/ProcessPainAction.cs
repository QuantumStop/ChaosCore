namespace Core.AI;

public class ProcessPainAction : AIAction
{
	private enum PainType { None = -1, Low = 0, Medium = 1, High = 2 }
	private PainType _painType = PainType.None;

	private float _nextMoveUpdate = 0f;
	private Vector3 _targetPosition;
	private bool _done = false;
	private float _lowPainTimer; // low pain just pauses briefly

	public ProcessPainAction( AIController owner ) => RegisterActionDefinition( AIActionDefinition.ActionList.ProcessPainAction, owner );

	public override void OnEnter( AIController agent )
	{
		_done = false;

		if ( agent.WorldState.Get( "highPain" ) ) _painType = PainType.High;
		else if ( agent.WorldState.Get( "mediumPain" ) ) _painType = PainType.Medium;
		else if ( agent.WorldState.Get( "lowPain" ) ) _painType = PainType.Low;
		else
		{
			_done = true;
			return;
		}

		agent.Navigation.NavigationStopMovement();

		switch ( _painType )
		{
			case PainType.Low:
				{
					// fall off our current path/origin a bit
					Vector3 right = agent.WorldRotation.Right;
					float side = Game.Random.Float( -1f, 1f ) > 0 ? 1f : -1f;
					Vector3 sideStep = agent.WorldPosition + right * side * Game.Random.Float( 30f, 70f );

					var snapped = agent.Scene.NavMesh.GetClosestPoint( sideStep );
					_targetPosition = snapped ?? agent.WorldPosition;
					_lowPainTimer = agent.PainSensor.PainTime; // brief stagger then done. ill add the rest later

					agent.Agent.MoveTo( _targetPosition );
					break;
				}

			case PainType.Medium:
				{
					// Run away from where we got hit
					Vector3 awayDir = -agent.LastAttackDir.WithZ( 0 ).Normal;
					Vector3 fleePos = agent.WorldPosition + awayDir * Game.Random.Float( 150f, 300f );

					var snapped = agent.Scene.NavMesh.GetClosestPoint( fleePos );
					_targetPosition = snapped ?? agent.WorldPosition;

					agent.Agent.MoveTo( _targetPosition );
					break;
				}

			case PainType.High:
				{
					// Run to nearest cover node!
					HintNode coverNode = null;
					float bestDist = float.MaxValue;

					// linqless conversion mightve been fucked, sorry!
					foreach ( var n in agent.Blackboard.nodePool )
					{
						if ( !n.IsValid() )
							continue;

						var dist = n.WorldPosition.Distance( agent.WorldPosition );

						if ( dist < float.MaxValue )
						{
							bestDist = dist;
							coverNode = n;
						}
					}

					if ( coverNode.IsValid() )
					{
						_targetPosition = coverNode.WorldPosition;
						agent.Agent.MoveTo( _targetPosition );
					}
					else
					{
						Vector3 threatPos = agent.WorldPosition + agent.LastAttackDir * 500f;
						// ideally agent.LastKnownThreatPosition if you track it

						var candidates = GetCoverCandidates( agent );
						float bestScore = -1f;
						Vector3 bestPos = agent.WorldPosition;

						foreach ( var c in candidates )
						{
							float s = ScoreCandidate( c, agent, threatPos );
							if ( s > bestScore )
							{
								bestScore = s;
								bestPos = c;
							}
						}

						agent.Agent.MoveTo( _targetPosition );
						break;
					}
					break;
				}
		}
	}

	public override void Perform( AIController agent )
	{
		if ( _done ) return;

		switch ( _painType )
		{
			case PainType.Low:
				{
					// Refresh move
					if ( WorldTime.Now >= _nextMoveUpdate )
					{
						agent.Agent.MoveTo( _targetPosition );
						_nextMoveUpdate = WorldTime.Now + 0.2f;
					}

					// Done after brief stagger time or arrival
					if ( WorldTime.Now >= _lowPainTimer ||
						 agent.WorldPosition.Distance( _targetPosition ) <= 25f )
						_done = true;

					break;
				}

			case PainType.Medium:
			case PainType.High:
				{
					if ( WorldTime.Now >= _nextMoveUpdate )
					{
						agent.Agent.MoveTo( _targetPosition );
						_nextMoveUpdate = WorldTime.Now + 0.2f;
					}

					if ( agent.WorldPosition.Distance( _targetPosition ) <= 35f )
						_done = true;

					break;
				}
		}
	}

	List<Vector3> GetCoverCandidates( AIController agent, int rings = 2 )
	{
		var candidates = new List<Vector3>();
		int[] counts = { 8, 16 }; // probes per ring
		float[] radii = { 150f, 300f };

		for ( int r = 0; r < rings; r++ )
		{
			for ( int i = 0; i < counts[r]; i++ )
			{
				float angle = 360f / counts[r] * i;
				Rotation rot = Rotation.FromYaw( angle );
				Vector3 dir = rot.Forward;
				Vector3 probe = agent.WorldPosition + dir * radii[r];

				var snapped = agent.Scene.NavMesh.GetClosestPoint( probe );
				if ( snapped.HasValue )
					candidates.Add( snapped.Value );
			}
		}
		return candidates;
	}

	bool IsCoveredFromThreat( Vector3 candidatePos, Vector3 threatPos, Scene scene )
	{
		// Offset upward to approximate head/chest height
		Vector3 coverHead = candidatePos + Vector3.Up * 60f;
		Vector3 threatEyes = threatPos + Vector3.Up * 60f;

		var tr = scene.Trace
			.Ray( coverHead, threatEyes )
			.WithoutTags( "npc" ) // ignore other NPCs
			.Run();

		return tr.Hit; // geometry in the way = cover
	}

	float ScoreCandidate( Vector3 candidate, AIController agent, Vector3 threatPos )
	{
		float score = 0f;

		// Hard requirement — must actually occlude
		if ( !IsCoveredFromThreat( candidate, threatPos, agent.Scene ) )
			return -1f;

		// Prefer closer candidates (faster to reach)
		float distToAgent = candidate.Distance( agent.WorldPosition );
		score += MathX.Remap( distToAgent, 0, 400f, 40f, 0f );

		// Prefer farther from threat
		float distToThreat = candidate.Distance( threatPos );
		score += MathX.Remap( distToThreat, 0, 600f, 0f, 30f );

		// Prefer candidates that aren't directly behind the agent
		// (avoid running past the threat to reach cover)
		Vector3 toCandidate = (candidate - agent.WorldPosition).Normal;
		Vector3 toThreat = (threatPos - agent.WorldPosition).Normal;
		float dot = Vector3.Dot( toCandidate, toThreat );
		score += MathX.Remap( dot, -1f, 1f, 20f, 0f ); // reward moving away from threat

		return score;
	}

	public override void OnExit( AIController agent )
	{
		// Clear pain facts so they don't re-trigger immediately
		agent.WorldState.Set( "lowPain", false );
		agent.WorldState.Set( "mediumPain", false );
		agent.WorldState.Set( "highPain", false );

		agent.Agent.Stop();
	}

	public override bool IsDone() => _done;
	public override bool IsFailed() => false;

	public override bool CheckProceduralPrecondition( AIController agent )
	{
		return agent.WorldState.Get( "lowPain" ) ||
			   agent.WorldState.Get( "mediumPain" ) ||
			   agent.WorldState.Get( "highPain" );
	}
}
