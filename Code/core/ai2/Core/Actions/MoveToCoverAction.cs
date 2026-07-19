namespace Core.AI;

public class MoveToCoverAction : MoveToAction
{
	protected List<HintNode> availableNodes = [];
	protected List<HintNode> potentialNodes = []; // nodes we should consider
	protected HintNode chosenCoverNode; // what we finally decide on

	public float idealMinDist = 256f;
	public float idealMaxDist = 1024f;

	public MoveToCoverAction( AIController owner )
	{
		RegisterActionDefinition( AIActionDefinition.ActionList.ActionTakeCover, owner );


	}

	public override bool CheckProceduralPrecondition( AIController agent )
	{
		if ( !agent.Blackboard.activeEnemy.IsValid() )
			return false;

		return agent.Blackboard.HasAnyCoverNode();
	}

	public override bool IsDone() => arrived;

	public override void OnEnter( AIController agent )
	{
		var nav = agent.Agent;
		if ( !nav.IsValid() )
		{
			arrived = true;
			return;
		}

		chosenCoverNode = agent.Blackboard.ClaimCoverNode( agent );
		if ( !chosenCoverNode.IsValid() )
		{
			arrived = true;
			return;
		}

		targetPosition = chosenCoverNode.WorldPosition;
		agent.DoMovement( targetPosition, AIController.GoalType.GOALTYPE_COVER );
		hasStarted = true;
	}

	public override void Perform( AIController agent )
	{
		var nav = agent.Agent;
		if ( !nav.IsValid() || !hasStarted )
		{
			arrived = true;
			return;
		}

		Vector3 agentPos = nav.AgentPosition;
		float dist = agentPos.Distance( targetPosition );

		if ( Time.Now >= _nextMoveUpdate )
		{
			agent.Agent.MoveTo( targetPosition );
			_nextMoveUpdate = Time.Now + 0.2f;
		}

		if ( dist <= stoppingDistance )
		{
			agent.Navigation.NavigationStopMovement();
			arrived = true;
		}
	}

	public override void OnExit( AIController agent )
	{

		agent.Navigation.NavigationStopMovement();
	}
}
