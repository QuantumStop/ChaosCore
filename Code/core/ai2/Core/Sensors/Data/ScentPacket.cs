namespace Core.AI;

public struct ScentPacket : ISensorPacket
{
	public AIController Owner { get; set; }
	public List<DetectedScent> DetectedScents;

	public Vector3 StrongestDirection; // gradient direction toward the strongest currently-detected scent
	public bool AnyDetected;
}
