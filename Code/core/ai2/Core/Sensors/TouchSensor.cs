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
		Packet.touchingPlayer = Agent.touchingPlayer;
		Packet.touchingEnemy = Agent.touchingEnemy;
		Packet.touchingFriend = Agent.touchingAlly;
	}
}
