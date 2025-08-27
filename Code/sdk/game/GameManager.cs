namespace SDK;
using Core;
[Title( "SDK Game Manager" )]
public class SDKGameManager : GameManager
{
	protected override void DecideGameRules() { Rules = new SDKRules(); }
}
