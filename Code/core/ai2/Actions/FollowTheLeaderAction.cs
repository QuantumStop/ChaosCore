namespace Core.AI;

public class FollowTheLeaderAction : MoveToAction
{
	public bool IsLeader;
	public FollowTheLeaderAction( AIController owner ) => RegisterActionDefinition( AIActionDefinition.ActionList.ActionFollowTheLeader, owner );

	public override void Perform( AIController agent )
	{
		var leader = agent.AISquad?.Leader;

		if ( !leader.IsValid() )
			return;

		_targetPosition = leader == agent ? agent.Scene.NavMesh.GetRandomPoint( agent.WorldPosition, 1024 ).Value : leader.WorldPosition;

		base.Perform( agent );
	}


	public override bool IsDone()
	{
		if ( !_hasStarted )
			return false;

		var leader = Owner.AISquad?.Leader;
		if ( !leader.IsValid() )
			return true;

		float dist = Owner.WorldPosition.Distance( leader.WorldPosition );
		return dist <= _stoppingDistance;
	}


	public override bool CheckProceduralPrecondition( AIController agent )
	{
		var squad = agent.AISquad;
		if ( squad is null )
			return false;

		var leader = squad.Leader;
		if ( !leader.IsValid() )
			return false;

		return true;
	}
}
