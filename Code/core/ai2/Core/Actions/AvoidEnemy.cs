namespace Core.AI;

public sealed class HoundeyeBackOffAction : AIAction
{
	private bool _done;
	private Vector3 _retreatTarget;

	private const float _minBackOffDist = 200f;
	private const float _maxBackOffDist = 350f;
	private const float _reachedThreshold = 200f;

	public HoundeyeBackOffAction( AIController owner )
	{
		Owner = owner;
		Cost = 1f;
		RegisterActionDefinition( AIActionDefinition.ActionList.ActionBackAwayFromEnemy, owner );

	}

	public override void OnEnter( AIController agent )
	{
		_done = false;

		var enemy = agent.Blackboard.activeEnemy;
		if ( !enemy.IsValid() )
		{
			_done = true;
			return;
		}

		Vector3 awayDir = (agent.WorldPosition - enemy.WorldPosition).Normal;

		float backOffDist = Game.Random.Float( _minBackOffDist, _maxBackOffDist );
		Vector3 desiredPos = agent.WorldPosition + awayDir * backOffDist;
		var point = agent.Scene.NavMesh.GetClosestPoint( desiredPos );

		if ( agent.Agent.IsValid() &&
			point is not null )
		{
			_retreatTarget = point.Value;
			agent.DoMovement( point, AIController.GoalType.GOALTYPE_LOCATION );
		}
		else
		{
			_retreatTarget = desiredPos; // fuck it man
		}

	}

	public override void Perform( AIController agent )
	{
		if ( _done )
			return;

		if ( !agent.Blackboard.activeEnemy.IsValid() )
		{
			Finish();
			return;
		}

		float dist = agent.WorldPosition.Distance( _retreatTarget );
		if ( dist <= _reachedThreshold )
		{
			Finish();
			return;
		}
	}

	public override bool IsDone() => _done;

	public override bool CheckProceduralPrecondition( AIController agent )
	{
		var enemy = agent.Blackboard.activeEnemy;
		if ( !enemy.IsValid() )
			return false;

		float dist = (enemy.WorldPosition - agent.WorldPosition).Length;

		// Only back off if enemy is actually too close
		return dist <= _minBackOffDist;
	}

	private void Finish()
	{
		_done = true;
		Owner.Navigation.NavigationStopMovement();
	}
}
