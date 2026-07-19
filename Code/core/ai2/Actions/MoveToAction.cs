namespace Core.AI;
/// <summary>
/// General action for which to base movement actions.
/// </summary>
public abstract class MoveToAction : AIAction
{
	protected Vector3 _targetPosition;
	protected float _stoppingDistance => Owner.Agent.Radius;
	protected bool _arrived = false;
	protected bool _hasStarted = false;
	public float NextMoveUpdate = 0f;

	public override bool IsDone() => _arrived;

	public override void OnEnter( AIController agent )
	{
		var nav = agent.Agent;

		if ( nav.IsValid() )
		{
			agent.DoMovement( _targetPosition, AIController.GoalType.GOALTYPE_LOCATION );
			_hasStarted = true;
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
		agent.IsMoving = true;
		// targetPosition = enemy.WorldPosition;
		// agent.DoMovement( targetPosition, AIController.GoalType.GOALTYPE_LOCATION );

		if ( _targetPosition.Distance( agent.WorldPosition ) <= _stoppingDistance )
		{
			OnExit( agent );
		}
	}


	public override void OnExit( AIController agent )
	{
		agent.IsMoving = false;
		_arrived = true;
		agent.Navigation.NavigationStopMovement();
	}
}
