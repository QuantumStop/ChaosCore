namespace Core.AI;

public struct TouchPacket : ISensorPacket
{
	public AIController Owner { get; set; }
	public bool touchingPlayer, touchingEnemy, touchingFriend;
}
