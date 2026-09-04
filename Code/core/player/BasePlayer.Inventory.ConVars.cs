#if FMOD
using FMODSbox;
#endif
namespace Core;

partial class BasePlayer
{
	/// <summary>
	/// The give console command
	/// </summary>
	/// <param name="name">Name of thing to give</param>
	/// <param name="parameter">Additional parameters</param>
	[ConCmd( "give", ConVarFlags.Cheat, Help = "Give the player a certain item like a weapon or a healthkit" )]
	public static void CmdGive( string name, string parameter = null )
	{
		if ( name.StartsWith( "weapon" ) )
		{
			Local?.GiveItemWeaponByName( name, parameter );
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
	/// <summary>
	/// Fill the ammo for current gun
	/// </summary>
	[ConCmd( "givecurrentammo" )]
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
	/// <summary>
	/// Alias for take
	/// </summary>
	[ConCmd( "remove" )] public static void CmdRemove( string name, string parameter = null ) { CmdTake( name, parameter ); }
	/// <summary>
	/// Remove ammo or item
	/// </summary>
	/// <param name="name">Name of thing</param>
	/// <param name="parameter"></param>
	[ConCmd( "take" )]
	public static void CmdTake( string name, string parameter = null )
	{
		if ( name.StartsWith( "weapon" ) )
		{
			Local.RemoveItemWeaponByName( name );
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
