namespace Core.AI;

public struct AudioPacket : ISensorPacket
{
	public AIController Owner { get; set; }
}
