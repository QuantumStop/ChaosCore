#if FMOD
using FMODSbox;
#endif
namespace Core;

partial class BasePlayer
{
	[ConCmd( "give", ConVarFlags.Cheat, Help = "Give the player a certain item like a weapon or a healthkit" )]
	public static void CmdGive( string name, string parameter = null )
	{
		if ( name.StartsWith( "weapon" ) )
		{
			Local?.GiveWeaponItemByName( name, parameter );
		}
		else if ( name.StartsWith( "ammo" ) )
		{
			var ammoInfo = AmmoInfo.GetAmmoData( name );

			if ( !ammoInfo.IsValid() )
			{
				Log.Warning( $"Ammo type '{name}' is not valid." );
				return;
			}

			int amount = ammoInfo.DefaultAmmo;

			Local?.AddReserveAmmo( name, amount );
			return;
		}
		else if ( name.StartsWith( "item" ) )
		{
			Local?.GiveItemByName( name );
		}
		else
		{
			Log.Info( "'" + name + "' is not a recognized weapon or item" );
		}
	}
	[ConCmd( "givecurrentammo", Help = "Fill ammo for the currently held gun." )]
	public static void CmdGiveCurrentAmmo()
	{
		if ( !Local.CurrentWeapon.IsValid() )
			return;

		Local.AddReserveAmmo( Local.CurrentWeapon.GetPrimaryAmmoType(), 1000000000 );
#if FMOD
		FMODSound.Play( "event:/Common/AmmoPickup" );
#else
		Local.PlayPickupSteal( "ammo_pickup", 0, Local.WorldPosition );
#endif
	}
	[ConCmd( "remove", Help = "Alias for take, does the same." )] public static void CmdRemove( string name, string parameter = null ) { CmdTake( name, parameter ); }
	[ConCmd( "take", Help = "Remove ammo or an item from your inventory." )]
	public static void CmdTake( string name, string parameter = null )
	{
		if ( name.StartsWith( "weapon" ) )
		{
			Local.RemoveWeapon( name );
		}
		else if ( name.StartsWith( "ammo" ) )
		{
			var ammoinfo = AmmoInfo.GetAmmoData( name );

			if ( !ammoinfo.IsValid() )
				return;

			Local.RemoveReserveAmmo( name.Split( " " )[0], parameter.ToInt( ammoinfo.DefaultAmmo ) );
		}
		else if ( name.StartsWith( "item" ) )
		{
			Local.RemoveItemByName( name );
		}
		else
		{
			Log.Info( "'" + name + "' is not a recognized weapon or item" );
		}
	}
}
