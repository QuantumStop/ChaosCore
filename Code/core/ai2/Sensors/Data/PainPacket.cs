namespace Core.AI;

public struct PainPacket : ISensorPacket
{
	public AIController Owner { get; set; }

	public float painScore;
	public bool painIsLow;
	public bool painIsMedium;
	public bool painIsHigh;
	public float painTime;
	public bool shouldUpdateWorldState;
	public float timeSinceLastInjury;

}

