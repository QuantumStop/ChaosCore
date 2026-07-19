namespace Core.AI;

public class SquadContext
{
#if IGNIS
	[SaveRestore]
#endif
	public BaseEntity activeEnemy { get; set; }
	public bool enemyVisible { get; set; }
	public TimeSince lastSeenEnemyTime { get; set; }
	public Vector3 lastKnownPosition { get; set; }
	public bool shouldFlock { get; set; }

}

public sealed class SquadSensor : BaseSensor<SquadPacket>
{
	public SquadSensor( AIController agent )
			: base( agent )
	{
		squad = agent.aiSquad;
	}

	private readonly AISquad squad;

	private float scanInterval = 0.2f;
	private float scanTimer;
#if IGNIS
	[SaveRestore]
#endif
	public SquadContext context { get; set; }

	public override void UpdatePacket()
	{
		if ( squad is null )
			return;

		scanTimer += Time.Delta;
		if ( scanTimer < scanInterval )
			return;

		scanTimer = 0f;
		UpdateSquadState();
	}
	private void UpdateSquadState()
	{
		int aliveCount = 0;
		int enemyContactCount = 0;
		bool leaderAlive = false;
		var isSquadLeader = false;

		// Ensure context exists
		context ??= new SquadContext();

		BaseEntity bestEnemy = null;
		float bestEnemyDist = float.MaxValue;

		foreach ( var member in squad.members )
		{
			if ( !member.IsValid() )
				continue;

			if ( member.IsAlive )
				aliveCount++;

			if ( member == squad.Leader && member.IsAlive )
				leaderAlive = true;

			var enemy = member.Blackboard.activeEnemy;

			if ( squad.Leader == Agent )
				isSquadLeader = true;

			if ( !enemy.IsValid() || !enemy.IsValid )
				continue;

			enemyContactCount++;

			float dist = member.WorldPosition.Distance( enemy.WorldPosition );
			if ( dist < bestEnemyDist )
			{
				bestEnemyDist = dist;
				bestEnemy = enemy;
			}
		}

		// TODO: update this part with the new squad context, and make a method for transmitting a squad context message
		context.activeEnemy = bestEnemy;
		// send out context to our squad
		if ( context.activeEnemy.IsValid() )
		{
			foreach ( var member in squad.members )
			{
				if ( !member.IsValid() || !member.IsAlive )
					continue;

				member.Blackboard.activeEnemy = context.activeEnemy;

			}
		}

		float distToLeader = float.MaxValue;
		if ( Agent.aiSquad?.Leader is not null )
			distToLeader = Agent.WorldPosition.Distance( Agent.aiSquad.Leader.WorldPosition );

		/*agent.WorldState.Set( AIFacts.IsSquadLeader, isSquadLeader );
		agent.WorldState.Set( AIFacts.SquadHasEnemyContact, enemyContactCount > 0 );
		agent.WorldState.Set( AIFacts.LeaderDistanceOk, leaderAlive && distToLeader < 400f );
		agent.WorldState.Set( AIFacts.SquadCohesionOK, CheckSquadCohesion() );
		agent.WorldState.Set( AIFacts.SquadLeaderAlive, leaderAlive );
		agent.WorldState.Set( AIFacts.SquadIsBroken, aliveCount <= 1 );*/
		Packet.AliveCount = aliveCount;

		Packet.IsSquadLeader = isSquadLeader;
		Packet.SquadLeaderAlive = leaderAlive;
		Packet.SquadHasEnemyContact = enemyContactCount > 0;

		Packet.DistanceToLeader = distToLeader;
		Packet.LeaderDistanceOK = leaderAlive && distToLeader < 400f;

		Packet.SquadCohesionOK = CheckSquadCohesion();
		Packet.SquadIsBroken = aliveCount <= 1;

		Packet.ActiveEnemy = bestEnemy;
	}

	public SquadPacket GetOutputPacketData()
	{
		return Packet;
	}

	private bool CheckSquadCohesion()
	{
		float radius = 400;
		int closeMembers = 0;
		foreach ( var member in Agent.aiSquad.members )
		{
			if ( member == Agent )
				continue;

			if ( member.WorldPosition.Distance( Agent.WorldPosition ) <= radius )
				closeMembers++;

		}

		if ( closeMembers >= (int)Agent.aiSquad.MemberCount * .5 )
			return true;

		return false;
	}
}
