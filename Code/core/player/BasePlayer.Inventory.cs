using Core;
using Sandbox.Internal;
using System;

public class PlayerAmmoReserve : Component
{
	[Property, ReadOnly] public string AmmoType { get; set; }
	[Property, ReadOnly] public int ReserveAmmo { get; set; } = 0;
}
public partial class BasePlayer
{
	// It is probably better to store inventory shit on the player itself, thats how S1 does it
	// the original inventory had gameobject->list conversion but why not do it straight up
	// I keep saying "How S1 does it" not because we are going for full recreation but because it's a game that has shipped
	/// <summary>
	/// List of all weapons on the player
	/// </summary>
	[Property, ReadOnly, Feature( "Weapons" )] public List<BaseCombatWeapon> WeaponList = new List<BaseCombatWeapon>();
	/// <summary>
	/// List of all reserve ammo components on the player
	/// </summary>
	[Property, ReadOnly, Feature( "Weapons" )] public List<PlayerAmmoReserve> AmmoReserveList = new List<PlayerAmmoReserve>();
	/// <summary>
	/// Viewmodel GameObject
	/// </summary>
	[Property, Feature( "Weapons" ), Header( "GameObjects" )] public GameObject WeaponGameObject => Local?.ViewmodelWeaponObject;
	/// <summary>
	/// Current weapon
	/// </summary>
	[Property, Feature( "Weapons" ), Header( "Weapon States" ), ReadOnly, Change( nameof( OnCurrentWeaponChange ) )] public BaseCombatWeapon CurrentWeapon { get; private set; }
	/// <summary>
	/// Current weapon crosshair
	/// </summary>
	[Property, Feature( "Weapons" ), Header( "Weapon States" ), ReadOnly, Change( nameof( OnCurrentWeaponChange ) )] public Crosshair CurrentCrosshair { get; private set; }
	/// <summary>
	/// Weapon soon to be equipped
	/// </summary>
	[Property, Feature( "Weapons" ), ReadOnly] public BaseCombatWeapon WeaponToEquip { get; set; }

	private GameObject _muzzleattachment => Local?.ViewmodelWeapon?.GetAttachmentObject( "muzzle" );
	/*
		public List<BaseCombatWeapon> GetOrderedWeaponList()
		{
			WeaponList.Sort( delegate ( BaseCombatWeapon a, BaseCombatWeapon b )
			{
				if ( a.WeaponData.Bucket == b.WeaponData.Bucket )
				{
					if ( a.WeaponData.BucketPosition == b.WeaponData.BucketPosition ) return 0;
					else if ( a.WeaponData.BucketPosition == b.WeaponData.BucketPosition ) return 1;
					else return -1;
				}
				else if ( a.WeaponData.Bucket > b.WeaponData.Bucket ) return 1;
				else return -1;
			} );

			return WeaponList;
		}
	*/
	/// <summary>
	/// Weapon list sorting related thingies for weapon selection
	/// </summary>
	/// <returns>Sorted weapons</returns>
	public List<BaseCombatWeapon> GetSortedWeaponsByBucket( int bucket )
	{
		var weapons = new List<BaseCombatWeapon>();
		foreach ( var weapon in WeaponList )
		{
			if ( weapon.WeaponData.Bucket == bucket )
				weapons.Add( weapon );
		}
		weapons.Sort( delegate ( BaseCombatWeapon a, BaseCombatWeapon b )
		{
			if ( a.WeaponData.BucketPosition == b.WeaponData.BucketPosition ) return 0;
			else if ( a.WeaponData.BucketPosition > b.WeaponData.BucketPosition ) return 1;
			else return -1;
		} );
		return weapons;
	}
	/// <summary>
	/// Weapon list sorting related thingies for weapon selection
	/// </summary>
	/// <returns>Sorted weapons</returns>
	public List<List<BaseCombatWeapon>> GetAllSortedBuckets()
	{
		var buckets = new List<List<BaseCombatWeapon>>();
		var weaponbuckets = new List<int>();
		foreach ( var weapon in WeaponList )
		{
			if ( !weaponbuckets.Contains( weapon.WeaponData.Bucket ) )
			{
				weaponbuckets.Add( weapon.WeaponData.Bucket );
				buckets.Add( GetSortedWeaponsByBucket( weapon.WeaponData.Bucket ) );
			}
		}
		return buckets;
	}
	/// <summary>
	/// Current weapon has changed, handle the swap
	/// </summary>
	private void OnCurrentWeaponChange()
	{
		Log.Info( $"Changing weapon to {CurrentWeapon?.WeaponData?.Name ?? "Null"}" );
		Local.SetAllWeaponModels( CurrentWeapon?.WeaponData.WeaponViewmodel );

		if ( _muzzleattachment.IsValid() ) CurrentWeapon?.ClearEffects( _muzzleattachment );
	}

	/// <summary>
	/// Hide/Show Weapon GO so you dont see a big cube all the time
	/// </summary>
	/// <param name="which"></param>
	public void ToggleViewmodelObj( bool which )
	{
		WeaponGameObject.Enabled = which;
	}
	/// <summary>
	/// unimplemented
	/// </summary>
	public bool HolsterWeapon()
	{
		return false; // true;
	}
	/// <summary>
	/// unimplemented
	/// </summary>
	public bool UnholsterWeapon()
	{
		// TODO
		return false; // true;
	}

	/// <summary>
	/// Add AND equip a weapon
	/// </summary>
	/// <param name="name">Weapon name</param>
	/// <param name="param">Additional params</param>
	/// <param name="switchto">Switch to that weapon?</param>
	/// <param name="item">Optional weapon item</param>
	public BaseCombatWeapon GiveWeaponByName( string name, string param, bool switchto = true, BaseWeaponItem item = null )
	{
		var data = WeaponParse.GetWeaponData( name );

		if ( data == null )
			return null;

		if ( !WeaponGameObject.IsValid() )
			return null;

		// Check if the weapon is already in the inventory
		foreach ( var weapon in WeaponList )
		{
			if ( weapon.WeaponData.ResourceName == data.ResourceName )
			{
				Log.Info( "Already have this weapon" );

				// i dont think we will be expecting to fill secondary ammo from a weapon pickup, 
				// so don't support that (it's ass to do so anyway) - just pick them up as BaseAmmoItem
				var ammoname = data.PrimaryAmmoType.ResourceName;

				// only do this if we are less than max carry for this ammo
				if ( GetReserveAmmo( ammoname ) < AmmoInfo.GetAmmoData( ammoname ).MaxAmmo )
				{
					if ( item != null )
					{
						// in most cases you just take the whole mag and disappear the gun, but its nice to handle this specific edge case
						var reserve_counter = Math.Clamp( AmmoInfo.GetAmmoData( ammoname ).MaxAmmo - GetReserveAmmo( ammoname ), 0, AmmoInfo.GetAmmoData( ammoname ).MaxAmmo ); // 90 - 85 = 5 ammo needed to fill
						var ammocount = Math.Clamp( reserve_counter, 0, data.PrimaryAmmoCapacity );
						//	Log.Info( "count reserve: " + reserve_counter );
						//	Log.Info( "add reserve: " + ammocount );

						AddReserveAmmo( ammoname, ammocount );
						Sound.Play( "ammo_pickup" ).ListenLocal = true;

						item.DecreaseInternalMag( ammocount );

						if ( item.InternalAmmoCountPrimary <= 0 ) // the gun was emptied
							item.DestroyItem();
					}
				}
				return null;
			}
		}

		// it is important to send this to the correct viewmodel game object
		var wpnobj = WeaponGameObject.Components.Create( GlobalGameNamespace.TypeLibrary.GetType( data.ResourceName.ToString().ToLower() ) ) as BaseCombatWeapon;
		wpnobj.WeaponData = data;

		if ( param == "nofirstequip" || param == "noeq" )
			wpnobj.FirstEquip = false;

		WeaponList.Add( wpnobj );
		if ( switchto ) SwitchToWeapon( wpnobj );

		return wpnobj;
	}

	/// <summary>
	/// Switch to specified weapon
	/// </summary>
	/// <param name="weapon">Weapon in question</param>
	public void SwitchToWeapon( BaseCombatWeapon weapon )
	{
		if ( CurrentWeapon == weapon && CurrentWeapon != null && CurrentWeapon.Equipped )
			return;

		Local.ToggleViewmodelObj( true );

		if ( CurrentWeapon != null )    // dont try playing the anim if this is the first ever weapon (this might be not needed because of the above check or because of the ?)
		{
			CurrentWeapon?.Holster();
		}

		CurrentWeapon = weapon;
		CurrentWeapon?.Draw();

		WeaponGameObject.Name = "Viewmodel " + "(" + weapon.WeaponData.Name + ")";
	}

	/// <summary>
	/// Handle the additional inventory stuff every frame, currently useless
	/// </summary>
	private void HandleWeaponInventory()
	{
		// I assume this was so you can have delayed weapon switching for holster animations? - Xenthio
		if ( WeaponToEquip != null )
		{
			SwitchToWeapon( WeaponToEquip );
			WeaponToEquip = null;
		}

		if ( Input.Pressed( "holster" ) )
		{
			if ( !UnholsterWeapon() )
				HolsterWeapon();
		}

		//if ( Input.MouseWheel.y < 0 )
		//	CycleNextWeapon();

		//if ( Input.MouseWheel.y > 0 )
		//	CyclePrevWeapon();
	}

	/// <summary>
	/// Get weapon by its name
	/// </summary>
	/// <param name="name">Name of the weapon</param>
	/// <returns></returns>
	public BaseCombatWeapon GetWeaponByName( string name )
	{
		foreach ( var weapon in WeaponList )
			if ( name == weapon.GetType().Name ) return weapon;

		return null;
	}

	/// <summary>
	/// The give console command
	/// </summary>
	/// <param name="name">Name of thing to give</param>
	/// <param name="parameter">Additional parameters</param>
	[ConCmd( "give" )]
	public static void CmdGive( string name, string parameter = null )
	{
		var weapon = Local?.CurrentWeapon;

		if ( name.StartsWith( "weapon" ) )
		{
			Local?.GiveWeaponByName( name, parameter );
		}
		else if ( name.StartsWith( "ammo" ) )
		{
			var ammoinfo = AmmoInfo.GetAmmoData( name );

			if ( !ammoinfo.IsValid )
				return;

			Local.AddReserveAmmo( weapon.GetPrimaryAmmoType(), weapon.GetPrimaryDefault() );
		}
		else if ( name.StartsWith( "item" ) )
		{
			Local.GiveItemByName( name );
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
		if ( Local.CurrentWeapon == null )
			return;

		Local.AddReserveAmmo( Local.CurrentWeapon.GetPrimaryAmmoType(), 1000000000 );
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
			var weapon = Local.GetWeaponByName( name );

			if ( weapon != null && weapon.IsValid )
				weapon.Destroy();
		}
		else if ( name.StartsWith( "ammo" ) )
		{
			var ammoinfo = AmmoInfo.GetAmmoData( name );

			if ( !ammoinfo.IsValid )
				return;

			//			Local.RemoveReserveAmmo( name.Split( " " )[0], parameter.ToInt( ammoinfo.DefaultQuantity ) );
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

	public int AddReserveAmmo( string ammoname, int amount, bool onlycheck = false )
	{
		//		returns amount left over if not all ammo can fit
		if ( amount <= 0 )
			return 0;

		var component = GetReserveAmmoComponent( ammoname );
		if ( component == null )
		{
			component = GameObject.Components.Create<PlayerAmmoReserve>();
			component.AmmoType = ammoname;
		}

		if ( !onlycheck )
			component.ReserveAmmo += amount;

		var overflow = component.ReserveAmmo - Math.Clamp( component.ReserveAmmo + (onlycheck ? amount : 0), 0, AmmoInfo.GetAmmoData( ammoname ).MaxAmmo );

		if ( !onlycheck )
			component.ReserveAmmo -= overflow;

		return overflow;
	}

	public PlayerAmmoReserve GetReserveAmmoComponent( string ammoname )
	{
		foreach ( var ammoReserve in GameObject.Components.GetAll<PlayerAmmoReserve>() )
			if ( ammoReserve.AmmoType == ammoname ) return ammoReserve;

		return null;
	}

	public int GetReserveAmmo( string ammoname )
	{
		var component = GetReserveAmmoComponent( ammoname );
		if ( component != null )
			return component.ReserveAmmo;

		return 0;
	}

	public void RemoveReserveAmmo( string ammoname, int amount )
	{
		var component = GetReserveAmmoComponent( ammoname );
		if ( component != null )
			component.ReserveAmmo -= amount;
	}

	public void GiveItemByName( string name )
	{
		var typedesc = GlobalGameNamespace.TypeLibrary.GetType( name );

		if ( typedesc != null )
		{
			var itemobj = Scene.CreateObject();
			itemobj.Name = name.ToUpper();
			itemobj.WorldPosition = WorldPosition - Vector3.Up * 10f;
			itemobj.Components.Create( typedesc );
			itemobj.Components.Get<BaseItem>()?.OnPickup();
		}
	}

	public void RemoveItemByName( string name )
	{
		var typedesc = GlobalGameNamespace.TypeLibrary.GetType( name );

		if ( typedesc != null )
		{
			var itemobj = Scene.CreateObject();
			itemobj.Name = name.ToUpper();
			itemobj.WorldPosition = WorldPosition - Vector3.Up * 10f + WorldRotation.Forward * 100f;
			itemobj.Components.Create( typedesc );
			itemobj.Components.Get<BaseItem>()?.OnRemove();
		}
	}
}
