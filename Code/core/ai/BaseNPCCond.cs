namespace Core;

using static BaseNPCConditions;
using static NpcSoundManager;

public class BaseNPCConditions : Component
{
	public enum AIConditions
	{
		COND_NONE,
		COND_SCHEDULE_DONE,
		COND_NEW_ENEMY,
		COND_SEE_ENEMY,
		COND_SEE_FEAR,
		COND_IN_PVS,
		COND_LOST_ENEMY,
		COND_CAN_RANGE_ATTACK1,
		COND_CAN_RANGE_ATTACK2,
		COND_CAN_MELEE_ATTACK1,
		COND_CAN_MELEE_ATTACK2,
		COND_LIGHT_DAMAGE,
		COND_HEAVY_DAMAGE,
		COND_REPEATED_DAMAGE,
		COND_ENEMY_DEAD,
		COND_SMELL,
		COND_HEAR_DANGER,
		COND_HEAR_COMBAT,
		COND_HEAR_PHYSICS,
		COND_TAKING_COVER,
		COND_NO_THREATS, // basically just idle
		COND_BORED, // set randomly when idle, could eventually be used to trigger ambient behaviors
		COND_HEAR_WORLD,
		COND_HEAR_PLAYER,
		COND_SEE_PLAYER,
		COND_HEAR_BULLET_IMPACT,
		COND_HEAR_PHYSICS_DANGER,
		
	}


	[Property, ReadOnly] public List<AIConditions> ActiveConditions { get; set; } = new List<AIConditions>();
	[Property] public bool DrawDebug { get; set; } = false;
	[Property] public float nextGatherCondTime { get; set; } = 2.0f;
	public bool conditionsGathered { get; set; } = false;
	public List<AIConditions> conditionQueue { get; set; } = new List<AIConditions>(); // old idea, keeping incase its needed during conditions expansion
	
	protected override void OnFixedUpdate()
	{
		/*nextGatherCondTime -= Time.Delta;
        if ( nextGatherCondTime < 0 )
        {
			ClearConditions();
		
			nextGatherCondTime = 2;
		}*/
		if ( DrawDebug )
		{
			var position = WorldPosition;
			float offsetY = -10f;

			foreach ( var cond in ActiveConditions )
			{
				
				Gizmo.Draw.IgnoreDepth = true;

				var textPosition = position + Vector3.Up * offsetY;
				Gizmo.Draw.Text( cond.ToString(), new Transform( textPosition ) );

				offsetY -= 10f;

				Gizmo.Draw.IgnoreDepth = false;
			}
		}

	}



	public void ClearConditions()
	{
		ActiveConditions.Clear();
	}

	public bool HasCondition( AIConditions condition )
	{
		return ActiveConditions.Contains( condition );
	}

	public void SetCondition( AIConditions condition )
	{
		if ( !HasCondition( condition ) )
		{
			if (DrawDebug)
			Log.Info($"{condition} set!");
			ActiveConditions.Add( condition );
		}
	}

	public void RemoveCondition( AIConditions condition )
	{
		ActiveConditions.Remove( condition );
	}
}
