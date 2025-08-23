using chaoscore;
using Sandbox;
using System;
using System.Collections.Generic;
using static BaseNPCConditions;

public class AISchedule
{
	public string Name;
	public List<AITask> Tasks;
	public List<AIConditions> Interrupts;

	public AISchedule( string name, AITask[] tasks, params AIConditions[] interrupts ) // recognize me?
	{
		Name = name;
		Tasks = new List<AITask>( tasks );
		Interrupts = new List<AIConditions>( interrupts );
	}
}


public class NpcBrain : Component
{
	public enum AIState
	{
		INVALID = -1,
		NONE = 0,
		IDLE,
		ALERT,
		//	SEARCHING, 
		COMBAT,
		SCRIPTED,
		DEAD
	}

	//AIState State;

	public AIState idealState;

	public BaseNpc Owner { get; set; }

	[Property] public bool DrawDebug { get; set; }
	[Property, ReadOnly] public AIState State { get; set; }

	[Property, ReadOnly] public float _NextThinkTime;


	public event Action OnThink;


	private Queue<AITask> TaskList;
	private BaseNPCConditions Conditions;

	public AITask CurrentTask => HasActiveSchedule() ? TaskList.Peek() : null;
	private AISchedule CurrentAISchedule;

	protected override void OnStart()
	{
		Conditions = Components.Get<BaseNPCConditions>();

		//	CheckForScriptedMovement();
		NPCInitThink();
		idealState = AIState.IDLE;
		//State = State;
		SelectSchedule();
		_NextThinkTime = Scene.GetSystem<AIThinkScheduler>().GetNextThinkTime();

	}

	public void NPCInitThink()
	{
		if ( Owner.GetSleepState() == BaseNpc.AI_SleepState.SLEEPSTATE_AUTOPVS )
		{
			// This code is a bit wonky, but it makes it easier for level designers to
			// select this option in Hammer. So we set a sleep flag to indicate the choice,
			// and then set the sleep state to awake (normal)
			//	AddSleepFlags( AI_SLEEP_FLAG_AUTO_PVS );
			//Owner.SetSleepState( AISS_AWAKE );
		}

		if ( Owner.GetSleepState() == BaseNpc.AI_SleepState.SLEEPSTATE_AUTOPVS_AFTER_PVS )
		{
			//	AddSleepFlags( AI_SLEEP_FLAG_AUTO_PVS_AFTER_PVS );
			//	Owner.SetSleepState( AISS_AWAKE );
		}

		if ( Owner.GetSleepState() > BaseNpc.AI_SleepState.SLEEPSTATE_AWAKE )
		{
			Sleep();
		}

		if ( Owner.canFollowMoveTarget )
		{
			CheckForScriptedMovement();
		}

	}

	public void Sleep()
	{
		// Don't render.
		Owner.BodyModel.Enabled = false;

		if ( State == AIState.SCRIPTED )
		{
			Log.Info( $"{Owner.TargetName} put to sleep while in Scripted state!\n" );
		}

		//	VacateStrategySlot();

		// Slam my schedule.
		//	StartSchedule( SCHED_SLEEP );

		//	m_OnSleep.FireOutput( this, this );
	}

	protected override void OnFixedUpdate()
	{
		if ( Time.Now < _NextThinkTime )
			return;

		NPCThink();
		
		

		_NextThinkTime = Time.Now + 0.1f /*Scene.GetSystem<AIThinkScheduler>().GetNextThinkTime()*/;
	}

	public void CheckForScriptedMovement()
	{
		if ( Owner._currentPathCorner != null && !Owner.hasStartedPathCornerMovement )
		{
			StartSchedule( AISchedules.SCHED_SCRIPTED_MOVE );
		}
	}

	public void SetNextThink( float nextThink )
	{

		_NextThinkTime = Time.Now + nextThink;

	}


	public virtual void NPCThink() // Quake never leaves
	{
		if ( Conditions == null || Owner == null )
			return;

		if ( DrawDebug )
			Log.Info( $"[{Time.Now:0.00000}] {GameObject.Name} is thinking." );
		if ( Owner.Health <= 0 )
		{

			Owner.EventKilled();
			return;
		}

		Owner.Think(); // run the BaseNPC Think first.

		bool bInPVS = Owner.CheckPVSCondition();

		if (Owner.GetSleepState() > BaseNpc.AI_SleepState.SLEEPSTATE_AWAKE)
		Owner.UpdateSleepState( bInPVS );

		// Why do this? Well, my current thinking is that
		// the NPCThink should be centralized in the brain.
		// Then, we can determine sleep state here, and adjust accordingly.
		// The thinking is already centralized here, so i figured this would make sense. Maybe its ugly!

		//Owner.EvaluateCreatures(); // Targeting stuff
		State = idealState;
		State = State;

		//Owner.GatherConditions();


		Owner.ApplyFlockingAvoidance(); // Should be moved into a nav component.. whenever i make it




		if ( DrawDebug )
		{
			DrawDebugText();
		}
		OnThink?.Invoke(); // invoke Think after we run our AI, for everything dependant on it (debug)
	}

	public virtual void DrawDebugText()
	{
		Gizmo.Draw.Text( $"State: {State}", new Transform( Owner.WorldPosition ) );

		if ( CurrentAISchedule != null && CurrentTask != null )
		{
			Gizmo.Draw.Text( $"Schedule: {CurrentAISchedule.Name}", new Transform( Owner.WorldPosition + Vector3.Up * 35 ) );
			Gizmo.Draw.Text( $"Task: {CurrentTask.Name}", new Transform( Owner.WorldPosition + Vector3.Up * 15 ) );
			Gizmo.Draw.Text( $"TaskData: {CurrentTask.TaskData}", new Transform( Owner.WorldPosition + Vector3.Up * 25 ) );
		}
		else if ( CurrentAISchedule == null )
		{
			Gizmo.Draw.Text( $"Brain: Schedule is null!", new Transform( Owner.WorldPosition + Vector3.Up * 35 ) );

		}
		else if ( CurrentTask == null )
		{
			Gizmo.Draw.Text( $"Brain: Task is null!", new Transform( Owner.WorldPosition + Vector3.Up * 15 ) );
		}
	}

	// this will be redone to just return a schedule instead of this weird method of starting it through another method
	public virtual void SelectSchedule()
	{
		if ( DrawDebug )
		{
			Log.Info( "SelectSchedule: Begin" );
		}
		State = idealState;

		switch ( State )
		{
			case (AIState.NONE):
				{
					// For now we just force it to go to idle
					State = AIState.IDLE;
					break;
				}

			case (AIState.IDLE):
				{

					/*if ( Conditions.HasCondition( AIConditions.COND_HEAR_DANGER ))
					{
						InterruptSchedule( "Heard danger - switching to ALERT" );
						StartSchedule( AISchedules.SCHED_ALERT_RUN_RANDOM );
						State = AIState.ALERT;
					}
					*/
					if ( State == AIState.IDLE && (TaskList == null || TaskList.Count == 0) )
					{
						StartIdleSchedule();
						//TestFlockingSchedule();
					}
					
					break;
				}
			case (AIState.ALERT):
				{

					/*if ( Conditions.HasCondition(AIConditions.COND_HEAR_DANGER))
					{
						//	InterruptSchedule( "Heard danger - switching to ALERT" );
						StartSchedule( AISchedules.SCHED_ALERT_RUN_RANDOM );
					}*/
					StartSchedule(AISchedules.SCHED_IDLE_STAND);
					break;
				}
			case (AIState.COMBAT):
				{
					if ( Owner == null ) break;

					if ( !Owner.HasEnemy )
					{
						State = AIState.ALERT;
					}

					Owner.BodyModel.Set( "b_isMad", true );

					/*if ( Owner.Brain.CurrentAISchedule == AISchedules.SCHED_RANGE_ATTACK1 &&
					!Conditions.HasCondition( BaseNPCConditions.AIConditions.COND_CAN_RANGE_ATTACK1 ) )
					{
						
						Owner.Brain.InterruptSchedule( "Interrupting RANGE_ATTACK1 schedule: condition no longer valid." ); // This should cancel the current running schedule
					}*/


					// Only chase if we know enemy's last position, we're not already chasing, and should chase!
					bool canChase = Owner.enemyLastKnownPosition != Vector3.Zero &&
									!Owner.chasingEnemy &&
									Owner.shouldChaseEnemy;

					bool seesEnemy = Owner.Conditions.HasCondition( BaseNPCConditions.AIConditions.COND_SEE_ENEMY );

					if ( canChase && seesEnemy )
					{

						if ( Owner.Brain.CurrentAISchedule != AISchedules.SCHED_CHASE_ENEMY )
						{
							if ( DrawDebug )
							{
								Log.Info( $"{Owner.TargetName} is starting chase schedule." );
							}

							StartSchedule( AISchedules.SCHED_CHASE_ENEMY );
						}
					}

					if ( Conditions.HasCondition( BaseNPCConditions.AIConditions.COND_SEE_ENEMY ) &&
							Conditions.HasCondition( BaseNPCConditions.AIConditions.COND_CAN_RANGE_ATTACK1 ) )
					{
						if ( Owner.Brain.CurrentAISchedule != AISchedules.SCHED_RANGE_ATTACK1 )
						{
							if ( DrawDebug )
							{
								Log.Info( $"{Owner.TargetName} is starting range attack schedule." );
							}
							StartSchedule( AISchedules.SCHED_RANGE_ATTACK1 );
							//Conditions.RemoveCondition( BaseNPCConditions.AIConditions.COND_CAN_RANGE_ATTACK1 );
						}
					}

					break;
				}
			case (AIState.SCRIPTED):
				if ( Owner._Cine != null ) // scripted sequences
				{
					if ( Owner.shouldMoveToCine && !Owner.hasReachedCine )
					{
						if ( DrawDebug )
							Log.Info( $"{Owner.TargetName} moving to scripted sequence" );

						StartSchedule( AISchedules.SCHED_CINE_MOVE_TO_POSITION );
						break;
					}
					else
					{
						if ( DrawDebug )
							Log.Info( $"{Owner.TargetName} beginning scripted sequence" );
						StartSchedule( AISchedules.SCHED_CINE );
					}
				
					
				}
				break;
		}

		if ( HasActiveSchedule() )
		{
			var task = TaskList.Peek();

			if ( DrawDebug )
				Log.Info( $"Executing task: {task.Name} in state {State}" );

			if ( TaskComplete( task ) )
			{
				if ( DrawDebug )
				{
					Log.Info( $"{task.Name} executed successfully." );
				}
				//	Conditions.SetCondition( AIConditions.COND_SCHEDULE_DONE ); // The task has returned true at last, therefore we fire this condition 
				task.Reset();
				TaskList.Dequeue();

				// Only restart idle schedule if we are in idle.. ugly bad kill kill kill
				if ( TaskList.Count == 0 && State == AIState.IDLE )
				{
					if ( Owner.shouldWanderOnIdle )
						StartIdleSchedule();
					else
						StartSchedule(AISchedules.SCHED_IDLE_STAND);

				}
			}
		}

		
	}

	public bool TaskComplete( AITask task )
	{
		if ( DrawDebug )
		{
			Log.Info( "Running task.." );
		}
		return task.Execute( Owner, task.TaskData, task );
	}

	public bool HasActiveSchedule() { return TaskList != null && TaskList.Count > 0; }

	public void StartSchedule( AISchedule schedule )
	{
		if ( DrawDebug )
			Log.Info( $"Starting Schedule: {schedule.Name}" );
		CurrentAISchedule = schedule;

		// We set the currentaisched to the parameter. Then we make a new task queue, and clone the needed tasks into it from the schedule definition.
		var freshTasks = new Queue<AITask>();
		foreach ( var task in schedule.Tasks )
		{
			if ( DrawDebug )
				Log.Info( $"Queuing Task: {task.Name}" );
			freshTasks.Enqueue( task.Clone() );
		}

		TaskList = freshTasks;
	}


	public void InterruptSchedule( string reason = "Unknown" )
	{
		if ( DrawDebug )
			Log.Info( $"Schedule interrupted: {reason}" );
		var task = TaskList.Peek();
		task.Reset();
		// Clear current schedule queue
		TaskList?.Clear();
		
		
		TaskList = null;
	}

	void SetIdealState( AIState eIdealState )
	{
		if ( eIdealState != idealState )
		{
			idealState = eIdealState;
		}
	}

	public void GetNewSchedule()
	{
		SelectSchedule();
	}
	public void MaintainSchedule()
	{
		if ( DrawDebug )
			Log.Info("MaintainSchedule: Starting");
		if ( !Conditions.conditionsGathered )
		{
			Owner.GatherConditions();
		}

		if ( ShouldSelectIdealState() )
		{
			AIState eIdealState = Owner.SelectIdealState();
			SetIdealState( eIdealState );
		}

		GetNewSchedule();

		Conditions.ClearConditions();
	}

	public bool ShouldSelectIdealState()
	{
		// Don't get ideal state if you are supposed to be dead.
		if ( idealState == AIState.DEAD )
			return false;


		// If I'm supposed to be in scripted state, but i'm not yet, do not allow 
		// SelectIdealState() to be called, because it doesn't know how to determine 
		// that a NPC should be in SCRIPT state and will stomp it with some other 
		// state. (Most likely ALERT)
		if ( (idealState == AIState.SCRIPTED) && (State != AIState.SCRIPTED) )
			return false;

		// If the NPC has any current conditions, and one of those conditions indicates
		// that the previous schedule completed successfully, then don't run SelectIdealState(). 
		// Paths between states only exist for interrupted schedules, or when a schedule 
		// contains a task that suggests that the NPC change state.
		if ( !Conditions.HasCondition( AIConditions.COND_SCHEDULE_DONE ) )
			return true;

		if ( (State == AIState.COMBAT) && ( Owner.GetEnemy() == null) )
			return true;

		if ( (State == AIState.IDLE || State == AIState.ALERT ) && ( Owner.GetEnemy() != null) )
			return true;

		return false;
	}

	// This is just a bit cleaner than doing it in selectschedule until i redo it
	private void StartIdleSchedule()
	{
		Random rnd = new Random();
		int chance = rnd.Next( 5 );

		if ( chance == 1 )
		{
			StartSchedule( AISchedules.SCHED_IDLE_STAND );
		}
		else
		{
			StartSchedule( AISchedules.SCHED_IDLE_WANDER );
		}
			

		State = AIState.IDLE;
		
	}

	// This will eventually all be improved (including tasks) 
	// to allow for overriding and npc-specific schedule and task implementation.
	// I also need to implement condition interrupts,
	// Taskdata is specified (its weird, not all tasks need it. so you need to know how the task works)
	// and then used in the task however applicable.
	//
	public class AISchedules
	{
		// IDLE Wandering Schedule
		public static AISchedule SCHED_IDLE_WANDER = new AISchedule(
			"SCHED_IDLE_WANDER",

			   new AITask[]
	{
			new AITask { Name = "Idle Wait", Execute = NpcTaskHandlers.TASK_WAIT, TaskData = 4.0f },
			new AITask { Name = "Random Wander", Execute = NpcTaskHandlers.TASK_WANDER, TaskData = 512.0f },
			new AITask { Name = "Idle Bored", Execute = NpcTaskHandlers.TASK_BORED, TaskData = 3.0f }
	},

		new AIConditions[]
			{ 
				AIConditions.COND_NEW_ENEMY,
				AIConditions.COND_SEE_ENEMY,
				
			}
		
		);

		// Flocking test
		public static AISchedule SCHED_IDLE_FLOCK_WANDER = new AISchedule(
			"SCHED_IDLE_FLOCK_WANDER",

			   new AITask[]
			{
				new AITask { Name = "Flock Wander", Execute = NpcTaskHandlers.TASK_WANDER, TaskData = 512.0f }
			}
		);
		// IDLE Standing Schedule
		public static AISchedule SCHED_IDLE_STAND = new AISchedule(
			"SCHED_IDLE_STAND",

			   new AITask[]
			{
				new AITask { Name = "Idle Wait", Execute = NpcTaskHandlers.TASK_WAIT, TaskData = 1.0f },
		//		new AITask { Name = "Idle Bored", Execute = NpcTaskHandlers.TASK_BORED, TaskData = 3.0f }
			}
		);
		// ALERT Run Random Schedule
		public static AISchedule SCHED_ALERT_RUN_RANDOM = new AISchedule(
			"SCHED_ALERT_RUN_RANDOM",

			   new AITask[]
			{
				new AITask { Name = "Alert Run Random", Execute = NpcTaskHandlers.TASK_WANDER, TaskData = 512.0f },
				new AITask { Name = "Alert Wait", Execute = NpcTaskHandlers.TASK_WAIT, TaskData = 0.5f }
			}
		);

		// ALERT Run To Sound Schedule
		public static AISchedule SCHED_ALERT_GO_TO_SOUND = new AISchedule(
			"SCHED_ALERT_GO_TO_SOUND",

			   new AITask[]
			{
			new AITask { Name = "Alert Run Random", Execute = NpcTaskHandlers.TASK_MOVE_TO_SOUND, TaskData = 0 },
			new AITask { Name = "Alert Noise", Execute = NpcTaskHandlers.TASK_ALERT_NOISE, TaskData = 0.5f },
			new AITask { Name = "Alert Wait", Execute = NpcTaskHandlers.TASK_WAIT, TaskData = 0.5f }
			}
		);
		// Scripted Movement, currently for path corner movement
		public static AISchedule SCHED_SCRIPTED_MOVE = new AISchedule(
			"SCHED_SCRIPTED_MOVEMENT",

			   new AITask[]
			{
			new AITask { Name = "Wait", Execute = NpcTaskHandlers.TASK_WAIT, TaskData = 0.1f },
			new AITask { Name = "Scripted Movement", Execute = NpcTaskHandlers.TASK_SCRIPTED_MOVE, TaskData = 0 },
			new AITask { Name = "Stop Movement", Execute = NpcTaskHandlers.TASK_STOP_MOVEMENT, TaskData = 0 }
			}

		);

		public static AISchedule SCHED_SCRIPTED_MOVE_CONTINUOUS = new AISchedule(
			"SCHED_SCRIPTED_MOVEMENT_CONTINUOUS",

			   new AITask[]
			{
			
			new AITask { Name = "Scripted Movement", Execute = NpcTaskHandlers.TASK_SCRIPTED_MOVE, TaskData = 0 },
		
			}

		);

		//	COMBAT Chase Enemy Schedule
		public static AISchedule SCHED_CHASE_ENEMY = new AISchedule(
			"SCHED_CHASE_ENEMY",

			   new AITask[]
			{
			//	new AITask { Name = "Stop Movement", Execute = NpcTaskHandlers.TASK_STOP_MOVEMENT, TaskData = 0 },
			new AITask { Name = "Wait", Execute = NpcTaskHandlers.TASK_WAIT, TaskData = 0.1f },
			new AITask { Name = "Find Enemy", Execute = NpcTaskHandlers.TASK_GET_ENEMY, TaskData = 0 },
			new AITask { Name = "Chase Enemy", Execute = NpcTaskHandlers.TASK_CHASE_ENEMY, TaskData = 0 },
			new AITask { Name = "Stop Movement", Execute = NpcTaskHandlers.TASK_STOP_MOVEMENT, TaskData = 0 }
			}
		);

		public static AISchedule SCHED_RANGE_ATTACK1 = new AISchedule(
			"SCHED_RANGE_ATTACK1",

			   new AITask[]
			{
			new AITask { Name = "Stop Movement", Execute = NpcTaskHandlers.TASK_STOP_MOVEMENT, TaskData = 0 },
			new AITask { Name = "Wait", Execute = NpcTaskHandlers.TASK_WAIT, TaskData = 0.1f },
			new AITask { Name = "Range Attack 1", Execute = NpcTaskHandlers.TASK_RANGE_ATTACK1, TaskData = 0 }
			}
		
		);
		//CINE Move to Position Schedule
		public static AISchedule SCHED_CINE_MOVE_TO_POSITION = new AISchedule(
			"SCHED_CINE_MOVE_TO_POSITION",

			   new AITask[]
			{
			new AITask { Name = "Move to Scripted Sequence", Execute = NpcTaskHandlers.TASK_CINE_MOVE_TO_POSITION, TaskData = 0 },
		
			}

		);

		// CINE Schedule
		public static AISchedule SCHED_CINE = new AISchedule(
			"SCHED_CINE",

			   new AITask[]
			{
				new AITask { Name = "Scripted Sequence", Execute = NpcTaskHandlers.TASK_WAIT, TaskData = 1.0f }, // todo: maybe a proper task?
			}
		);
	}

	


}




