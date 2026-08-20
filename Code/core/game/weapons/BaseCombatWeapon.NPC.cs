using System;

namespace Core;

public enum WeaponHolsterSlot
{
	/// <summary>A big weapon that requires two hands, usually stored on the back if put away</summary>
	SLOT_TWOHANDED,
	/// <summary>A small weapon that requires one hand, usually stored in the side holster if put away</summary>
	SLOT_ONEHANDED,
	/// <summary>A melee weapon that can require any amount of hands but is defintely stored separately from other weapons</summary>
	SLOT_MELEE,
	/// <summary>I don't know</summary>
	NONE
}


public partial class BaseCombatWeapon
{
	/// <summary>
	/// OnUpdate but for NPC
	/// </summary>
	protected virtual void OnUpdateNPC() { }
	/// <summary>
	/// OnFixedUpdate but for NPC
	/// </summary>
	protected virtual void OnFixedUpdateNPC() => HandleNPCInput();
	/// <summary>
	/// Fake NPC left mouse input to know when it wants to attack
	/// </summary>
	public bool IsPrimaryAttackDown { get; set; } = false;
	/// <summary>
	/// Fake NPC right mouse input to know when it wants to attack
	/// </summary>
	public bool IsSecondaryAttackDown { get; set; } = false;

	/// <summary>
	/// Handle fake NPC inputs
	/// </summary>
	protected virtual void HandleNPCInput()
	{
		// Secondary has priority
		if ( IsSecondaryAttackDown && _nextSecondaryAttack <= WorldTime.Now )
		{
			if ( !IsMeleeWeapon() && UsesSecondary() && SecondaryAmmoLoaded <= 0 )
			{
				HandleFireOnEmpty();
			}
			else
			{
				// reverse of below
				if ( IsSecondaryAttackDown ) _nextSecondaryAttack = WorldTime.Now;
				SecondaryAttackNPC();
			}
		}
		if ( IsPrimaryAttackDown && _nextPrimaryAttack <= WorldTime.Now )
		{
			if ( !IsMeleeWeapon() && UsesPrimary() && PrimaryAmmoLoaded <= 0 )
			{
				HandleFireOnEmpty();
			}
			else // we don't support underwater shit
			{
				// If the firing button was just pressed, reset the firing time
				if ( IsPrimaryAttackDown ) _nextPrimaryAttack = WorldTime.Now;
				PrimaryAttackNPC();
			}
		}
	}
	/// <summary>
	/// NPC version of PrimaryAttack()
	/// </summary>
	protected virtual void PrimaryAttackNPC()
	{
		if ( !AttackConditionsNPC() ) return;
		if ( !UsesPrimary() ) return;

		if ( (WorldTime.Now - LastAttackTime) > (GetPrimaryFireRate() + 0.1f) ) _shotsFired = 0;

		++_shotsFired;

		LastAttackTime = WorldTime.Now;

		AttackSound( false );

		PrimaryAmmoLoaded -= 1;
		PrimaryAmmoInChamber = Math.Min( 1, PrimaryAmmoLoaded );

		_nextPrimaryAttack = WorldTime.Now + GetPrimaryFireRate();

		CreateMuzzleFlash( false );
		EjectShells();
		FireBullet();
	}
	/// <summary>
	/// NPC version of SecondaryAttack()
	/// </summary>
	protected virtual void SecondaryAttackNPC()
	{
		if ( !AttackConditionsNPC( false ) ) return;
		if ( !UsesSecondary() ) return;

		LastAttackTime = WorldTime.Now;

		// unimplemented by default	
	}

	protected virtual bool AttackConditionsNPC( bool primary = true )
	{
		if ( !Owner.NPC.IsValid() ) return false;

		if ( !Owner.NPC.IsAlive ) return false;

		// blocked by an animtag
		if ( _disallowAttack ) return false;

		return true;
	}

	/// <summary>Public function to be able to reload the weapon from elsewhere</summary>
	/// <param name="primary">Are we reloading the primary ammo or secondary?</param>
	public void ForceReload( bool primary = true ) => FinishReload( primary );
}
