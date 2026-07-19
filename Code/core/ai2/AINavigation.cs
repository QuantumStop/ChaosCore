using Sandbox.Navigation;
using static Core.AI.AIController;

namespace Core.AI;

public struct NavData()
{
	public AIController Controller;
	public Vector3 Position;
	public GoalType GoalType;
}

public class AINavigation : AIModule
{

	public NavData? Data;
	public MoveType ControllerMoveType;
	public NavMeshPath Path;
	public TimeSince TimeSinceMovementStart;


	public override void Init( AIController owner )
	{
		Owner = owner;
		ControllerMoveType = owner.moveType;
	}

	public void NavigationDoMovement( NavData data )
	{
		Data = data; // Assign nav data here, held onto until we reach are position or recieve a new call to movement.
		Owner.IsMoving = true;

		TimeSinceMovementStart = 0f;

		switch ( ControllerMoveType )
		{
			case MoveType.MOVE_NONE:
				return;
			case MoveType.MOVE_GROUND:
				Owner.Agent.MoveTo( data.Position );
				Path = Owner.Agent.GetPath();
				//	AddDataToNavigationQueue();
				return;
		}

	}

	public void NavigationCheckMovement( bool cineMovement = false )
	{
		if ( Data is null )
		{
			NavigationCheckForErrors();
			return;
		}


		var distToGoal = Data.Value.Position.Distance( Owner.WorldPosition );
		if ( distToGoal <= Owner.Definition.AgentRadius && Data.Value.GoalType != GoalType.GOALTYPE_PATHCORNER_CONTINUOUS )  //  
		{
			if ( cineMovement )
				Owner.HasReachedCine = true;
			if ( Owner.FollowingForcedMovementPath )
			{
				Owner.ForcedMoveComplete = true;
				Owner.ClearForcedMovePosition();
			}

			NavigationStopMovement();
		}

	}

	/// <summary>
	/// Adds the current NavData parameters position value to the queue
	/// </summary>
	public void AddDataToNavigationQueue()
	{
		Owner.AIManager.RequestMove( Data.Value );
	}

	public void NavigationStopMovement()
	{
		Owner.NavStatus = AIController.NavigationStatus.NAVIGATION_COMPLETED;

		Owner.CurrentMoveGoal = GoalType.GOALTYPE_NONE;
		Owner.IsMoving = false;
		Owner.Agent.Stop();

	}

	private const int _velocitySampleCount = 20;
	private const float _stuckVelocityThreshold = 1f;
	private const float _stuckAverageThreshold = 0.1f;
	private const float _sampleInterval = 0.1f;

	private readonly Queue<float> _velocitySamples = new( _velocitySampleCount );
	private float _sampleTimer;

	public void NavigationCheckForErrors()
	{
		if ( !Owner.Agent.IsNavigating )
		{
			_velocitySamples.Clear();
			_sampleTimer = 0f;
			return;
		}

		if ( Owner.Agent.Velocity.Length > _stuckVelocityThreshold )
		{
			_velocitySamples.Clear();
			return;
		}

		_sampleTimer += Time.Delta;
		if ( _sampleTimer < _sampleInterval ) return;
		_sampleTimer = 0f;

		// drop oldest when full
		if ( _velocitySamples.Count >= _velocitySampleCount )
			_velocitySamples.Dequeue();

		_velocitySamples.Enqueue( Owner.Agent.Velocity.Length );

		// Only evaluate when we have a full sample window
		if ( _velocitySamples.Count < _velocitySampleCount ) return;

		float avg = 0f;
		foreach ( var v in _velocitySamples ) avg += v;
		avg /= _velocitySampleCount;

		if ( avg <= _stuckAverageThreshold )
		{
			Log.Warning( $"[AINavigation] {Owner.GameObject.Name} is stuck!!! stopping navigation." );
			_velocitySamples.Clear();
			NavigationStopMovement();
		}
	}
}
