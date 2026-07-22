using Core;
namespace SDK;

public class SDKRulesMP : MultiplayerRules
{
	public override void GameStart() => Networking.CreateLobby( new() );
}

public class SDKRulesSP : SingleplayerRules
{
	public override void GameStart() => SDKGameManager.Current.OnActive( Connection.Local );
}
