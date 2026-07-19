using System;
namespace Core.AI;

[Obsolete( "ChooseNewSquadLeaderAction does not work!" )]
public sealed class ChooseNewSquadLeaderAction : AIAction
{

	private float endTime = 0f;
	private bool done;

	public ChooseNewSquadLeaderAction( AIController owner )
	{
		Owner = owner;
		Cost = 1.5f;

		RegisterActionDefinition( AIActionDefinition.ActionList.ActionRangeAttack1, owner );

	}

	public override void OnEnter( AIController agent )
	{
		done = false;
	}

	public override void Perform( AIController agent )
	{
		if ( done )
			return;

		if ( Time.Now >= endTime )
		{
			Finish();
		}
	}

	public override bool IsDone() => done;

	public override bool CheckProceduralPrecondition( AIController agent )
	{
		var enemy = agent.Blackboard.activeEnemy;
		if ( agent.WorldState.Has( "" ) )
			return false;

		float dist = (enemy.WorldPosition - agent.WorldPosition).Length;
		return dist <= agent.Definition.RangeAttack1_Distance;
	}


	private void Finish()
	{
		done = true;
	}
}

