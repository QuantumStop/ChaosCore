using System.Diagnostics;

namespace Core;

#if FMOD
using FMODSbox;
#endif
using Sandbox.Internal;
using System;

public partial class BasePlayer
{
	private static string GetWeaponTypeLibraryKey( WeaponParse data )
	{
		if ( data is null )
			return null;

		return data.ResourceName?.ToString()?.ToLowerInvariant();
	}

	private static TypeDescription ResolveWeaponTypeDescription( WeaponParse data )
	{
		var key = GetWeaponTypeLibraryKey( data );
		if ( string.IsNullOrWhiteSpace( key ) )
			return null;

		return GlobalGameNamespace.TypeLibrary.GetType( key );
	}

	private static bool TryResolveWeaponDataAndType( string name, out WeaponParse data, out TypeDescription type )
	{
		data = WeaponParse.GetWeaponData( name );
		type = ResolveWeaponTypeDescription( data );

		return data.IsValid() && type is not null;
	}

	// It is probably better to store inventory shit on the player itself, thats how S1 does it
	// the original inventory had gameobject->list conversion but why not do it straight up
	// I keep saying "How S1 does it" not because we are going for full recreation but because it's a game that has shipped
	/// <summary>
	/// List of all weapons on the player
	/// </summary>
	[Property, ReadOnly, Feature( "Weapons" )] public List<BaseCombatWeapon> WeaponList = [];

	/// <summary>
	/// List of all reserve ammo components on the player
	/// </summary>
	[Property, ReadOnly, Feature( "Weapons" ), InlineEditor] public List<PlayerAmmoReserve> AmmoReserveList = [];
	/// <summary>
	/// Viewmodel GameObject
	/// </summary>
	[Property, Feature( "Weapons" ), Header( "GameObjects" )] public GameObject WeaponGameObject => ViewmodelWeaponObject;
	/// <summary>
	/// Current weapon
	/// </summary>
	[Property, Feature( "Weapons" ), Header( "Weapon States" ), ReadOnly]
	public BaseCombatWeapon CurrentWeapon
	{
		get;
		protected set
		{
			if ( field != value )
			{
				field = value;
				OnCurrentWeaponChange();
			}
		}
	}
	/// <summary>
	/// Current weapon crosshair
	/// </summary>
	[Property, Feature( "Weapons" ), Header( "Weapon States" ), ReadOnly]
	public Crosshair CurrentCrosshair
	{
		get;
		protected set
		{
			if ( field != value )
			{
				field = value;
				OnCurrentWeaponChange();
			}
		}
	}
	/// <summary>
	/// Weapon soon to be equipped
	/// </summary>
	[Property, Feature( "Weapons" ), ReadOnly]
	public BaseCombatWeapon WeaponToEquip
	{
		get;
		protected set
		{
			if ( field != value )
			{
				field = value;

				if ( value.IsValid() && value != CurrentWeapon )
					CurrentWeapon?.Holster();
			}
		}
	}

	// --- Weapon HUD/Selection ---
	[ConVar( "hud_showemptyweaponslots", Help = "Show empty weapon slots in selection HUD." )] public static bool HudShowEmptyWeaponSlots { get; set; } = false;
	[ConVar( "hud_weaponselection_debug", Help = "Debug Weapon Selection" )] public static bool DebugWeaponSelection { get; set; } = false;

	[Property, ReadOnly, Feature( "Inventory" )] public int SelectedBucket { get; protected set; } = -1;
	[Property, ReadOnly, Feature( "Inventory" )] public int PositionInSelectedBucket { get; protected set; } = 0;
	[Property, ReadOnly, Feature( "Inventory" )] public TimeSince TimeSinceLastWeaponSelect { get; protected set; }

	[Property, ReadOnly, Feature( "Inventory" )]
	public bool ShouldDrawWeaponHUD => !IsHUDElementHidden( HIDEHUD_FLAGS.HIDEHUD_PLAYERDEAD | HIDEHUD_FLAGS.HIDEHUD_WEAPONSELECTION ) && SelectedBucket != -1;

	[Property, ReadOnly, Feature( "Inventory" )] protected List<List<BaseCombatWeapon>> _cachedBuckets = [];
	[Property, ReadOnly, Feature( "Inventory" )] protected int _lastInventoryHash = 0;

	private GameObject _muzzleattachment => ViewmodelWeapon?.GetAttachmentObject( "muzzle" );

	/// <summary>
	/// Can the model be changed when we are switching, or something else is using the model
	/// </summary>
	[Property, Hide] public bool AllowWeaponModelChange { get; set; } = true;

	/// <summary>
	/// Current weapon has changed, handle the swap
	/// </summary>
	private void OnCurrentWeaponChange()
	{
		if ( AllowWeaponModelChange )
			SetAllWeaponModels( CurrentWeapon.IsValid() ? CurrentWeapon.WeaponData?.WeaponViewmodel : null );

		if ( _muzzleattachment.IsValid() ) CurrentWeapon?.ClearEffects( _muzzleattachment );
	}

	/// <summary>
	/// Force ownership on evey child BaseCombatWeapon to be Player's
	/// </summary>
	public void RestoreWeaponOwnership()
	{
		foreach ( var weapon in GetComponentsInChildren<BaseCombatWeapon>( true ) )
		{
			if ( !weapon.IsValid() )
				continue;

			weapon.Owner = new BaseCombatWeapon.WeaponOwner( this );
		}
	}

	/// <summary>
	/// Force a weapon model refresh back to the one we are holding
	/// </summary>
	public void ForceWeaponChange() => OnCurrentWeaponChange();

	/// <summary>
	/// Add AND equip a weapon
	/// </summary>
	/// <param name="name">Weapon name</param>
	/// <param name="param">Additional params</param>
	/// <param name="switchto">Switch to that weapon?</param>
	/// <param name="item">Optional weapon item</param>
	public BaseCombatWeapon GiveWeaponByName( string name, string param, bool switchto = true, BaseWeaponItem item = null )
	{
		if ( !TryResolveWeaponDataAndType( name, out var data, out var weaponType ) )
			return null;

		if ( !WeaponGameObject.IsValid() )
			return null;

		var weaponSnapshot = new BaseCombatWeapon[WeaponList.Count];
		WeaponList.CopyTo( weaponSnapshot );

		// Check if the weapon is already in the inventory
		foreach ( var weapon in weaponSnapshot )
		{
			if ( weapon?.WeaponData?.ResourceName == data?.ResourceName )
			{
				// i dont think we will be expecting to fill secondary ammo from a weapon pickup, 
				// so don't support that (it's ass to do so anyway) - just pick them up as BaseAmmoItem
				if ( weapon.WeaponData.HasPrimaryAmmoType )
				{
					var ammoname = data?.PrimaryAmmoType.ResourceName;

					// only do this if we are less than max carry for this ammo
					if ( GetReserveAmmo( ammoname ) < AmmoInfo.GetAmmoData( ammoname ).MaxAmmo )
					{
						if ( item.IsValid() )
						{
							// in most cases you just take the whole mag and disappear the gun, but its nice to handle this specific edge case
							var reserve_counter = Math.Clamp( AmmoInfo.GetAmmoData( ammoname ).MaxAmmo - GetReserveAmmo( ammoname ), 0, AmmoInfo.GetAmmoData( ammoname ).MaxAmmo ); // 90 - 85 = 5 ammo needed to fill
							var ammocount = Math.Clamp( reserve_counter, 0, data.PrimaryAmmoCapacity );
							//	Log.Info( "count reserve: " + reserve_counter );
							//	Log.Info( "add reserve: " + ammocount );

							AddReserveAmmo( ammoname, ammocount );
#if FMOD
							FMODSound.Play( "event:/Common/AmmoPickup" );
#else
							Local.PlayPickupSteal( "ammo_pickup", 0, WorldPosition );
#endif
							item.DecreaseInternalMag( ammocount );

							if ( item.InternalAmmoCountPrimary <= 0 ) // the gun was emptied
								item.DestroyItem();
						}
					}
				}
				return null;
			}
			if ( (weapon?.WeaponData?.Bucket == data?.Bucket) && (weapon?.WeaponData?.Position == data?.Position) )
			{
				DropWeapon( weapon );
			}
		}

		// it is important to send this to the correct viewmodel game object
		var wpnobj = WeaponGameObject.Components.Create( weaponType, false ) as BaseCombatWeapon;
		wpnobj.WeaponData = data;

		if ( param == "nofirstequip" || param == "noeq" )
			wpnobj.FirstEquip = false;

		WeaponList.Add( wpnobj );
		if ( switchto ) SwitchToWeapon( wpnobj );

		return wpnobj;
	}

	[ConVar( "debug_weapon_drop_ray" )] public static bool DebugDropRay { get; set; } = false;

	public void DropWeapon( BaseCombatWeapon weapon, bool switchtonew = true )
	{
		GameObject WeaponObject = Scene.CreateObject();
		WeaponObject.Name = weapon.WeaponData.ResourceName + " (" + weapon.WeaponData.Name + ")";

		var tr = Scene.Trace.Ray( Controller.AimRay, 48 )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger" )
			.UsePhysicsWorld()
			.Run();

		WeaponObject.WorldPosition = tr.EndPosition;

		if ( DebugDropRay )
			DebugOverlay.Trace( tr, 15, true );

		var item = WeaponObject.Components.Create<BaseWeaponItem>( false );
		item.Data = weapon.WeaponData;
		item.InternalAmmoCountPrimary = weapon.PrimaryAmmoLoaded;
		item.WasDropped = true;
		item.Enabled = true;

		item.PositionImpulse = Controller.Head.LocalRotation.Angles().WithPitch( -25 ).Forward * 250 * item.Physics.Mass;

		RemoveWeapon( weapon, switchtonew );
		if ( !switchtonew ) CurrentWeapon = null;
	}

	/// <summary>
	/// Switch to specified weapon
	/// </summary>
	/// <param name="weapon">Weapon in question</param>
	virtual protected void WeaponSwitch( BaseCombatWeapon weapon )
	{
		if ( CurrentWeapon.IsValid() && CurrentWeapon == weapon )
			return;

		if ( DebugWantHolster ) CurrentWeapon?.Holster(); // call holster anyway since it disables the component, we never see it

		CurrentWeapon = weapon;
		ViewmodelVisible = true;

		CurrentWeapon?.Draw();

		WeaponGameObject.Name = "Viewmodel " + "(" + weapon.WeaponData.Name + ")";

	}
	/// <summary>
	/// Skip holster delay when switching weapons.
	/// </summary>
	[ConVar( "debug_holster_switch", ConVarFlags.Cheat, Help = "Skip holster delay when switching weapons." )] public static bool DebugWantHolster { get; set; } = false;

	/// <summary>
	/// Public accessor to WeaponSwitch, which also decides if we want the holster delay or not
	/// </summary>
	/// <param name="weapon"></param>
	public void SwitchToWeapon( BaseCombatWeapon weapon )
	{
		if ( !DebugWantHolster ) WeaponToEquip = weapon;
		else WeaponSwitch( weapon );
	}

	/// <summary>
	/// Handle the additional inventory stuff every frame, like checking the holster thing
	/// </summary>
	private void HandleWeaponInventory()
	{
		// I assume this was so you can have delayed weapon switching for holster animations? - Xenthio
		// Correct! - answer
		if ( WeaponToEquip.IsValid() )
		{
			if ( CurrentWeapon.IsValid() && WeaponToEquip == CurrentWeapon )
			{
				WeaponToEquip = null; // just reset it back and ignore the rest, this CAN happen
				return;
			}

			//	CurrentWeapon?.Holster();

			// being in a reload skips the holster, or it will be waiting for it to finish first,
			// which defeats the purpose for having staged reloads at all and also is annoying
			// In an ideal world the animgraph could be redone to support transitioning into the holster,
			// but this is easier and you probably want faster switching for this anyway
			// Also so much fucking support for weapons with no viewmodel which is really a dev only thing
			if ( !CurrentWeapon.IsValid() ||
				(CurrentWeapon.IsValid() && !CurrentWeapon.WeaponData.WeaponViewmodel.IsValid()) ||
				(CurrentWeapon.IsValid() && (CurrentWeapon.IsHolstered ^ CurrentWeapon.IsReloading)) )
			{
				// the only time we want to call the internal function directly, or we will be stuck in a loop
				// where SwitchToWeapon will be calling this fucking thing over and over
				WeaponSwitch( WeaponToEquip );
				WeaponToEquip = null;
			}
		}
	}

	/// <summary>
	/// We want this called once, but only switching is able to be calledo once due to 50 conditions, so we have to force this be called once
	/// </summary>
	private void WeaponToEquipChange()
	{
		if ( WeaponToEquip.IsValid() && WeaponToEquip != CurrentWeapon )
			CurrentWeapon?.Holster();
	}

	/// <summary>
	/// Get the weapon by its name in the player's weapon list
	/// </summary>
	/// <param name="name">Name of the weapon</param>
	/// <returns></returns>
	public BaseCombatWeapon GetWeaponByName( string name )
	{
		foreach ( var weapon in WeaponList )
			if ( name.Equals( weapon.GetType().Name, StringComparison.OrdinalIgnoreCase ) ) return weapon;

		return null;
	}

	public void GiveWeaponItemByName( string name, string param )
	{
		if ( !string.IsNullOrEmpty( name ) )
		{
			if ( !ResourceLibrary.TryGet<WeaponParse>( "scripts/weapons/" + name + ".wpn", out var gun ) )
				return;

			var itemobj = Scene.CreateObject();
			itemobj.Name = name.ToLowerInvariant();
			itemobj.WorldPosition = WorldPosition + Vector3.Up * 10f;
			var weapon = itemobj.Components.Create<BaseWeaponItem>( false );
			weapon.Data = gun;

			if ( param == "nofirstequip" || param == "noeq" )
				weapon.SkipFirstEquipAnim = true;

			weapon.PickUp = true;
			weapon.LastOwner = this;
			weapon.Enabled = true;
		}
	}

	/// <summary>
	/// Remove a given weapon, if we have it
	/// </summary>
	/// <param name="name">Full Type name of the weapon</param>
	/// <param name="switchtonew">Do we switch to the new weapon?</param>
	public void RemoveWeaponByName( string name, bool switchtonew = true ) => RemoveWeapon( GetWeaponByName( name ), switchtonew );

	/// <summary>
	/// Remove a given weapon, if we have it
	/// </summary>
	/// <param name="weapon">BaseCombatWeapon of the weapon</param>
	/// <param name="switchtonew">Do we switch to a weapon we have left?</param>
	public void RemoveWeapon( BaseCombatWeapon weapon, bool switchtonew = true )
	{
		if ( !weapon.IsValid() ) return;

		if ( CurrentWeapon == weapon && WeaponList.Count > 0 ) // if we are removing the weapon we are currently holding
		{
			var index = WeaponList.IndexOf( CurrentWeapon );
			WeaponList.Remove( weapon );
			if ( switchtonew ) SwitchToWeapon( BestNextWeapon( weapon ) );
		}
		else
		{
			WeaponList.Remove( weapon ); // just remove it and nothing else
		}

		if ( weapon.IsValid() )
			weapon.Destroy();
	}

	/// <summary>
	/// Find best weapon besides current one, in case we need to switch away
	/// </summary>
	/// <returns>New weapon</returns>
	public static BaseCombatWeapon BestNextWeapon( BaseCombatWeapon oldWeapon )
	{
		int bestWeight = -1;
		BaseCombatWeapon bestWeapon = oldWeapon;

		foreach ( var weapon in Local.WeaponList )
		{
			if ( !weapon.IsValid() ) continue;
			if ( weapon == oldWeapon ) continue; // somehow ended up with a not cleared list
			if ( weapon.WeaponData.Weight <= bestWeight ) continue; // "worse" than our current gun
			if ( !weapon.HasUsableAmmo() ) continue; // has to have any ammo, this checks both primary and secondary which is probably bad

			bestWeight = weapon.WeaponData.Weight;
			bestWeapon = weapon;
		}

		return bestWeapon; // if we only have one weapon either itself is the best or none (if the list is cleared)
	}

	public int AddReserveAmmo( string ammoname, int amount, bool onlycheck = false )
	{
		//		returns amount left over if not all ammo can fit
		if ( amount <= 0 )
			return 0;

		var ammoRef = GetReserveAmmoReference( ammoname );
		if ( ammoRef is null )
		{
			ammoRef = new PlayerAmmoReserve { AmmoType = ammoname };
			AmmoReserveList.Add( ammoRef );
		}

		if ( !onlycheck )
			ammoRef.ReserveAmmo += amount;

		var overflow = ammoRef.ReserveAmmo - Math.Clamp( ammoRef.ReserveAmmo + (onlycheck ? amount : 0), 0, AmmoInfo.GetAmmoData( ammoname ).MaxAmmo );

		if ( !onlycheck )
			ammoRef.ReserveAmmo -= overflow;

		return overflow;
	}

	public PlayerAmmoReserve GetReserveAmmoReference( string ammoname )
	{
		foreach ( var ammoReserve in AmmoReserveList )
			if ( ammoReserve.AmmoType == ammoname )
				return ammoReserve;

		return null;
	}

	public int GetReserveAmmo( string ammoname )
	{
		var ammo = GetReserveAmmoReference( ammoname );
		if ( ammo is not null )
			return ammo.ReserveAmmo;

		return 0;
	}

	public void RemoveReserveAmmo( string ammoname, int amount )
	{
		var ammo = GetReserveAmmoReference( ammoname );
		ammo?.ReserveAmmo -= amount;
	}

	public void GiveItemByName( string name )
	{
		var typedesc = GlobalGameNamespace.TypeLibrary.GetType( name );

		if ( typedesc is not null )
		{
			var itemobj = Scene.CreateObject();
			itemobj.Name = name.ToLowerInvariant();
			itemobj.WorldPosition = WorldPosition + Vector3.Up * 10f;
			var item = itemobj.Components.Create( typedesc, false );
			(item as BaseItem).LastOwner = this; // probably bad
			(item as BaseItem).PickUp = true; // probably bad
			item.Enabled = true;
		}
	}

	public void RemoveItemByName( string name )
	{
		var typedesc = GlobalGameNamespace.TypeLibrary.GetType( name );

		if ( typedesc is not null )
		{
			var itemobj = Scene.CreateObject();
			itemobj.Name = name.ToLowerInvariant();
			itemobj.WorldPosition = WorldPosition - Vector3.Up * 10f + WorldRotation.Forward * 100f;
			itemobj.Components.Create( typedesc );
			itemobj.Components.Get<BaseItem>()?.OnRemove();
		}
	}

	public class PlayerAmmoReserve
	{
		/// <summary>
		/// The (entity) name of this ammo
		/// </summary>
		[Property] public string AmmoType { get; set; }
		/// <summary>
		/// The amount of this ammo
		/// </summary>
		[Property] public int ReserveAmmo { get; set; }

		public override string ToString() => AmmoType;
	}
}
