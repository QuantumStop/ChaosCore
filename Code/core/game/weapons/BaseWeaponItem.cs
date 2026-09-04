using System;

namespace Core;

[Title( "Weapon Item" )]
public class BaseWeaponItem : BaseItem
{
	public override string ToString() => Data?.Name;
#if IGNIS
	[DebugExpose( group: "BaseWeaponItem", DisplayMember = "ResourcePath" )]
#endif
	[Property, Title( "Weapon Data" )] public WeaponParse Data { get; set; }
#if IGNIS
	[DebugExpose( group: "BaseWeaponItem" )]
#endif
	[Property] public bool SkipFirstEquipAnim { get; set; } = false;
#if IGNIS
	[DebugExpose( group: "BaseWeaponItem" )]
#endif
	[Property] public bool FillReserveAmmoForWeapon { get; set; } = false;
#if IGNIS
	[DebugExpose( group: "BaseWeaponItem" )]
#endif
	[Property, ReadOnly, Feature( "Debug" )] public bool WasDropped { get; set; } = false;
	public Vector3 PositionImpulse { get; set; }
	public Vector3 AngularImpulse { get; set; }

	// no holding ground weapons
	public override bool CanBeHeld => false;

#if IGNIS
	[DebugExpose( group: "BaseWeaponItem" )]
#endif
	[Property, ReadOnly, Feature( "Debug" )] public int InternalAmmoCountPrimary { get; set; } = 1;


	public BaseWeaponItem() => Data = WeaponParse.GetWeaponData( GetType().Name );

	public void DecreaseInternalMag( int howmuch )
	{
		if ( InternalAmmoCountPrimary > 0 ) InternalAmmoCountPrimary -= howmuch;
	}

	protected override void OnValidate()
	{
		if ( GetType().Name != "BaseWeaponItem" && (!Data.IsValid() || GetType().Name != Data.ResourceName) )
			Data = WeaponParse.GetWeaponData( GetType().Name );

		base.OnValidate();
	}

	protected override string GetModel() => Data?.WeaponWorldmodel?.ResourcePath ?? base.GetModel();

	protected override void OnStart()
	{
		base.OnStart();
		Physics?.PhysicsBody.ApplyImpulse( PositionImpulse );
		Physics?.PhysicsBody.ApplyAngularImpulse( AngularImpulse );

		// Log.Info( "Enabled" );

		if ( !Data.IsValid() )
		{
			Log.Warning( "No Weapon Data!" );
			return;
		}

		if ( !WasDropped && Data.HasPrimaryAmmoType && !FillReserveAmmoForWeapon ) InternalAmmoCountPrimary = Data.PrimaryAmmoCapacity;
	}

	public const float PickupTime = 0.3f;

	[Property, ReadOnly, Feature( "Debug" )] private float _counter = 0;
	[Property, ReadOnly, Feature( "Debug" )] private bool _isPressing = false;

	/// <summary>Is this weapon in a slot that's occupied by the person attempting pickup</summary>
	[Property, ReadOnly, Feature( "Debug" )]
	public bool SlotTaken()
	{
		if ( !LastOwner.IsValid() || OverrideSlotOccupancy ) return false;

		// stomp the pickup if slot is occupied, so we can do the Pressing
		foreach ( var weapon in LastOwner.WeaponList )
		{
			if ( (weapon.WeaponData?.Bucket == Data?.Bucket) && (weapon.WeaponData?.Position == Data?.Position) && (weapon.WeaponData != Data) )
			{
				SlotTakenBy = weapon.WeaponData;
				return true;
			}
		}

		return false;
	}

	/// <summary>Who is it taken by</summary>
	[Property, ReadOnly, Feature( "Debug" )] public WeaponParse SlotTakenBy { get; private set; }

	/// <summary>
	/// A hack to force the replacement in cases where we definitely know we want to replace, i.e. external giving (so no player input)
	/// This is rarely used, we should be preferring the default logic (aka false).
	/// </summary>
	public bool OverrideSlotOccupancy { get; set; } = false;

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( Input.Released( "use" ) ) // IPressable Release doesnt work somehow
			_isPressing = false;

		if ( !_isPressing && _counter > 0 )
			_counter -= Time.Delta;

		_counter = Math.Clamp( _counter, 0, PickupTime );
	}

	public override bool Pressing( IPressable.Event press )
	{
		base.Pressing( press );

		press.Source.Components.TryGet<BasePlayer>( out var Activator );
		if ( Activator.LifeState == LifeState.Dead ) return false;

		LastOwner = Activator;

		if ( !SlotTaken() ) return false;

		_isPressing = true;

		if ( _counter < PickupTime )
			_counter += Time.Delta;

		if ( _counter == PickupTime )
		{
			var weapon = Activator.GiveWeaponByName( Data.ResourceName, null, false, this );

			if ( !weapon.IsValid() ) return false;

			base.OnPickup( Activator );

			weapon.Owner = new BaseCombatWeapon.WeaponOwner( Activator );

			weapon.PrimaryAmmoLoaded = InternalAmmoCountPrimary;
			weapon.WasOnTheGround = true;

			Activator.SwitchToWeapon( weapon );
			DestroyItem();
		}

		return true;
	}

	/// <summary>Horrible, horrible hack to know if we need to pre-call FirstSetup() on a given baseweapon</summary>
	public bool NeedsWarmingUp { get; set; } = false;

	public override void OnPickup( BasePlayer Activator = null )
	{
		if ( Activator.LifeState == LifeState.Dead || SlotTaken() ) return; // Ghosts can attempt to pick up items, but we won't let them actually do it

		var weapon = Activator.GiveWeaponByName( Data.ResourceName, null, false, this );

		if ( !weapon.IsValid() )    // if we are just filling ammo this stops it
			return;

		base.OnPickup( Activator ); // putting this here is easier than to figure out the pickup check override

		weapon.Owner = new BaseCombatWeapon.WeaponOwner( Activator );

		if ( SkipFirstEquipAnim ) weapon.FirstEquip = false;

		if ( NeedsWarmingUp ) weapon.ForceFirstSetup();

		if ( weapon.WeaponData.HasPrimaryAmmoType && FillReserveAmmoForWeapon )
			Activator.AddReserveAmmo( weapon.WeaponData.PrimaryAmmoType.ResourceName, 10000 );

		Activator.SwitchToWeapon( weapon );
		DestroyItem();
	}

	public override void Look( IPressable.Event e )
	{
		if ( !SlotTaken() ) return;

		e.Source.Components.TryGet<BasePlayer>( out var Activator );
		if ( Activator.LifeState == LifeState.Dead ) return;

		DebugOverlay.ScreenText( new Vector2( Screen.Width * 0.5f, Screen.Height * 0.55f ), $"[Hold {Input.GetButtonOrigin( "Use" )}] {ResolvePrintName( SlotTakenBy )} -> {ResolvePrintName( Data )}" );
		if ( _counter > 0 ) DebugOverlay.ScreenText( new Vector2( Screen.Width * 0.5f, Screen.Height * 0.575f ), $"{MathF.Truncate( _counter / PickupTime * 100 )}%" );
	}

	/// <summary>500 null checks to get proper printable weapon name</summary><returns>Either the name or placeholder</returns>
	public static string ResolvePrintName( WeaponParse data ) => !data.IsValid() || string.IsNullOrWhiteSpace( data.Name ) ? "Unnamed Weapon" : data.Name;
}
