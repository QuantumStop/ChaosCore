namespace Core.AI;

public class MoveToCoverAction : MoveToAction
{
	protected List<HintNode> _availableNodes = [];
	protected List<HintNode> _potentialNodes = []; // nodes we should consider
	protected HintNode _chosenCoverNode; // what we finally decide on

	public float IdealMinDist = 256f;
	public float IdealMaxDist = 1024f;

	public MoveToCoverAction( AIController owner ) => RegisterActionDefinition( AIActionDefinition.ActionList.ActionTakeCover, owner );

	public override bool CheckProceduralPrecondition( AIController agent )
	{
		if ( !agent.Blackboard.activeEnemy.IsValid() )
			return false;

		return agent.Blackboard.HasAnyCoverNode();
	}

	public override bool IsDone() => _arrived;

	public override void OnEnter( AIController agent )
	{
		var nav = agent.Agent;
		if ( !nav.IsValid() )
		{
			_arrived = true;
			return;
		}

		_chosenCoverNode = agent.Blackboard.ClaimCoverNode( agent );
		if ( !_chosenCoverNode.IsValid() )
		{
			_arrived = true;
			return;
		}

		_targetPosition = _chosenCoverNode.WorldPosition;
		agent.DoMovement( _targetPosition, AIController.GoalType.GOALTYPE_COVER );
		_hasStarted = true;
	}

	public override void Perform( AIController agent )
	{
		var nav = agent.Agent;
		if ( !nav.IsValid() || !_hasStarted )
		{
			_arrived = true;
			return;
		}

		Vector3 agentPos = nav.AgentPosition;
		float dist = agentPos.Distance( _targetPosition );

		if ( WorldTime.Now >= NextMoveUpdate )
		{
			agent.Agent.MoveTo( _targetPosition );
			NextMoveUpdate = WorldTime.Now + 0.2f;
		}

		if ( dist <= _stoppingDistance )
		{
			agent.Navigation.NavigationStopMovement();
			_arrived = true;
		}
	}

	public override void OnExit( AIController agent )
	{

		agent.Navigation.NavigationStopMovement();
	}
}
