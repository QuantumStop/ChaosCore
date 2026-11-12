using Sandbox.Navigation;
using System;
using static Core.AI.AIController;

namespace Core.AI;

public struct NavData()
{
	public AIController Controller;
	public Vector3 Position;
	public GoalType GoalType;
}

public partial class AINavigation : AIModule
{
	public int FailureRevision { get; private set; }

	/// <summary>
	/// Records a failed movement attempt.
	/// </summary>
	public void ReportFailure()
	{
		FailureRevision++;
		Owner.NavStatus = NavigationStatus.NAVIGATION_IN_ERROR;
	}
	public AIMotor Motor { get; private set; }
	public AIGroundMotor GroundMotor => Motor as AIGroundMotor;

	/// <summary>
	/// Assign and initialize this navigation module's motor.
	/// </summary>
	public void SetMotor( AIMotor motor )
	{
		ArgumentNullException.ThrowIfNull( motor );

		if ( motor.Owner.IsValid() && motor.Owner != Owner )
			throw new ArgumentException( "Motor must belong to this NPC.", nameof( motor ) );

		// Try to set the same motor should not do anything
		if ( Motor == motor )
			return;

		// Clean a previous motor to be able to switch motors 
		// and to make sure we're using the correct one
		ClearLocalMovement();
		Motor?.Terminate();
		motor.Init( Owner );
		Motor = motor;
	}

	public override void Terminate()
    {
        ClearLocalMovement();
        Motor?.Terminate();
    }

	public NavData? Data;
	public MoveType ControllerMoveType;
	public NavMeshPath Path;
	public TimeSince TimeSinceMovementStart;

	public override void Init( AIController owner )
	{
		Owner = owner;
		ControllerMoveType = owner.MovementType;
		
		// TODO: I think this should be defined in a definition instead!
		if ( ControllerMoveType == MoveType.MOVE_GROUND || ControllerMoveType == MoveType.MOVE_CRAWL )
			SetMotor( new AIGroundMotor() );
	}

	public void NavigationDoMovement( NavData data )
	{
		// Keep the current path when an action refreshes an unchanged destination.
		if ( Motor is AIGroundMotor && Owner.Agent.IsValid() && Owner.Agent.IsNavigating && !Owner.Agent.IsTraversingLink
			&& Destination is Vector3 destination && destination.Distance( data.Position ) < 1f )
		{
			Data = data;
			Owner.IsMoving = true;
			return;
		}

		Data = data; // Assign nav data here, held onto until we reach are position or recieve a new call to movement.
		Owner.IsMoving = true;

		TimeSinceMovementStart = 0f;

		if ( ControllerMoveType == MoveType.MOVE_NONE || Motor is null )
			return;

		MoveTo( data.Position );

		if ( Motor is AIGroundMotor )
			Path = Owner.Agent.GetPath();
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
		Stop();

	}

	private const int _velocitySampleCount = 20;
	private const float _stuckVelocityThreshold = 1f;
	private const float _stuckAverageThreshold = 0.1f;
	private const float _sampleInterval = 0.1f;

	private readonly Queue<float> _velocitySamples = new( _velocitySampleCount );
	private float _sampleTimer;

	public void NavigationCheckForErrors()
	{
		if ( Owner.Agent.IsTraversingLink || IsYielding )
		{
			_velocitySamples.Clear();
			_sampleTimer = 0f;
			return;
		}

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
			ReportFailure();
		}
	}
}
