using Sandbox.Internal;

namespace SDK;

public partial class Player
{
	[ConCmd("ch_createbattery", ConVarFlags.Cheat)]
	static private void CreateBattery()
	{
		CreateEntity( "item_battery" );
	}

	[ConCmd( "ch_createhealthkit", ConVarFlags.Cheat )]
	static private void CreateHealthkit()
	{
		CreateEntity( "item_healthkit" );
	}
}
