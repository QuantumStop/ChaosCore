namespace Core.AI;
/// <summary>
/// General action for which to base movement actions.
/// </summary>
public abstract class MoveToAction : AIAction
{
	protected Vector3 targetPosition;
	protected float stoppingDistance => Owner.Agent.Radius;
	protected bool arrived = false;
	protected bool hasStarted = false;
	public float _nextMoveUpdate = 0f;

	public override bool IsDone() => arrived;

	public override void OnEnter( AIController agent )
	{
		var nav = agent.Agent;

		if ( nav.IsValid() )
		{
			agent.DoMovement( targetPosition, AIController.GoalType.GOALTYPE_LOCATION );
			hasStarted = true;
		}
		else
		{
			Log.Warning( "No Nav Agent for this movement!" );
		}
	}

	public override void Perform( AIController agent )
	{
		var nav = agent.Agent;
		if ( !nav.IsValid() ) return;
		agent.isMoving = true;
		// targetPosition = enemy.WorldPosition;
		// agent.DoMovement( targetPosition, AIController.GoalType.GOALTYPE_LOCATION );

		if ( targetPosition.Distance( agent.WorldPosition ) <= stoppingDistance )
		{
			OnExit( agent );
		}
	}


	public override void OnExit( AIController agent )
	{
		agent.isMoving = false;
		arrived = true;
		agent.Navigation.NavigationStopMovement();
	}
}
