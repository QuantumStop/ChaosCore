namespace Core.AI;

public class MoveToEnemyAction : MoveToAction
{
	public override bool StopAtDestination => false;

	public MoveToEnemyAction( AIController owner )
	{
		RegisterActionDefinition( AIActionDefinition.ActionList.ActionChaseEnemy, owner );

	}

	public override void OnEnter( AIController agent )
	{
		_arrived = false;
		_hasStarted = false;
		NextMoveUpdate = 0f;

		if ( !agent.Blackboard.activeEnemy.IsValid() )
		{
			_arrived = true;
			return;
		}

		_targetPosition = agent.EnemyLKP.Value;
		base.OnEnter( agent );
	}


	public override void Perform( AIController agent )
	{
		if ( agent.Blackboard.activeEnemy.IsValid() )
			_targetPosition = agent.EnemyLKP.Value;

		// Keep the navigation goal in sync with the moving target.
		if ( WorldTime.Now >= NextMoveUpdate )
		{
			agent.DoMovement( _targetPosition, AIController.GoalType.GOALTYPE_LOCATION );
			NextMoveUpdate = WorldTime.Now + 0.2f;
		}

		// Check arrival manually
		float dist = (agent.WorldPosition - _targetPosition).Length;
		if ( dist <= _stoppingDistance )
			_arrived = true;
	}

	public override bool IsDone()
	{
		bool done = _hasStarted && Owner.WorldState.Get( "enemyInRangeAttack1" );

		return done;
	}

	public override bool IsFailed()
	{
		bool failed = !Owner.Blackboard.activeEnemy.IsValid() ||
					!Owner.WorldState.Get( AIFacts.EnemyVisible ) ||
					Owner.WorldState.Get( AIFacts.LowPain ) ||
					Owner.WorldState.Get( AIFacts.MediumPain ) ||
					Owner.WorldState.Get( AIFacts.HighPain );

		return failed;
	}

	public override bool CheckProceduralPrecondition( AIController agent )
	{
		var enemy = agent.Blackboard.activeEnemy;

		if ( !enemy.IsValid() )
			return false;
		return true;
	}
}
