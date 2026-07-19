namespace Core.AI;

public abstract class BaseSensor<TPacket>
	where TPacket : struct, ISensorPacket
{
	protected AIController Agent;

	public TPacket Packet;

	protected BaseSensor( AIController agent )
	{
		Agent = agent;
	}

	public abstract void UpdatePacket();
}
