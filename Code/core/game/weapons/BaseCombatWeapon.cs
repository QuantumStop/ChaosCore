namespace Core;

#if FMOD
#endif

using System;
using Core.AI;

[Hide]
public partial class BaseCombatWeapon : BaseEntity
{
	protected override string GetEditorVis() => null;
	/// <summary>
	/// Who has this weapon? We use the funny WeaponOwner struct which has the casts built in, so you don't have to if (Owner is BasePlayer) every time
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] public WeaponOwner Owner { get; set; }
	[Property, ReadOnly, Feature( "Debug" )] public WeaponParse WeaponData { get; set; }
	public BaseCombatWeapon() => WeaponData = WeaponParse.GetWeaponData( GetType().Name );

	/// <summary>
	/// Primary amount WITH chambered bullet
	/// </summary>
	[Property, Header( "Primary" ), Feature( "Debug" ), ShowIf( nameof( UsesPrimary ), true )]
	public int PrimaryAmmoLoaded
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				ChangePrimaryAmmo();
			}
		}
	}
	/// <summary>
	/// Primary amount of chambered bullets
	/// </summary>
	[Property, Feature( "Debug" ), ShowIf( nameof( UsesPrimary ), true )] public int PrimaryAmmoInChamber { get; set; }
	/// <summary>
	/// Secondary amount WITH chambered bullet
	/// </summary>
	[Property, ReadOnly, Header( "Secondary" ), Feature( "Debug" ), ShowIf( nameof( UsesSecondary ), true )] public int SecondaryAmmoLoaded { get; set; }

	/// <summary>
	/// Is the weapon ready to fire? (most likely in idle animation)
	/// </summary>
	[Property, ReadOnly, Header( "Stats" ), Feature( "Debug" )] public bool ReadyToFire { get; protected set; } = false;
	[Property, ReadOnly, Feature( "Debug" )] public bool FirstEquip { get; set; } = true;
	[Property, ReadOnly, Feature( "Debug" )] public bool MagOut { get; set; } = false;
	/// <summary>
	/// Reload one by one, instead of by mag (shotguns, rifles)
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] protected virtual bool _reloadsSingly => WeaponData.ReloadsSingly;
	/// <summary>
	/// This should've been OnStart
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] public bool DoFirstSetup { get; protected set; } = true;
	[Property, ReadOnly, Feature( "Debug" )] public bool WasOnTheGround { get; set; } = false;
	/// <summary>
	/// How many shots were fired in quick succession (consequitive shots)
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] protected int _shotsFired { get; set; }
	/// <summary>
	/// When was the last time we did any kind of attack
	/// </summary>	
	[Property, ReadOnly, Feature( "Debug" )] public float LastAttackTime { get; set; }
	/// <summary>
	/// When is the next primary shot
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" ), ShowIf( nameof( UsesPrimary ), true )] protected float _nextPrimaryAttack { get; set; }
	/// <summary>
	/// When is the next secondary shot
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" ), ShowIf( nameof( UsesSecondary ), true )] protected float _nextSecondaryAttack { get; set; }
	/// <summary>
	/// When is the next dry fire click
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" ), ShowIf( nameof( UsesPrimary ), true )] protected float _nextEmptyAttack { get; set; }
	/// <summary>
	/// How much time has this spent being empty
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] private float _gunEmptyTime { get; set; } = 0f;
	/// <summary>
	/// Delay between being empty and start of reload
	/// </summary>
	private const float _autoReloadDelay = 0.1f;
	/// <summary>
	/// Is the gun currently SPECIFICALLY holstered (and not just unavailable to shoot)
	/// PS: this is used for the "full holster" when weapon switching
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )]
	public bool IsHolstered
	{
		get;
		protected set
		{
			if ( field == value ) return;
			field = value;

			if ( value is true ) HolsterAction();
		}
	} = true;

	/// <summary>What happens when the holster finishes, per each kind of holster reason there can be</summary>
	protected virtual void HolsterAction()
	{
		switch ( Owner.Player.HolsterOwner )
		{
			default:
				break;
			case BasePlayer.HolsterType.Weapon:
				Owner.Player?.ApplyWeaponSwitch();
				break;
		}
	}
	/// <summary>
	/// Is attack blocked (mid reload, mid draw)
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] protected bool _disallowAttack { get; set; } = false;
	/// <summary>
	/// Non-empty reload has one less stage - no bolt pull/slide release
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] private int _reloadStageAmount => WeaponData.StageAmount - (PrimaryAmmoLoaded > 0 ? 1 : 0);
	/// <summary>
	/// Is current stage last? Technically a useless check due to instantly resetting to 0 after reaching last
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] private bool _isLastStage => _currentReloadStage >= _reloadStageAmount;
	/// <summary>
	/// Is the gun empty
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )]
	private bool _gunEmpty
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				ChangeEmpty(); // methods instead of inline so the methods can be virtual now and then overriden if needed
			}
		}
	} = false;
	/// <summary>
	/// Used to track whether we are storing a staged reloading state
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] protected bool _inStagedReload => _currentReloadStage > 0;
	/// <summary>
	/// Is this weapon currently reloading (stage agnostic, some don't care)
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] public bool IsReloading { get; protected set; }
	/// <summary>
	/// What stage of reloading I'm on
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )]
	protected int _currentReloadStage
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				ChangeReloadStage(); // methods instead of inline so the methods can be virtual now and then overriden if needed
			}
		}
	}
	protected virtual void ChangeReloadStage()
	{
		Owner.Player?.SetAllAnimgraphParams( "i_reload_stage", _currentReloadStage );
		if ( DebugReloadStage ) Log.Info( WeaponData.Name + "'s stage has changed to " + _currentReloadStage );
		if ( _isLastStage ) FinishReload();
	}

	protected virtual void ChangePrimaryAmmo() => Owner.Player?.SetAllAnimgraphParams( "i_ammo_loaded", PrimaryAmmoLoaded );
	protected virtual void ChangeEmpty() => Owner.Player?.SetAllAnimgraphParams( "b_empty", _gunEmpty );

	[ConVar( "debug_reloadstage" )] static public bool DebugReloadStage { get; set; }
	[ConVar( "debug_disable_recoil", ConVarFlags.Cheat )] static public bool DebugNoRecoil { get; set; }

	/// <summary>
	/// Syncs all animgraph parameters on Player to reflect current weapon state.
	/// </summary>
	private void SyncAnimgraphState()
	{
		FirstEquip = FirstEquip && DoFirstSetup;

		Owner.Player?.SetAllAnimgraphParams( "b_first_equip", FirstEquip );
		Owner.Player?.SetAllAnimgraphParams( "b_mag_out", MagOut );
		Owner.Player?.SetAllAnimgraphParams( "i_ammo_loaded", PrimaryAmmoLoaded );
		Owner.Player?.SetAllAnimgraphParams( "i_reload_stage", _currentReloadStage );
		Owner.Player?.SetAllAnimgraphParams( "b_empty", _gunEmpty );
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( (Owner.Player.IsValid() && !Owner.Player.IsControlledLocally) || IsProxy ) return;

		if ( UsesPrimary() ) _nextPrimaryAttack = WorldTime.Now;
		if ( UsesSecondary() ) _nextSecondaryAttack = WorldTime.Now;

		if ( !WeaponData.WeaponViewmodel.IsValid() ) ReadyToFire = true;
		if ( !WeaponData.WeaponViewmodel.IsValid() ) IsHolstered = false;
	}

	protected override void OnStart()
	{
		base.OnStart();

		if ( (Owner.Player.IsValid() && !Owner.Player.IsControlledLocally) || IsProxy ) return;

		//		if ( BasePlayer.Local.CurrentWeapon != this )
		//			return;

		FirstSetup();
		SetupCustomData();

		WasOnTheGround = false;
	}

	/// <summary>
	/// Use this to setup your custom data instead of overriding OnStart and having to do base.OnStart() etc etc
	/// </summary>
	protected virtual void SetupCustomData() { }

	protected virtual void FirstSetup()
	{
		if ( DoFirstSetup )
		{
			if ( !WasOnTheGround ) PrimaryAmmoLoaded = WeaponData.PrimaryAmmoCapacity;
			PrimaryAmmoInChamber = 1;

			_currentReloadStage = 0;

			IsHolstered = true;

			DoFirstSetup = false;
		}
	}

	/// <summary>
	/// Use this to pre-warm a newly given weapon that's not enabled by default.
	/// This also makes sure that if you picked up multiple weapons, first equip doesn't play on all of them when switched for the first time.
	/// </summary>
	public void ForceFirstSetup() => FirstSetup();

	protected override void OnUpdate()
	{
		// nobody owns this
		if ( !Owner.Player.IsValid() && !Owner.NPC.IsValid() ) return;

		if ( (Owner.Player.IsValid() && !Owner.Player.IsControlledLocally) || IsProxy ) return;

		// dont run the whole update if its not equipped
		if ( !ReadyToFire ) return;

		if ( Owner.Player.IsValid() )
		{
			HandleWeaponInput();
			HandleWeaponStates();
			return;
		}

		if ( Owner.NPC.IsValid() ) OnUpdateNPC();
	}

	protected override void OnFixedUpdate()
	{
		// nobody owns this
		if ( !Owner.Player.IsValid() && !Owner.NPC.IsValid() ) return;

		if ( (Owner.Player.IsValid() && !Owner.Player.IsControlledLocally) || IsProxy ) return;

		// dont run the whole fixedupdate if its not equipped
		if ( !ReadyToFire ) return;

		if ( Owner.NPC.IsValid() ) OnFixedUpdateNPC();
	}

	/// <summary>
	/// Handle what all the buttons do for weapons, kinda equivalent to BaseCombatWeapon::ItemPostFrame()
	/// </summary>
	protected virtual void HandleWeaponInput()
	{
		// Secondary has priority
		if ( Input.Down( "attack2" ) && _nextSecondaryAttack <= WorldTime.Now )
		{
			if ( !IsMeleeWeapon() && UsesSecondary() && SecondaryAmmoLoaded <= 0 )
			{
				HandleFireOnEmpty();
			}
			else if ( Owner.Player.IsUnderwater && !WeaponData.FiresUnderwater ) // remove the false when water level is implemented
			{
				if ( _nextSecondaryAttack < 0 )
				{
					BasePlayer.Local?.SetAllAnimgraphParams( "b_attack2", true );
					_nextSecondaryAttack = WorldTime.Now + 0.2f;
				}
				return;
			}
			else
			{
				// reverse of below
				if ( Input.Pressed( "attack2" ) || Input.Released( "attack1" ) )
				{
					_nextSecondaryAttack = WorldTime.Now;
				}

				SecondaryAttack();
			}


		}
		if ( Input.Down( "attack1" ) && _nextPrimaryAttack <= WorldTime.Now )
		{
			if ( !IsMeleeWeapon() && UsesPrimary() && PrimaryAmmoLoaded <= 0 )
			{
				HandleFireOnEmpty();
			}
			else if ( Owner.Player.IsUnderwater && !WeaponData.FiresUnderwater ) // remove the false when water level is implemented
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
				// If the firing button was just pressed, or the alt-fire just released, reset the firing time
				if ( Input.Pressed( "attack1" ) || Input.Released( "attack2" ) )
				{
					_nextPrimaryAttack = WorldTime.Now;
				}

				PrimaryAttack();
			}
		}

		if ( Input.Pressed( "reload" ) )
		{
			StartReload();
		}
	}
	/// <summary>
	/// Handle the animgraph params, reload stuff, anything that isn't input but has to be per frame
	/// </summary>
	protected virtual void HandleWeaponStates()
	{
		//	auto reload even if you are already in one, but only if you have ammo
		if ( !IsMeleeWeapon() )
		{
			if ( PrimaryAmmoLoaded <= 0 && ReadyToFire && HasUsableAmmo() )
			{
				_gunEmpty = true;
				if ( _gunEmptyTime + _autoReloadDelay < WorldTime.Now )
					StartReload();
			}
			else if ( (PrimaryAmmoLoaded > 0) && _inStagedReload )
			{
				StartReload();
			}
			else if ( PrimaryAmmoLoaded <= 0 && ReadyToFire && !HasUsableAmmo() ) // super empty
			{
				_gunEmpty = true;
				if ( _gunEmptyTime + (_autoReloadDelay * 5) < WorldTime.Now )
					Owner.Player?.SwitchToWeapon( BasePlayer.BestNextWeapon( this ) );
			}
			else
			{
				_gunEmpty = false;
				_gunEmptyTime = WorldTime.Now;
			}
		}
	}
	/// <summary>
	/// Draw the weapon.
	/// Needs to be public because other stuff can force us to draw
	/// </summary>
	public virtual void Draw( BasePlayer.HolsterType type = BasePlayer.HolsterType.Weapon, bool force = false )
	{
		if ( !force && Owner.Player.HolsterOwner != BasePlayer.HolsterType.None && Owner.Player.HolsterOwner != type ) return;

		Owner.Player.HolsterOwner = BasePlayer.HolsterType.None; // clear the owner

		SyncAnimgraphState();
		Owner.Player?.SetAllAnimgraphParams( "b_equipped", true );

		Enabled = true;
		if ( !WeaponData.WeaponViewmodel.IsValid() ) ReadyToFire = true;
	}
	/// <summary>
	/// Holster the weapon.
	/// Needs to be public because other stuff can force us to put down the weapon
	/// </summary>
	public virtual void Holster( BasePlayer.HolsterType type = BasePlayer.HolsterType.Weapon, bool force = false )
	{
		if ( !force && Owner.Player.HolsterOwner != BasePlayer.HolsterType.None && Owner.Player.HolsterOwner != type ) return; // has to be not None and the same thing (None is default, so we have to force through it)

		Owner.Player.HolsterOwner = type; // new owner

		Owner.Player?.SetAllAnimgraphParams( "b_equipped", false );

		if ( !WeaponData.WeaponViewmodel.IsValid() ) ReadyToFire = false;
		Enabled = false;    // very important to disable the holstered gun component
	}

	/// <summary>
	/// Can I shoot?
	/// </summary>
	/// <returns>Yes/No</returns>
	/// <param name="primary">Is this used to check PrimaryAttack()?</param>
	protected virtual bool AttackConditions( bool primary = true )
	{
		// is not local player
		if ( (Owner.Player.IsValid() && !Owner.Player.IsControlledLocally) || IsProxy ) return false;

		if ( Owner.Player?.LifeState == LifeState.Dead ) return false;

		if ( Owner.Player.SelectionOpen ) return false;

		// blocked by an animtag
		if ( _disallowAttack ) return false;

		return true;
	}

#if !FMOD
	protected SoundHandle _shootHandle;
#endif

	/// <summary>
	/// The main weapon shot, usually left mouse click.
	/// </summary>
	protected virtual void PrimaryAttack()
	{
		if ( !AttackConditions() )
			return;

		if ( !UsesPrimary() )
			return;

		if ( (WorldTime.Now - LastAttackTime) > (GetPrimaryFireRate() + 0.1f) ) _shotsFired = 0;

		++_shotsFired;

		LastAttackTime = WorldTime.Now;

		Owner.Player?.SetAllAnimgraphParams( "b_attack1", true );

		AttackSound( Owner.Player.IsPossessedLocally );

		if ( !BasePlayer.NoReload )
		{
			PrimaryAmmoLoaded -= 1;
			PrimaryAmmoInChamber = Math.Min( 1, PrimaryAmmoLoaded );
		}

		_nextPrimaryAttack = WorldTime.Now + GetPrimaryFireRate();

		CreateMuzzleFlash();
		EjectShells();
		FireBullet();

		if ( !DebugNoRecoil )
		{
			CameraEffects.StartRecoil( WeaponData, LastAttackTime );
			BasePlayer.ApplyPhysRecoil( Owner.Player );
		}
	}

	protected virtual int _amountPerShot => WeaponData.BulletsPerShot;
	protected virtual bool _isProjectile => false;

	/// <summary>
	/// Actually fire the AttackResult bullets	
	/// </summary>
	/// <param name="isPlayer">Is the damage dealt by player?</param>
	/// <param name="isPrimary">Is this PrimaryAttack()?</param>
	public virtual void FireBullet( bool isPlayer = true, bool isPrimary = true )
	{
		var damageInfo = GetDamageInfo( isPrimary, isPlayer );
		var ownerTransform = isPlayer ? Owner.Player.GetEyeTransform() : Owner.NPC.WorldTransform;

		if ( WeaponData.SpreadType == SpreadType.SPREAD_DYNAMIC
			&& WeaponData.DynamicSpreadType == DynamicSpreadType.PER_CONSECUTIVE_SHOT )
		{
			for ( int i = 0; i < _amountPerShot; i++ )
			{
				float spread = GetSpreadForBullet( isPrimary, _shotsFired, i, _amountPerShot );

				if ( _isProjectile )
					AttackManager.FireProjectile( ownerTransform, damageInfo, spread );
				else
					AttackManager.FireHitscan( ownerTransform, damageInfo, spread );
			}
		}
		else
		{
			for ( int i = 0; i < _amountPerShot; i++ )
			{
				float spread = GetSpreadForBullet( isPrimary, _shotsFired + i );

				if ( _isProjectile )
					AttackManager.FireProjectile( ownerTransform, damageInfo, spread );
				else
					AttackManager.FireHitscan( ownerTransform, damageInfo, spread );
			}
		}
	}

	/// <summary>
	/// Empty version (no ammo) of PrimaryAttack(), but can be used for SecondaryAttack().
	/// </summary>
	protected virtual void HandleFireOnEmpty( float delay = 0.5f )
	{
		if ( _nextEmptyAttack <= WorldTime.Now && !IsReloading )
		{
			Owner.Player?.SetAllAnimgraphParams( "b_attack1", true );
			_nextEmptyAttack = WorldTime.Now + delay;
		}
	}
	/// <summary>
	/// Second type of attack, usually right mouse click.
	/// </summary>
	protected virtual void SecondaryAttack()
	{
		if ( !AttackConditions( false ) ) return;

		if ( !UsesSecondary() ) return;

		LastAttackTime = WorldTime.Now;

		// unimplemented by default	
	}
	/// <summary>
	/// Reload the weapon.
	/// </summary>
	protected virtual void StartReload( bool reloadPrimary = true )
	{
		if ( reloadPrimary )
		{
			// only handle singly for primary, secondary most of the time is already singly anyway (hl2 SMG1 grenade, chaos M16 grenade, etc etc etc)
			if ( _reloadsSingly )
			{
				// TODO
			}
			else
			{
				if ( !UsesPrimary() )
					return;

				if ( !BasePlayer.InfiniteAmmo )
				{
					if ( PrimaryAmmoLoaded >= GetPrimaryCapacity() + 1 ) // dont reload if the magazine is already full (and we are not mid reloading)
						return;

					if ( Owner.Player?.GetReserveAmmo( GetPrimaryAmmoType() ) <= 0 ) // dont reload if we dont have ammo
						return;
				}

				if ( !_isLastStage )
					Owner.Player?.SetAllAnimgraphParams( "b_reload", true );

				IsReloading = true;

				if ( !WeaponData.WeaponViewmodel.IsValid() )
					FinishReload();
			}
		}
		else
		{
			if ( !UsesSecondary() )
				return;

			if ( !BasePlayer.InfiniteAmmo )
			{
				if ( SecondaryAmmoLoaded >= GetSecondaryCapacity() + 1 )
					return;

				if ( Owner.Player?.GetReserveAmmo( GetSecondaryAmmoType() ) <= 0 )
					return;
			}
		}

	}

	protected virtual void FinishReload( bool reloadPrimary = true )
	{
		if ( reloadPrimary )
		{
			if ( _debugAnimEvents )
				Log.Info( "(not) AnimEvent: FinishReload" );

			// since its looking for the smallest value, when having no reserve and trying to reload, it will be the smallest, and will try to add nothing
			// in that case just pretend the reserve is always bigger than current ammo
			var minHack = !BasePlayer.InfiniteAmmo ? Owner.Player.GetReserveAmmo( WeaponData.PrimaryAmmoType.ResourceName ) : GetPrimaryCapacity() + 5;
			var primary = Math.Min( GetPrimaryCapacity() + (PrimaryAmmoInChamber > 0 ? 1 : 0) - PrimaryAmmoLoaded, minHack );
			// Log.Info( primary );

			if ( !BasePlayer.InfiniteAmmo )
				Owner.Player?.RemoveReserveAmmo( WeaponData.PrimaryAmmoType.ResourceName, primary );

			PrimaryAmmoLoaded += primary;
			PrimaryAmmoInChamber = Math.Min( 1, PrimaryAmmoLoaded );

			Owner.Player?.SetAllAnimgraphParams( "b_reload", false );

			IsReloading = false;

			_currentReloadStage = 0;
		}

	}

	/// <summary>We need to know what kind of owner this gun has, because we don't have BaseCombatCharacter</summary>
	public readonly struct WeaponOwner
	{
		/// <summary>If the owner is a Player, this has it</summary>
		public readonly BasePlayer Player;
		/// <summary>If the owner is an NPC, this has it</summary>
		public readonly AIController NPC;

		public WeaponOwner( BasePlayer player ) => Player = player;

		public WeaponOwner( AIController npc ) => NPC = npc;
	}
}
