using Sandbox.Navigation;
using static Core.AI.AIController;

namespace Core.AI;

public struct NavData()
{
	public AIController Controller;
	public Vector3 position;
	public GoalType goalType;
}

public class AINavigation : AIModule
{

	public NavData? Data;
	public MoveType ControllerMoveType;
	public NavMeshPath path;
	public TimeSince TimeSinceMovementStart;


	public override void Init( AIController owner )
	{
		Owner = owner;
		ControllerMoveType = owner.moveType;
	}

	public void NavigationDoMovement( NavData data )
	{
		Data = data; // Assign nav data here, held onto until we reach are position or recieve a new call to movement.
		Owner.isMoving = true;

		TimeSinceMovementStart = 0f;

		switch ( ControllerMoveType )
		{
			case MoveType.MOVE_NONE:
				return;
			case MoveType.MOVE_GROUND:
				Owner.Agent.MoveTo( data.position );
				path = Owner.Agent.GetPath();
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


		var distToGoal = Data.Value.position.Distance( Owner.WorldPosition );
		if ( distToGoal <= Owner.Definition.AgentRadius && Data.Value.goalType != GoalType.GOALTYPE_PATHCORNER_CONTINUOUS )  //  
		{
			if ( cineMovement )
				Owner.hasReachedCine = true;
			if ( Owner.followingForcedMovementPath )
			{
				Owner.forcedMoveComplete = true;
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
		Owner.aiManager.RequestMove( Data.Value );
	}

	public void NavigationStopMovement()
	{
		Owner.navStatus = AIController.NavigationStatus.NAVIGATION_COMPLETED;

		Owner.currentMoveGoal = GoalType.GOALTYPE_NONE;
		Owner.isMoving = false;
		Owner.Agent.Stop();

	}

	private const int VelocitySampleCount = 20;
	private const float StuckVelocityThreshold = 1f;
	private const float StuckAverageThreshold = 0.1f;
	private const float SampleInterval = 0.1f;

	private readonly Queue<float> _velocitySamples = new( VelocitySampleCount );
	private float _sampleTimer;

	public void NavigationCheckForErrors()
	{
		if ( !Owner.Agent.IsNavigating )
		{
			_velocitySamples.Clear();
			_sampleTimer = 0f;
			return;
		}

		if ( Owner.Agent.Velocity.Length > StuckVelocityThreshold )
		{
			_velocitySamples.Clear();
			return;
		}

		_sampleTimer += Time.Delta;
		if ( _sampleTimer < SampleInterval ) return;
		_sampleTimer = 0f;

		// drop oldest when full
		if ( _velocitySamples.Count >= VelocitySampleCount )
			_velocitySamples.Dequeue();

		_velocitySamples.Enqueue( Owner.Agent.Velocity.Length );

		// Only evaluate when we have a full sample window
		if ( _velocitySamples.Count < VelocitySampleCount ) return;

		float avg = 0f;
		foreach ( var v in _velocitySamples ) avg += v;
		avg /= VelocitySampleCount;

		if ( avg <= StuckAverageThreshold )
		{
			Log.Warning( $"[AINavigation] {Owner.GameObject.Name} is stuck!!! stopping navigation." );
			_velocitySamples.Clear();
			NavigationStopMovement();
		}
	}

}
