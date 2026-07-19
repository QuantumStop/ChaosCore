using Core.AI;

public class InvestigateSound : MoveToAction
{
	Vector3? ClosestSoundPosition;

	public InvestigateSound( AIController owner )
	{
		RegisterActionDefinition( AIActionDefinition.ActionList.InvestigateSoundAction, owner );

	}

	public override void OnEnter( AIController agent )
	{
		ClosestSoundPosition = agent.LastSoundHeardPosition;

		if ( !ClosestSoundPosition.HasValue )
		{
			arrived = true;
			return;
		}
		agent.FaceTarget( agent.LastSoundHeardPosition, 8 );

		targetPosition = agent.Scene.NavMesh.GetRandomPoint( ClosestSoundPosition.Value, 256 ).Value;
		_nextMoveUpdate = 0f;
		hasStarted = true;
		agent.Agent.MoveTo( targetPosition );
	}

	public override void Perform( AIController agent )
	{
		// always adjust the target position incase sound position is to change mid-action (new soud heard)
		if ( ClosestSoundPosition.HasValue )
			targetPosition = ClosestSoundPosition.Value;

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
		return arrived || Owner.WorldState.Get( AIFacts.EnemyVisible );
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
