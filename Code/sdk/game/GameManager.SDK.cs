namespace SDK;
using Core;

public class SDKGameManager : GameManager
{
	protected override void SetInstanceThis() { Instance = this; }
	protected override void DecideGameRules() { Rules = new SDKRules(); }
}
