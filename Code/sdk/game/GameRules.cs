using Core;
namespace SDK;

public class SDKRulesMP : MultiplayerRules
{
	public override void GameStart() => Networking.CreateLobby( new() );
}

public class SDKRulesSP : SingleplayerRules { }
