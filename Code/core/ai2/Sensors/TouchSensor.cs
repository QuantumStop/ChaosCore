namespace Core.AI;

public class TouchSensor : BaseSensor<TouchPacket>
{
	public TouchSensor( AIController agent )
		: base( agent )
	{
	}
	public TouchPacket GetOutputPacketData()
	{
		return Packet;
	}
	public override void UpdatePacket()
	{
		Packet.touchingPlayer = Agent.TouchingPlayer;
		Packet.touchingEnemy = Agent.TouchingEnemy;
		Packet.touchingFriend = Agent.TouchingAlly;
	}
}
