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
		squad = agent.AISquad;
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
		NpcTargetingSensor.SensorData? bestContact = null;

		foreach ( var member in squad.members )
		{
			if ( !member.IsValid() )
				continue;

			if ( member.IsAlive )
				aliveCount++;

			if ( member == squad.Leader && member.IsAlive )
				leaderAlive = true;

			if ( squad.Leader == Agent )
				isSquadLeader = true;

			var contact = member.TargetingSensor?.PrimaryTarget;
			if ( !member.IsAlive || !contact.HasValue || !contact.Value.Tracking || !contact.Value.Target.IsValid() )
				continue;

			enemyContactCount++;

			float dist = Agent.WorldPosition.Distance( contact.Value.LastKnownPosition );
			if ( dist < bestEnemyDist )
			{
				bestEnemyDist = dist;
				bestEnemy = contact.Value.Target;
				bestContact = contact;
			}
		}
		
		// TODO: update this part with the new squad context, and make a method for transmitting a squad context message
		context.enemyVisible = bestContact.HasValue;
		if ( bestContact.HasValue )
		{
			context.activeEnemy = bestEnemy;
			context.lastKnownPosition = bestContact.Value.LastKnownPosition;
			context.lastSeenEnemyTime = 0f;
			Agent.TargetingSensor.ReceiveSquadContact( bestContact.Value );
		}
		else if ( !context.activeEnemy.IsValid() || context.lastSeenEnemyTime > Agent.Definition.LostTargetTimeout )
		{
			context.activeEnemy = null;
		}

		float distToLeader = float.MaxValue;
		if ( Agent.AISquad?.Leader is not null )
			distToLeader = Agent.WorldPosition.Distance( Agent.AISquad.Leader.WorldPosition );

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

		Packet.ActiveEnemy = context.activeEnemy;
	}

	public SquadPacket GetOutputPacketData()
	{
		return Packet;
	}

	private bool CheckSquadCohesion()
	{
		float radius = 400;
		int closeMembers = 0;
		foreach ( var member in Agent.AISquad.members )
		{
			if ( member == Agent )
				continue;

			if ( member.WorldPosition.Distance( Agent.WorldPosition ) <= radius )
				closeMembers++;

		}

		if ( closeMembers >= (int)Agent.AISquad.MemberCount * .5 )
			return true;

		return false;
	}
}
