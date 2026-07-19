namespace Core.AI;

public class GoToEnemyLKPAction : MoveToAction
{
	public GoToEnemyLKPAction( AIController owner )
	{
		RegisterActionDefinition( AIActionDefinition.ActionList.ActionGoToEnemyLKP, owner );

	}

	public override void OnEnter( AIController agent )
	{
		if ( !agent.EnemyLKP.HasValue )
		{
			_arrived = true;
			return;
		}

		_targetPosition = agent.EnemyLKP.Value;
		NextMoveUpdate = 0f;
		_hasStarted = true;
		agent.Agent.MoveTo( _targetPosition );
	}

	public override void Perform( AIController agent )
	{
		// always adjust the target position incase lkp is to change mid-action
		if ( agent.EnemyLKP.HasValue )
			_targetPosition = agent.EnemyLKP.Value;

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
		return _arrived || Owner.WorldState.Get( "enemyVisible" );
	}

	public override bool IsFailed()
	{
		return !Owner.WorldState.Get( "hasEnemyLKP" ) &&
			   !Owner.WorldState.Get( "searchingForEnemy" );
	}

	public override bool CheckProceduralPrecondition( AIController agent )
	{
		return agent.WorldState.Get( "hasEnemyLKP" ) &&
			   agent.WorldState.Get( "searchingForEnemy" );
	}

	public override void OnExit( AIController agent )
	{
		agent.Agent.Stop();
		agent.Navigation.NavigationStopMovement();
	}
}
