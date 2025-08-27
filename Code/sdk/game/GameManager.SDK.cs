namespace SDK;
using Core;
[Title( "SDK Game Manager" )]
public class SDKGameManager : GameManager
{
	protected override void SetInstanceThis() { Instance = this; } // not sure this does anything but its fine whatever
	protected override void DecideGameRules() { Rules = new SDKRules(); }
}
