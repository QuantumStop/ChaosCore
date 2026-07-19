namespace Core.AI;

public sealed class MeleeAttack1Action : AIAction
{
	private SkinnedModelRenderer _model;


	private float _endTime;
	private bool _done;

	private const float _attackDuration = 1.2f;

	public MeleeAttack1Action( AIController owner ) => RegisterActionDefinition( AIActionDefinition.ActionList.ActionMeleeAttack1, owner );

	public override void OnEnter( AIController agent )
	{
		_done = false;

		_model ??= Owner.BodyModel;

		agent.CanMove = false;
		agent.Navigation.NavigationStopMovement();

		agent.BodyModel.Set( "b_MeleeAttack", true );
		_endTime = WorldTime.Now + _attackDuration;
	}

	public override void Perform( AIController agent )
	{
		if ( _done )
			return;

		if ( WorldTime.Now >= _endTime )
		{
			Finish();
		}
	}

	public override bool IsDone() => _done;

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

		Owner.CanMove = true;
		Owner.LastMeleeAttack1Time = WorldTime.Now;

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


		_done = true;

	}
}
