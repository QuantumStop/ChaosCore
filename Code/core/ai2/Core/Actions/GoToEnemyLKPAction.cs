using Core;
using Core.AI;

public class GoToEnemyLKPAction : MoveToAction
{
	public GoToEnemyLKPAction( AIController owner )
	{
		RegisterActionDefinition( AIActionDefinition.ActionList.ActionGoToEnemyLKP, owner );

	}

	public override void OnEnter( AIController agent )
	{
		if ( !agent.enemyLKP.HasValue )
		{
			arrived = true;
			return;
		}

		targetPosition = agent.enemyLKP.Value;
		_nextMoveUpdate = 0f;
		hasStarted = true;
		agent.Agent.MoveTo( targetPosition );
	}

	public override void Perform( AIController agent )
	{
		// always adjust the target position incase lkp is to change mid-action
		if ( agent.enemyLKP.HasValue )
			targetPosition = agent.enemyLKP.Value;

		if ( Time.Now >= _nextMoveUpdate )
		{
			agent.Agent.MoveTo( targetPosition );
			_nextMoveUpdate = Time.Now + 0.2f;
		}

		float dist = (agent.WorldPosition - targetPosition).Length;
		if ( dist <= stoppingDistance )
			arrived = true;
	}

	public override bool IsDone()
	{
		if ( !hasStarted ) return false;
		return arrived || Owner.WorldState.Get( "enemyVisible" );
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
