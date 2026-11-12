// dont put these into a namespace it will fuck the weapon giving and youll have to do give sdk.weapon_smg and thats ass
using System;
using Core;

public class weapon_glock : Core.BaseCombatWeapon
{
	private float _soonestPrimaryAttack { get; set; }
	private float _soonestSecondaryAttack { get; set; }
	private float _refirePrimaryTime => 0.1f;

	[Property, Feature( "Debug" ), ReadOnly] private int _burstSize;
	[Property, Feature( "Debug" ), ReadOnly] private float _burstPauseRate;
	[Property, Feature( "Debug" ), ReadOnly] private int _burstLength;
	[Property, Feature( "Debug" ), ReadOnly] private float _nextBurstTime;
	[Property, Feature( "Debug" ), ReadOnly] private float _burstFireRate;
	[Property, Feature( "Debug" ), ReadOnly] private bool _allowNextBurst { get; set; } = true;

	[Property, Feature( "Debug" ), ReadOnly] protected float _nextThink;
	[Property, Feature( "Debug" ), ReadOnly] protected bool _doThink;

	protected override void OnStart()
	{
		base.OnStart();

		_soonestPrimaryAttack = WorldTime.Now;
		_soonestSecondaryAttack = WorldTime.Now;
		_nextBurstTime = WorldTime.Now;
	}

	protected override void SetupCustomData()
	{
		_burstSize = (int)GetCustomDataFloat( "BurstSize" );
		_burstPauseRate = GetCustomDataFloat( "BurstCycleRate" );
		_burstFireRate = 60 / GetCustomDataFloat( "BurstFireRate" );
	}

	protected override void HandleWeaponInput()
	{
		if ( Input.Down( "attack2" ) && _nextSecondaryAttack <= WorldTime.Now )
		{
			if ( UsesPrimary() && PrimaryAmmoLoaded <= 0 )
			{
				HandleFireOnEmpty();
			}
			else if ( Owner.Player.IsUnderwater && !WeaponData.FiresUnderwater )
			{
				if ( _nextSecondaryAttack < 0 )
				{
					Owner.Player?.SetAllAnimgraphParams( "b_attack2", true );
					_nextSecondaryAttack = WorldTime.Now + 0.2f;
				}

				return;
			}
			else
			{
				if ( Input.Pressed( "attack2" ) || Input.Released( "attack1" ) )
					_nextSecondaryAttack = WorldTime.Now;

				SecondaryAttack();
			}
		}

		if ( Input.Released( "attack2" ) )
			_allowNextBurst = true;

		if ( Input.Down( "attack1" ) && _nextPrimaryAttack <= WorldTime.Now )
		{
			if ( UsesPrimary() && PrimaryAmmoLoaded <= 0 )
			{
				HandleFireOnEmpty();
			}
			else if ( Owner.Player.IsUnderwater && !WeaponData.FiresUnderwater )
			{
				if ( _nextPrimaryAttack < 0 )
				{
					Owner.Player?.SetAllAnimgraphParams( "b_attack1", true );
					_nextPrimaryAttack = WorldTime.Now + 0.2f;
				}

				return;
			}
			else
			{
				if ( Input.Pressed( "attack1" ) || Input.Released( "attack2" ) )
					_nextPrimaryAttack = WorldTime.Now;

				PrimaryAttack();
			}
		}

		if ( Input.Pressed( "reload" ) )
			StartReload();

		if ( !Input.Down( "attack1" ) && _soonestPrimaryAttack < WorldTime.Now )
		{
			_nextPrimaryAttack = WorldTime.Now - 0.1f;
			_nextSecondaryAttack = WorldTime.Now - 0.1f;
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( _doThink && _nextThink <= WorldTime.Now )
			SingleAttackThink();
	}

	protected override void PrimaryAttack()
	{
		base.PrimaryAttack();

		_soonestPrimaryAttack = WorldTime.Now + _refirePrimaryTime;
		_soonestSecondaryAttack = WorldTime.Now + _refirePrimaryTime;
	}

	protected override void SecondaryAttack()
	{
		if ( !AttackConditions() )
			return;

		if ( !UsesPrimary() )
			return;

		if ( !_allowNextBurst )
			return;

		if ( _burstLength > 0 )
			return;

		if ( _nextBurstTime <= WorldTime.Now )
		{
			_burstLength = _burstSize;
			_nextPrimaryAttack = WorldTime.Now + _burstPauseRate;
			_nextSecondaryAttack = WorldTime.Now + _burstPauseRate;

			_doThink = true;

			_nextBurstTime = WorldTime.Now + _burstPauseRate;
			_allowNextBurst = false;
		}
	}

	private void SingleAttackThink()
	{
		if ( PrimaryAmmoLoaded <= 0 )
			_burstLength = 0;

		if ( _burstLength <= 0 )
		{
			_doThink = false;
			return;
		}

		_burstLength--;

		StartViewmodelAnimGraphAttack( AnimGraphWeaponAttackType.attack_primary );
		Owner.Player?.SetAllAnimgraphParams( "b_attack1", true );

		AttackSound( Owner.Player.IsPossessedLocally );

		if ( !BasePlayer.NoReload )
		{
			PrimaryAmmoLoaded -= 1;
			PrimaryAmmoInChamber = Math.Min( 1, PrimaryAmmoLoaded );
		}

		_nextThink = WorldTime.Now + _burstFireRate;

		CreateMuzzleFlash();
		EjectShells();
		FireBullet();

		if ( !DebugNoRecoil )
			BasePlayer.ApplyPhysRecoil( Owner.Player );
	}
}
