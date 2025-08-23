namespace Core;
using System;

// move this out this is more npc related
public enum WeaponEquipSlot
{
	SLOT_PRIMARY,
	SLOT_SIDEARM,
	SLOT_MELEE,
	NONE
}

// probably dont namespace this

[Hide]
public partial class BaseCombatWeapon : BaseEntity
{
	protected override string GetEditorVis() { return null; }

	[Property, ReadOnly, Feature( "Debug" )] public WeaponParse WeaponData { get; set; }

	public TimeSince TimeSinceAttacked { get; set; } = 1000;
	public BaseCombatWeapon()
	{
		WeaponData = WeaponParse.GetWeaponData( GetType().Name );
	}

	/// <summary>
	/// Primary amount WITH chambered bullet
	/// </summary>
	[Property, ReadOnly, Header( "Primary" ), Feature( "Debug" ), Change( nameof( ChangePrimaryAmmo ) ), HideIf( nameof( UsesPrimary ), false )] public int PrimaryAmmoLoaded { get; set; }
	/// <summary>
	/// Primary amount of chambered bullets
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" ), HideIf( nameof( UsesPrimary ), false )] public int PrimaryAmmoInChamber { get; set; }
	/// <summary>
	/// Secondary amount WITH chambered bullet
	/// </summary>
	[Property, ReadOnly, Header( "Secondary" ), Feature( "Debug" ), HideIf( nameof( UsesSecondary ), false )] public int SecondaryAmmoLoaded { get; set; }

	/// <summary>
	/// Is the weapon equipped or down
	/// </summary>
	[Property, ReadOnly, Header( "Stats" ), Feature( "Debug" )] public bool Equipped { get; set; } = false;
	[Property, ReadOnly, Feature( "Debug" )] public bool FirstEquip { get; set; } = true;
	[Property, ReadOnly, Feature( "Debug" )] public bool MagOut { get; set; } = false;
	/// <summary>
	/// Reload one by one, instead of by mag (shotguns, rifles)
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] protected virtual bool ReloadsSingly { get; set; }
	/// <summary>
	/// This should've been OnStart
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] protected bool _DoFirstSetup { get; set; } = true;

	/// <summary>
	/// When is the next primary shot
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" ), HideIf( nameof( UsesPrimary ), false )] protected float _nextPrimaryAttack { get; set; }
	/// <summary>
	/// When is the next secondary shot
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" ), HideIf( nameof( UsesSecondary ), false )] protected float _nextSecondaryAttack { get; set; }
	/// <summary>
	/// When is the next dry fire click
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" ), HideIf( nameof( UsesPrimary ), false )] protected float _nextEmptyAttack { get; set; }
	/// <summary>
	/// Do i ignore next shot? (For alt tabbing, or else)
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] public bool _ingoreNextAttack { get; set; } = false;
	/// <summary>
	/// How much time has this spent being empty
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] private float _gunEmptyTime { get; set; } = 0f;
	/// <summary>
	/// Delay between being empty and start of reload
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] private float _autoReloadDelay { get; set; } = 0.1f;
	/// <summary>
	/// I don't remeber
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] protected bool _stealEquip { get; set; }
	/// <summary>
	/// Is attack blocked (mid reload, mid draw)
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] protected bool _disallowAttack { get; set; }
	/// <summary>
	/// Non-empty reload has one less stage - no bolt pull/slide release
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] private int _ReloadStageAmount => WeaponData.StageAmount - (PrimaryAmmoLoaded > 0 ? 1 : 0);
	/// <summary>
	/// Is current stage last? Technically a useless check due to instantly resetting to 0 after reaching last
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] private bool _isLastStage => _currentReloadStage >= _ReloadStageAmount;
	/// <summary>
	/// Is the gun empty
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" ), Change( nameof( ChangeEmpty ) )] private bool _gunEmpty { get; set; } = false;
	/// <summary>
	/// Used to track whether we are storing a staged reloading state
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] protected bool _inStagedReload => _currentReloadStage > 0;
	/// <summary>
	/// What stage of reloading I'm on
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" ), Change( nameof( ChangeReloadStage ) )] protected int _currentReloadStage { get; set; }
	protected virtual void ChangeReloadStage()
	{
		BasePlayer.Local?.SetAllAnimgraphParams( "i_reload_stage", _currentReloadStage );
		if ( DebugReloadStage ) Log.Info( WeaponData.Name + "'s stage has changed to " + _currentReloadStage );
		if ( _isLastStage ) FinishReload();
	}

	protected virtual void ChangePrimaryAmmo() { BasePlayer.Local?.SetAllAnimgraphParams( "i_ammo_loaded", PrimaryAmmoLoaded ); }
	protected virtual void ChangeEmpty() { BasePlayer.Local?.SetAllAnimgraphParams( "b_empty", _gunEmpty ); }

	[ConVar( "debug_reloadstage" )] static bool DebugReloadStage { get; set; }


	private void InitSetup()
	{
		BasePlayer.Local?.SetAllAnimgraphParams( "b_first_equip", FirstEquip );
		BasePlayer.Local?.SetAllAnimgraphParams( "b_steal_equip", _stealEquip );
		BasePlayer.Local?.SetAllAnimgraphParams( "b_mag_out", MagOut );
		BasePlayer.Local?.SetAllAnimgraphParams( "i_ammo_loaded", PrimaryAmmoLoaded );
		BasePlayer.Local?.SetAllAnimgraphParams( "i_reload_stage", _currentReloadStage );
		BasePlayer.Local?.SetAllAnimgraphParams( "b_empty", _gunEmpty );
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( IsProxy )
			return;

		if ( BasePlayer.Local.CurrentWeapon != this )
			return;

		if ( UsesPrimary() ) _nextPrimaryAttack = Time.Now;
		if ( UsesSecondary() ) _nextSecondaryAttack = Time.Now;


		//		Log.Info( "OnEnabled: " + WeaponData.Name );

		// setup animgraph state
		InitSetup();

		Equipped = true;

		if ( _inStagedReload )
			StartReload();
	}

	protected override void OnStart()
	{
		base.OnStart();

		if ( IsProxy )
			return;

		if ( BasePlayer.Local.CurrentWeapon != this )
			return;

		if ( _DoFirstSetup )
		{
			PrimaryAmmoLoaded = WeaponData.PrimaryAmmoCapacity;
			PrimaryAmmoInChamber = 1;

			_currentReloadStage = 0;

			_DoFirstSetup = false;
		}

		// a copy of onenabled just in case, definitely needed for first equip
		InitSetup();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		// dont run the whole update if its not equipped
		if ( !Equipped )
			return;

		HandleWeaponInput();
		HandleWeaponStates();
	}

	/// <summary>
	/// Handle what all the buttons do for weapons, kinda equivalent to BaseCombatWeapon::ItemPostFrame()
	/// </summary>
	protected virtual void HandleWeaponInput()
	{
		if ( !Application.IsFocused )
			_ingoreNextAttack = true;

		// this now does it for two clicks, not one pls fix
		if ( Input.Released( "attack1" ) || Input.Released( "attack2" ) )
			_ingoreNextAttack = false;

		// Secondary has priority
		if ( Input.Down( "attack2" ) && _nextSecondaryAttack <= Time.Now )
		{
			if ( !IsMeleeWeapon() && UsesSecondary() && SecondaryAmmoLoaded <= 0 )
			{
				HandleFireOnEmpty();
			}
			else if ( /*UnderwaterLevel == 3 && */ WeaponData.FiresUnderwater && false ) // remove the false when water level is implemented
			{
				if ( _nextSecondaryAttack < 0 )
				{
					BasePlayer.Local?.SetAllAnimgraphParams( "b_attack2", true );
					_nextSecondaryAttack = Time.Now + 0.2f;
				}
			}
			else
			{
				// reverse of below
				if ( Input.Pressed( "attack2" ) || Input.Released( "attack1" ) )
				{
					_nextSecondaryAttack = Time.Now;
				}

				SecondaryAttack();
			}


		}
		if ( Input.Down( "attack1" ) && _nextPrimaryAttack <= Time.Now )
		{
			if ( !IsMeleeWeapon() && UsesPrimary() && PrimaryAmmoLoaded <= 0 )
			{
				HandleFireOnEmpty();
			}
			else if ( /*UnderwaterLevel == 3 && */ WeaponData.FiresUnderwater && false ) // remove the false when water level is implemented
			{
				if ( _nextPrimaryAttack < 0 )
				{
					BasePlayer.Local?.SetAllAnimgraphParams( "b_attack1", true );
					_nextPrimaryAttack = Time.Now + 0.2f;
				}
			}
			else
			{
				// If the firing button was just pressed, or the alt-fire just released, reset the firing time
				if ( Input.Pressed( "attack1" ) || Input.Released( "attack2" ) )
				{
					_nextPrimaryAttack = Time.Now;
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
		//	auto reload even if you are already in one
		if ( PrimaryAmmoLoaded <= 0 && Equipped )
		{
			_gunEmpty = true;
			if ( _gunEmptyTime + _autoReloadDelay < Time.Now )
				StartReload();
		}
		else
		{
			_gunEmpty = false;
			_gunEmptyTime = Time.Now;
		}
	}
	/// <summary>
	/// Draw the weapon.
	/// Needs to be public because other stuff can force us to draw
	/// </summary>
	public virtual void Draw()
	{
		BasePlayer.Local?.SetAllAnimgraphParams( "b_equipped", true );

		_stealEquip = false;

		Enabled = true;
		Equipped = true;
	}
	/// <summary>
	/// Holster the weapon.
	/// Needs to be public because other stuff can force us to put down the weapon
	/// </summary>
	public virtual void Holster()
	{
		BasePlayer.Local?.SetAllAnimgraphParams( "b_equipped", false );

		Equipped = false;
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
		if ( IsProxy )
			return false;

		// ignoring next attack (alt tabbed)
		if ( _ingoreNextAttack )
			return false;

		// blocked by an animtag
		if ( _disallowAttack )
			return false;

		return true;
	}

	/// <summary>
	/// The main weapon shot, usually left mouse click.
	/// </summary>
	protected virtual void PrimaryAttack()
	{
		if ( !AttackConditions() )
			return;

		if ( !UsesPrimary() )
			return;

		BasePlayer.Local?.SetAllAnimgraphParams( "b_attack1", true );

		foreach ( var sound in WeaponData.AttackSoundsPrimary )
			Sound.Play( sound ).ListenLocal = true;

		EnvironmentManager.Instance.PlayEnviromentGunfire();

		if ( !BasePlayer.NoReload )
		{
			PrimaryAmmoLoaded -= 1;
			PrimaryAmmoInChamber = Math.Min( 1, PrimaryAmmoLoaded );
		}

		_nextPrimaryAttack = Time.Now + GetPrimaryFireRate();

		if ( WeaponData?.WeaponCrosshair?.WeaponCrosshairType != WeaponCrosshairType.None ) TimeSinceAttacked = 0;

		BasePlayer.Local.CurrentWeapon.TimeSinceAttacked = 0;

		CreateMuzzleLight();
		EjectShells();
		FireBullet();

		BasePlayer.Local?.StartVisualRecoil();
		BasePlayer.Local?.ApplyPhysRecoil();
	}

	/// <summary>
	/// Spawn a shell effect on a shell_eject attachment. Need Bone Objects!
	/// </summary>
	protected virtual void EjectShells()
	{
		SkinnedModelRenderer viewmodel = BasePlayer.Local?.ViewmodelWeapon;

		if ( WeaponData?.WeaponViewmodel?.Attachments.Get( "shell_eject" ) != null )
		{
			var ejectattachment = viewmodel.GetAttachmentObject( "shell_eject" );
			var velocity = BasePlayer.Local.Movement.Velocity;
			PrefabFile shellPrefab = WeaponData?.BulletCasingParticle;

			if ( ejectattachment.IsValid() && shellPrefab.IsValid() )
			{
				// Obsoloting, but keeping for reference, for now
				//	DebrisManager.StaticRef.CreateDebris( WeaponData?.BulletCasingModel?.ResourcePath,
				//	ejectattachment.WorldPosition + velocity * Time.Delta,
				//	ejectattachment.WorldRotation,
				//	ejectattachment.WorldRotation.Up * 70f + ejectattachment.WorldRotation.Right * 150f + velocity * 0.7f );

				DebrisManager.StaticRef.CreateShellCasing(
					shellPrefab.ResourcePath,
					ejectattachment.WorldPosition,
					ejectattachment.WorldRotation,
					ejectattachment.WorldRotation.Up * 100f +
					ejectattachment.WorldRotation.Right * 300f +
					velocity * 0.7f
				);
			}
		}
	}

	/// <summary>
	/// Actually fire the AttackResult bullets	
	/// </summary>
	/// <param name="amountPerShot">How much bullet per bullet</param>
	/// <param name="isPlayer">Is the damage dealt by player?</param>
	/// <param name="isPrimary">Is this PrimaryAttack()?</param>
	public virtual void FireBullet( int amountPerShot = 1, bool isPlayer = true, bool isPrimary = true )
	{
		var whatammo = isPrimary ? AmmoInfo.GetAmmoData( GetPrimaryAmmoType() ) : AmmoInfo.GetAmmoData( GetSecondaryAmmoType() );
		var whodmg = isPlayer ? whatammo.DamagePlayer : whatammo.DamageNPC;
		var whatshot = isPrimary ? WeaponData.SpreadDegreesPrimary : WeaponData.SpreadDegreesSecondary;

		for ( int i = 0; i < amountPerShot; i++ )
		{
			var tr = AttackManager.FireBullet( BasePlayer.Local.GetEyeTransform(),
			new CoreDamageInfo()
			{
				Attacker = GameObject.Root,
				Damage = whodmg,
				Tags = { "bullet" },
				Ammo = whatammo,
				BaseCombatWeapon = this
			}, whatshot );
		}
	}

	/// <summary>
	/// Empty version (no ammo) of PrimaryAttack(), but can be used for SecondaryAttack().
	/// </summary>
	protected virtual void HandleFireOnEmpty( float delay = 0.5f )
	{
		if ( _nextEmptyAttack < 0 )
		{
			BasePlayer.Local?.SetAllAnimgraphParams( "b_attack1", true );
			_nextEmptyAttack = Time.Now + delay;
		}
	}
	/// <summary>
	/// Second type of attack, usually right mouse click.
	/// </summary>
	protected virtual void SecondaryAttack()
	{
		if ( !AttackConditions() )
			return;

		if ( !UsesSecondary() )
			return;

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
			if ( ReloadsSingly )
			{
				// TODO
			}
			else
			{
				if ( !UsesPrimary() )
					return;

				if ( !BasePlayer.InfiniteAmmo )
				{
					if ( PrimaryAmmoLoaded >= GetPrimaryCapacity() + 1 )
						return;

					if ( BasePlayer.Local.GetReserveAmmo( GetPrimaryAmmoType() ) <= 0 )
						return;
				}

				if ( !_isLastStage )
					BasePlayer.Local?.SetAllAnimgraphParams( "b_reload", true );
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

				if ( BasePlayer.Local.GetReserveAmmo( GetSecondaryAmmoType() ) <= 0 )
					return;
			}
		}

	}

	protected virtual void FinishReload( bool reloadPrimary = true )
	{
		if ( reloadPrimary )
		{
			if ( DebugAnimEvents )
				Log.Info( "(not) AnimEvent: FinishReload" );

			// since its looking for the smallest value, when having no reserve and trying to reload, it will be the smallest, and will try to add nothing
			// in that case just pretend the reserve is always bigger than current ammo
			var minHack = !BasePlayer.InfiniteAmmo ? BasePlayer.Local.GetReserveAmmo( WeaponData.PrimaryAmmoType.ResourceName ) : GetPrimaryCapacity() + 5;
			var primary = Math.Min( GetPrimaryCapacity() + (PrimaryAmmoInChamber > 0 ? 1 : 0) - PrimaryAmmoLoaded, minHack );
			// Log.Info( primary );

			if ( !BasePlayer.InfiniteAmmo )
				BasePlayer.Local.RemoveReserveAmmo( WeaponData.PrimaryAmmoType.ResourceName, primary );

			PrimaryAmmoLoaded += primary;
			PrimaryAmmoInChamber = Math.Min( 1, PrimaryAmmoLoaded );

			BasePlayer.Local?.SetAllAnimgraphParams( "b_reload", false );
			_currentReloadStage = 0;
		}

	}
}
