namespace Core.AI;

public class InvestigateSound : MoveToAction
{
	private Vector3? _closestSoundPosition;

	public InvestigateSound( AIController owner ) => RegisterActionDefinition( AIActionDefinition.ActionList.InvestigateSoundAction, owner );

	public override void OnEnter( AIController agent )
	{
		_closestSoundPosition = agent.LastSoundHeardPosition;

		if ( !_closestSoundPosition.HasValue )
		{
			_arrived = true;
			return;
		}
		agent.FaceTarget( agent.LastSoundHeardPosition, 8 );

		_targetPosition = agent.Scene.NavMesh.GetRandomPoint( _closestSoundPosition.Value, 256 ).Value;
		NextMoveUpdate = 0f;
		_hasStarted = true;
		agent.Agent.MoveTo( _targetPosition );
	}

	public override void Perform( AIController agent )
	{
		// always adjust the target position incase sound position is to change mid-action (new soud heard)
		if ( _closestSoundPosition.HasValue )
			_targetPosition = _closestSoundPosition.Value;

		if ( WorldTime.Now >= NextMoveUpdate )
		{
			agent.Agent.MoveTo( _targetPosition );
			NextMoveUpdate = WorldTime.Now + 0.2f;
		}

		float dist = (agent.WorldPosition - _targetPosition).Length;
		if ( dist <= _stoppingDistance )
			_arrived = true;
	}

	public override bool IsDone()
	{
		if ( !_hasStarted ) return false;
		return _arrived || Owner.WorldState.Get( AIFacts.EnemyVisible );
	}

	public override bool IsFailed()
	{
		return Owner.WorldState.Get( AIFacts.EnemyVisible ) &&
			   Owner.WorldState.Get( AIFacts.HasEnemy );
	}

	public override bool CheckProceduralPrecondition( AIController agent )
	{
		return !agent.WorldState.Get( AIFacts.EnemyVisible );
	}

	public override void OnExit( AIController agent )
	{
		agent.Agent.Stop();
		agent.Navigation.NavigationStopMovement();
	}
}
