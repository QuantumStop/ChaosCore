using System;
namespace Core.AI;

public sealed class MeleeAttack1Action : AIAction
{
	private SkinnedModelRenderer model;


	private float endTime;
	private bool done;

	private const float AttackDuration = 1.2f;

	public MeleeAttack1Action( AIController owner )
	{
		RegisterActionDefinition( AIActionDefinition.ActionList.ActionMeleeAttack1, owner );

	}

	public override void OnEnter( AIController agent )
	{
		done = false;

		model ??= Owner.BodyModel;


		agent.canMove = false;
		agent.Navigation.NavigationStopMovement();

		agent.BodyModel.Set( "b_MeleeAttack", true );
		endTime = Time.Now + AttackDuration;
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
		return agent.Blackboard.activeEnemy.IsValid();
	}

	public override bool IsFailed()
	{
		return Owner.WorldState.Get( AIFacts.LowPain ) || Owner.WorldState.Get( AIFacts.MediumPain ) || Owner.WorldState.Get( AIFacts.HighPain );
	}

	private void Finish()
	{

		Owner.canMove = true;
		Owner.lastMeleeAttack1Time = Time.Now;

		if ( Owner.Blackboard.activeEnemy is not null && (Owner.Blackboard.activeEnemy.WorldPosition - Owner.WorldPosition).Length <= 250 )
		{

			var dmg = new DamageInfo( 15, Owner.GameObject, Owner.GameObject );
			dmg.Tags.Add( "blunt" );

			if ( Owner.Blackboard.activeEnemy is BasePlayer player )
			{
				player.OnDamage( dmg );
			}
			else if ( Owner.Blackboard.activeEnemy is AIController AI )
			{
				AI.OnDamage( dmg );
			}

		}


		done = true;

	}
}
