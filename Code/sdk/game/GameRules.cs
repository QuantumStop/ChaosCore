using Core;
namespace SDK;

public class SDKRules : MultiplayerRules
{
	public override void GameStart() => Networking.CreateLobby( new() );
}
