namespace Core.AI;

public class MoveToEnemyAction : MoveToAction
{
	public MoveToEnemyAction( AIController owner )
	{
		RegisterActionDefinition( AIActionDefinition.ActionList.ActionChaseEnemy, owner );

	}

	public override void OnEnter( AIController agent )
	{

		if ( !agent.Blackboard.activeEnemy.IsValid() )
		{
			arrived = true;
			return;
		}

		targetPosition = agent.enemyLKP.Value;
		base.OnEnter( agent );

	}


	public override void Perform( AIController agent )
	{
		if ( agent.Blackboard.activeEnemy.IsValid() )
			targetPosition = agent.enemyLKP.Value;

		// Directly drive the navmesh agent, bypass the request stuff for right now. slow it down in the action and pray
		if ( Time.Now >= _nextMoveUpdate )
		{
			agent.Agent.MoveTo( targetPosition );
			_nextMoveUpdate = Time.Now + 0.2f;
		}

		// Check arrival manually
		float dist = (agent.WorldPosition - targetPosition).Length;
		if ( dist <= stoppingDistance )
			arrived = true;
	}

	public override bool IsDone()
	{
		bool done = hasStarted && Owner.WorldState.Get( "enemyInRangeAttack1" );

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
