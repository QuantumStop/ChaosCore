using System;
using Sandbox.Internal;

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
	[Property, ReadOnly] public bool WasDropped { get; set; } = false;
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

		_isPressing = true;

		if ( _counter < PickupTime )
			_counter += Time.Delta;

		if ( _counter == PickupTime )
		{
			press.Source.Components.TryGet<BasePlayer>( out var Activator );

			var damn = Activator.WeaponList.ToList();

			foreach ( var weapons in damn )
			{
				if ( (weapons.WeaponData?.Bucket == Data?.Bucket) && (weapons.WeaponData?.Position == Data?.Position) )
				{
					var weapon = Activator.GiveWeaponByName( Data.ResourceName, null, false, this );

					if ( !weapon.IsValid() || Activator.LifeState == LifeState.Dead )
						return false;

					base.OnPickup( Activator );

					weapon.PrimaryAmmoLoaded = InternalAmmoCountPrimary;
					weapon.WasOnTheGround = true;

					Activator.SwitchToWeapon( weapon );
					DestroyItem();
				}
			}
		}

		return true;
	}

	/// <summary>
	/// Horrible, horrible hack to know if we need to pre-call FirstSetup() on a given baseweapon
	/// </summary>
	public bool NeedsWarmingUp { get; set; } = false;

	public override void OnPickup( BasePlayer Activator = null )
	{
		if ( Activator.LifeState == LifeState.Dead ) // Ghosts can attempt to pick up items, but we won't let them actually do it
			return;

		foreach ( var weapons in Activator.WeaponList )
		{
			if ( (weapons.WeaponData?.Bucket == Data?.Bucket) && (weapons.WeaponData?.Position == Data?.Position) && (weapons.WeaponData.ResourceName != Data.ResourceName) ) return;
		}

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
}
