using Sandbox;
using System;
using System.Collections.Generic;

public delegate bool TaskFunc( BaseNpc owner, float taskdata, AITask task );


public class AITask
{
	public string Name;
	public TaskFunc Execute;
	public float TaskData;

	public bool HasStarted = false;
	public float ElapsedTime = 0f;

	public AITask Clone()
	{
		return new AITask
		{
			Name = this.Name,
			Execute = this.Execute,
			TaskData = this.TaskData
		};
	}

	public AITask Clear()
	{
		return new AITask
		{
			Name = null,
			Execute = null,
			TaskData = 0
		};
	}

	public void Reset()
	{
		HasStarted = false;
		ElapsedTime = 0f;
	}
}

// The task handlers implement individual tasks. 
// We can give them an owner npc, and task data. 
// returns true when task complete, false when
// the task must continue
public static class NpcTaskHandlers
{
	public static bool TASK_SKIP( BaseNpc owner, float taskdata ) { return true; }

	// Wander Movement Task
	// ============================
	// ACCEPTS: TaskData as maximum random wander distance
	// PURPOSE: Generic movement task that gets a randomly point on the NavMesh and walks to.
	public static bool TASK_WANDER( BaseNpc owner, float taskData, AITask task )
	{
		if ( owner == null || owner.Agent == null )
			return true;

		if ( !task.HasStarted )
		{
			
			owner.BodyModel.Set( "b_IsMoving", true );
			owner.Agent.MaxSpeed = 180.0f;

			var point = owner.GetValidRandomPosition( owner.WorldPosition, taskData, 128f );

			if ( point != null )
			{
				owner.hasWaypoint = true;
				if ( owner.DebugMode )
					Log.Info( $"TASK_WANDER: Moving to {point}");
				//	owner.Agent.MoveTo( point.Value );
				owner.DoMovement( point, BaseNpc.GoalType.GOALTYPE_LOCATION );
				task.HasStarted = true;
				owner._currentTarget = point.Value;
				if ( owner.DebugMode )
					Log.Info( "TASK_WANDER: Movement started" );
			}
			else
			{
				owner.hasWaypoint = false;
				return true;
			}
		}
		else if ( task.HasStarted )
		{ 
		// They like to sometimes stop moving. Ill fix it, eventually.. but for now just shit the bed and cancel the task
		var forwardAmount = Vector3.Dot( owner.WorldRotation.Forward, owner.Agent.Velocity.Normal ); 

		if ( task.ElapsedTime >= 5.0f && forwardAmount <= 0.01f )
		{
				if ( owner.DebugMode )
					Log.Info( "TASK_WANDER: Uh oh, we stopped moving for some reason. Hard shutdown!" );
			owner.Agent.Stop();
			task.Reset();
			return true;
		}
		}
		

		if ( owner.OnReachedMoveTarget() )
		{

			StopMovement( owner, task );
			return true;
		}

		return false;
	}

	// Sound Movement Task
	// ============================
	// ACCEPTS: NONE
	// PURPOSE: Generic movement task that moves to the last heard sound.
	public static bool TASK_MOVE_TO_SOUND( BaseNpc owner, float taskData, AITask task )
	{
		if ( owner == null || owner.Agent == null )
			return true;

		if ( !task.HasStarted )
		{
			
			owner.BodyModel.Set( "b_IsMoving", true );
			owner.Agent.MaxSpeed = 180.0f;

			foreach ( var sound in owner.Targeting.KnownSounds )
			{
				var point = sound.Value;

				owner.hasWaypoint = true;
				owner.DoMovement( point.Position, BaseNpc.GoalType.GOALTYPE_LOCATION );
				task.HasStarted = true;
				owner._currentTarget = point.Position;
				if ( owner.DebugMode )
					Log.Info( "TASK_MOVE_TO_SOUND: Movement started" );

				
			}
			return true;
		}


		if ( owner.OnReachedMoveTarget() )
		{

			StopMovement(owner, task );
			return true;
		}

		return false;
	}

	// Chase Enemy Movement Task
	// ============================
	// ACCEPTS: NONE
	// PURPOSE: Generic movement task that chases the current enemy LKP.
	public static bool TASK_CHASE_ENEMY( BaseNpc owner, float taskData, AITask task )
	{
		if ( owner == null || owner.Agent == null )
			return true; // Fail-safe: end task

		// Begin the task
		if ( !task.HasStarted )
		{
			if ( owner.enemyLastKnownPosition == Vector3.Zero )
			{
				if ( owner.DebugMode )
					Log.Warning( "TASK_CHASE_ENEMY: No valid enemy position." );
				return true; // Nothing to chase
			}

			owner.BodyModel?.Set( "b_IsMoving", true );

			owner.Agent.MaxSpeed = 180.0f;
			owner.shouldChaseEnemy = false;
			owner.chasingEnemy = true;

			var point = owner.enemyLastKnownPosition;

			owner.hasWaypoint = true;
			owner._currentTarget = point;

			owner.DoMovement( point, BaseNpc.GoalType.GOALTYPE_ENEMY );

			task.HasStarted = true;

			if ( owner.DebugMode )
				Log.Info( $"TASK_CHASE_ENEMY: Chasing to {point}" );

			return false; // Still in progress
		}

		// Check if we've reached the move target
		if ( owner.OnReachedMoveTarget() )
		{
			if ( owner.DebugMode )
				Log.Info( "TASK_CHASE_ENEMY: Reached target." );

			owner.chasingEnemy = false;
			owner.BodyModel?.Set( "b_IsMoving", false );

			StopMovement( owner, task );

			return true; // Task complete
		}

		return false; // Still chasing
	}

	// Scripted Movement Task
	// ============================
	// ACCEPTS: NONE
	// PURPOSE: Scripted movement task that gets the current path corner.
	public static bool TASK_SCRIPTED_MOVE( BaseNpc owner, float taskData, AITask task )
	{
		if ( owner == null || owner.Agent == null )
			return true;

		if ( !task.HasStarted )
		{
			StartScriptedMove( owner, task );
			return false;
		}

		if ( owner.OnReachedMoveTarget() )
		{
			owner.hasStartedPathCornerMovement = false;
			if ( owner.DebugMode )
				Log.Info( "TASK_SCRIPTED_MOVE: Movement Ended" );

			var previousTarget = owner._currentPathCorner;
			//previousTarget.OnReachedPathTarget();

			if ( owner._currentPathCorner != null ) 
			{
				// Start next move if there is a new corner, otherwise come to a stop and finish the task
				StartScriptedMove( owner, task );
				return false;
			}

			StopMovement( owner, task );
			return true;
		}

		return false;
	}

	private static void StartScriptedMove( BaseNpc owner, AITask task )
	{
		owner.BodyModel.Set( "b_IsMoving", true );
		owner.Agent.MaxSpeed = 180.0f;

		var point = owner._currentPathCorner;

		if ( point == null )
		{
			
			Log.Warning( "StartMove called but _currentPathCorner is null!!" );
			return;
		}

		owner.hasWaypoint = true;
		owner.DoMovement( point.WorldPosition, BaseNpc.GoalType.GOALTYPE_PATHCORNER );
		owner.hasStartedPathCornerMovement = true;
		task.HasStarted = true;
		owner._currentTarget = point.WorldPosition;
		if ( owner.DebugMode )
			Log.Info( $"TASK_SCRIPTED_MOVE: Moving to {point}" );
	}


	// Wait Task
	// ============================
	// ACCEPTS: TaskData as time to wait
	// PURPOSE: Makes the npc wait for a specified time.
	public static bool TASK_WAIT( BaseNpc owner, float taskData, AITask task )
	{
		if ( task.HasStarted )
		{
			task.ElapsedTime += Time.Delta;

			if ( task.ElapsedTime >= taskData )
			{
				task.Reset();
				return true;
			}
		}
		else
		{
			task.HasStarted = true;
			task.ElapsedTime = 0f;
		}

		return false;
	}

	// Stop Movement Task
	// ============================
	// ACCEPTS: NONE
	// PURPOSE: Stops the current NPC movement.
	public static bool TASK_STOP_MOVEMENT( BaseNpc owner, float taskData, AITask task )
	{
		if ( task.HasStarted )
		{
			StopMovement( owner, task );
			return true;	
		}
		else
		{
			task.HasStarted = true;
		}

		return false;
	}

	// Alert Noise Task
	// ============================
	// ACCEPTS: NONE
	// PURPOSE: Plays an alert noise from the NPC Definition Resource.
	public static bool TASK_ALERT_NOISE( BaseNpc owner, float taskData, AITask task )
	{
		if ( owner == null ) return true;

		if ( !owner.HasTaskStarted )
		{
			foreach ( var sound in owner.NpcDef.AlertSounds ) // Play an alert noise from our definition
			{
				Sound.Play( sound ).ListenLocal = true;
			}
			owner.HasTaskStarted = true;
		}
		else
		{
			// Maybe an anim can be played here

				owner.StopTask();
				return true;
			
		}

		return false;
	}

	// Bored Task
	// ============================
	// ACCEPTS: TaskData as time to wait
	// PURPOSE: Causes the NPC to stand still and play an animation. Mostly a test.
	public static bool TASK_BORED( BaseNpc owner, float taskData, AITask task )
	{
		if ( owner == null ) return true;

		if ( !owner.HasTaskStarted )
		{
			if ( owner.DebugMode )
				Log.Info( "NPC is bored..." );
			owner.BodyModel.Set( "b_IsMoving", false );
			owner.BodyModel.Set( "b_Busy", true );
			owner.HasTaskStarted = true;
			owner.WaitTime = taskData;
		}
		else
		{
			owner.WaitTime += Time.Delta;
			if ( owner.WaitTime >= taskData )
			{
				owner.BodyModel.Set( "b_Busy", false );
				owner.BodyModel.Set( "b_IsMoving", true );
				
				owner.StopTask();
				return true;
			}
		}

		return false;
	}

	// Get Enemy Task
	// ============================
	// ACCEPTS: NONE
	// PURPOSE: Gets the closest enemy.
	public static bool TASK_GET_ENEMY( BaseNpc owner, float taskData, AITask task )
	{
		if ( owner == null || owner.ActiveEnemies.Count == 0 )
			return true; // No enemy!

		BaseEntity closest = null;
		float closestDistance = float.MaxValue;

		foreach ( var enemy in owner.ActiveEnemies )
		{
			if ( !enemy.IsValid() )
				continue;

			float dist = owner.WorldPosition.Distance( enemy.WorldPosition );
			if ( dist < closestDistance )
			{
				closest = enemy;
				closestDistance = dist;
			}
		}

		if ( closest.IsValid() )
		{
			owner.CurrentEnemy = closest;
			owner.shouldChaseEnemy = true;
			return true; // Success, enemy found
		}

		return true;
	}

	static public void StopMovement( BaseNpc owner, AITask task )
	{
		owner.NavCheckTarget(); // commented out Agent.Stop since this uses that
		if ( owner.DebugMode )
			Log.Info( $"{task.Name}: Reached move target. Stopping..." );
		owner.hasWaypoint = false;
		owner.Agent.Stop();
		
		//task.Reset();
	}

	// Range Attack 1 Task
	// ============================
	// ACCEPTS: NONE
	// PURPOSE: Causes the NPC to stand still and play an animation. Mostly a test.
	public static bool TASK_RANGE_ATTACK1( BaseNpc owner, float taskData, AITask task )
	{
		if ( owner == null ) return true;

		if ( !owner.HasTaskStarted )
		{
			StopMovement(owner,task); // REALLY make sure! post statement: not that easy now is it. Review and fix this
			if ( owner.DebugMode )
				Log.Info( "NPC is attacking" );
			owner.BodyModel.Set( "b_IsMoving", false );
			owner.BodyModel.Set( "b_Attack", true );
			owner.HasTaskStarted = true;
			owner.Conditions.RemoveCondition( BaseNPCConditions.AIConditions.COND_CAN_RANGE_ATTACK1 ); // REMOVE HERE
			owner.nextRangeAttackTime = Time.Now + 3.0f;
			//	owner.WaitTime = taskData;
		}
		else 
		{
			if ( owner.BodyModel.Sequence.IsFinished )
			{
				owner.BodyModel.Set( "b_Attack", false );
				owner.BodyModel.Set( "b_IsMoving", true );
				
				owner.StopTask();
				return true;
			}
			return false;
		}
		

			

		

		return false;
	}

	// Sound Movement Task
	// ============================
	// ACCEPTS: NONE
	// PURPOSE: Generic movement task that moves to the last heard sound.
	public static bool TASK_CINE_MOVE_TO_POSITION( BaseNpc owner, float taskData, AITask task )
	{
		if ( owner == null || owner.Agent == null )
			return true;

		if ( !task.HasStarted )
		{

			owner.BodyModel.Set( "b_IsMoving", true );
			if (owner._Cine.MoveToPosition == ScriptedSequence.MovementOptions.Walk)
			owner.Agent.MaxSpeed = 75.0f;
			else
			owner.Agent.MaxSpeed = 125.0f;


			var point = owner._Cine.WorldPosition;
			owner.hasWaypoint = true;
			owner.DoMovement( point, BaseNpc.GoalType.GOALTYPE_PATHCORNER );
			owner.hasStartedPathCornerMovement = true;
			task.HasStarted = true;
			owner._currentTarget = point;
			if ( owner.DebugMode )
					Log.Info( "TASK_CINE_MOVE_TO_POSITION: Movement started" );


			
		//	return false;
		}
		

		if ( owner.OnReachedMoveTarget() )
		{
			owner.hasReachedCine = true;
			owner.shouldMoveToCine = false;
			StopMovement( owner, task );
			return true;
		}

		return false;
	}

}
