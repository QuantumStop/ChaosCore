namespace Core.AI;

public struct SquadPacket : ISensorPacket
{
	public AIController Owner { get; set; }

	public int AliveCount;

	public bool IsSquadLeader;
	public bool SquadLeaderAlive;
	public bool SquadHasEnemyContact;
	public bool SquadCohesionOK;
	public bool LeaderDistanceOK;
	public bool SquadIsBroken;

	public BaseEntity ActiveEnemy;
	public float DistanceToLeader;
}
