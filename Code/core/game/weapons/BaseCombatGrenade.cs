using System;
using Core.Voxels;
#if FMOD
using FMODSbox;
#endif

namespace Core;

/// <summary>
/// Base class for all thrown explosive/utility grenades.
///
/// Explosion behaviour is driven entirely by WeaponParse fields (see ExplosionType, ExplosionRadius, etc.)
/// and the CustomData dictionary for fuse/cook/throw tuning:
///
///   CustomData keys:
///     "fuse_time"            seconds until thrown grenade explodes.          Default 3.0
///     "max_cook_time"        seconds before OnOvercooked fires.              Default 4.0
///     "explode_on_overcook"  1 = explode in hand; 0 = auto-throw.            Default 0
///     "throw_force"          impulse magnitude applied to the live grenade.  Default 1200
/// </summary>
[Hide]
public abstract partial class BaseGrenade : BaseCombatWeapon
{
	protected virtual float FuseTime => TryGetCustomData( "fuse_time", 3.0f );
	protected virtual float MaxCookTime => TryGetCustomData( "max_cook_time", 4.0f );
	protected virtual bool ExplodeOnOvercook => TryGetCustomData( "explode_on_overcook", 1f ) >= 1f;
	protected virtual float ThrowForce => TryGetCustomData( "throw_force", 1024f );

	protected float ExplosionRadius => WeaponData?.ExplosionRadius ?? 180f;
	protected float ExplosionDamage => WeaponData?.ExplosionDamage ?? 100f;
	protected float ExplosionDuration => WeaponData?.ExplosionDuration ?? 0.05f;
	protected float ExplosionMaxVoxels => WeaponData?.ExplosionMaxVoxels ?? 15f;

	[ConVar( "ch_grenade_debug", Help = "Show grenade debug info" )] public static bool GrenadeDebug { get; set; } = false;
	[Property, ReadOnly, Feature( "Debug" )] public bool IsCooking { get; private set; }
	[Property, ReadOnly, Feature( "Debug" )] public float CookStartTime { get; private set; }

	/// <summary> Seconds cooked off the fuse so far. </summary>
	[Property, ReadOnly, Feature( "Debug" )] public float CookedSeconds => IsCooking ? (Time.Now - CookStartTime) : 0f;
	[Property, ReadOnly, Feature( "Debug" )] public float CurrentCookedFuse => IsCooking ? FuseTime - CookedSeconds : FuseTime;

	private class ThrownGrenade
	{
		public GameObject GrenadeGO;
		public float RemainingFuse;
	}

	private readonly List<ThrownGrenade> _liveGrenades = [];

	protected override bool ReloadsSingly => false;
	protected override bool IsProjectile => true;
	protected override void StartReload( bool _ = true ) { }
	protected override void FinishReload( bool _ = true ) { }
	protected override void EjectShells() { }
	protected override void CreateMuzzleFlash() { }

	protected virtual bool HasGrenades() => PrimaryAmmoLoaded >= 0;


	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( GrenadeDebug )
			DrawDebug();
	}

	protected override void HandleWeaponInput()
	{
		if ( Input.Pressed( "attack1" ) )
			BeginCook();

		// Throw if we're cooking
		if ( Input.Released( "attack1" ) )
			Throw();

		// Overcook check
		if ( IsCooking && CurrentCookedFuse <= 0 )
			OnOvercooked();

		// Secondary if it exists (edge cases)
		if ( Input.Pressed( "attack2" ) && _nextSecondaryAttack <= Time.Now )
		{
			SecondaryAttack();
			_nextSecondaryAttack = Time.Now + GetSecondaryFireRate();
		}
	}

	protected override void HandleWeaponStates()
	{
		for ( int i = _liveGrenades.Count - 1; i >= 0; i-- )
		{
			var tg = _liveGrenades[i];
			if ( !tg.GrenadeGO.IsValid() )
			{
				_liveGrenades.RemoveAt( i );
				continue;
			}

			tg.RemainingFuse -= Time.Delta;
			if ( tg.RemainingFuse <= 0f )
			{
				ExplodeLiveGrenade( tg.GrenadeGO );
				_liveGrenades.RemoveAt( i );
			}
		}
	}

	/// <summary>
	/// Pull the pin and start the fuse burning.
	/// </summary>
	protected virtual void BeginCook()
	{
		if ( IsCooking )
			return;

		if ( !AttackConditions() )
			return;

		if ( !HasGrenades() )
			return;

		IsCooking = true;
		CookStartTime = Time.Now;

		Owner.Player?.SetAllAnimgraphParams( "b_attack1", true );
		OnBeginCook();
	}

	/// <summary>
	/// Called the frame cooking starts.
	/// </summary>
	protected virtual void OnBeginCook() { }

	/// <summary>
	/// Normal cook-and-release throw. Fuse is shortened by however long was cooked.
	/// </summary>
	protected virtual void Throw()
	{
		if ( !IsCooking ) return;

		// Fuse left = total fuse - time cooked
		float fuseLeft = FuseTime - CookedSeconds;
		fuseLeft = MathF.Max( fuseLeft, 0f ); // clamp to 0

		FinishAndSpawn( fuseLeft, ThrowForce );
	}

	/// <summary>
	/// Skip cooking entirely: full fuse, thrown immediately.
	/// </summary>
	public virtual void QuickThrow()
	{
		if ( !AttackConditions() ) return;
		if ( !HasGrenades() ) return;

		FinishAndSpawn( FuseTime, ThrowForce );
	}

	/// <summary>
	/// Shared path for all throw variants: consume reserve ammo, sync HUD, then
	/// spawn a live grenade in the world via SpawnLiveGrenade().
	/// </summary>
	protected void FinishAndSpawn( float fuse, float force )
	{
		IsCooking = false;

		if ( !BasePlayer.InfiniteAmmo )
			PrimaryAmmoLoaded = Math.Max( 0, PrimaryAmmoLoaded - 1 );

		Owner.Player?.SetAllAnimgraphParams( "b_attack1", false );

		SpawnLiveGrenade( fuse, force );
	}

	/// <summary>
	/// Spawns a physics grenade object in the world, waits for the
	/// fuse to expire, then calls Explode() at its final resting position.
	/// Override if you need custom behaviour (like sticky, bouncing, etc).
	/// </summary>
	protected virtual void SpawnLiveGrenade( float fuse, float force )
	{
		var go = Scene.CreateObject();

		go.Name = WeaponData?.Name ?? "Grenade";
		go.Tags.Add( "grenade" );

		// Model + collider
		if ( WeaponData?.WeaponWorldmodel is not null )
		{
			var renderer = go.Components.Create<ModelRenderer>();
			renderer.Model = WeaponData.WeaponWorldmodel;

			var collider = go.Components.Create<ModelCollider>();
			collider.Model = WeaponData.WeaponWorldmodel;
			collider.Tags.Add( "debris" );
			collider.Tags.Add( "grenade" );
		}
		else
		{
			var collider = go.Components.Create<SphereCollider>();
			collider.Tags.Add( "debris" );
			collider.Radius = 3f;
		}

		var rb = go.Components.Create<Rigidbody>();
		rb.Tags.Add( "item" );

		var eye = Owner.Player.GetEyeTransform();
		go.WorldPosition = eye.Position + (eye.Forward * 16);
		go.WorldRotation = eye.Rotation;

		var playerVelocity = Owner.Player?.Movement?.Velocity ?? Vector3.Zero;
		go.Components.Get<Rigidbody>().Velocity = eye.Forward * force + playerVelocity;
		go.Components.Get<Rigidbody>().AngularVelocity = Vector3.Random.Normal * 4f;

		// Track the grenade
		_liveGrenades.Add( new ThrownGrenade
		{
			GrenadeGO = go,
			RemainingFuse = fuse
		} );
	}

	private void ExplodeLiveGrenade( GameObject go )
	{
		if ( !go.IsValid() )
			return;

		var explodePos = go.WorldPosition;

		var tr = Game.SceneTrace.Ray(
			explodePos,
			explodePos + Vector3.Up * 16f
		).WithoutTags( "grenade" )
		 .IgnoreGameObject( this.GameObject )
		 .Run();

		if ( tr.Hit )
			explodePos = tr.EndPosition + tr.Normal * 2f;

		Explode( explodePos );

		go.Destroy();
	}

	/// <summary>
	/// Called when the fuse expires.
	/// </summary>
	protected virtual void Explode( Vector3 origin )
	{
		switch ( WeaponData?.ExplosionType )
		{
			case ExplosionType.Volumetric:
				ExplodeVolumetric( origin );
				break;

			default:
				ExplodeTrace( origin );
				break;
		}

		SpawnExplosionEffect( origin );
	}

	/// <summary>
	/// Traditional sphere overlap explosion.
	/// </summary>
	protected virtual void ExplodeTrace( Vector3 origin )
	{
		var radius = ExplosionRadius;
		var damage = ExplosionDamage;
		var pushBack = WeaponData?.ExplosionPushBack;
		var curve = WeaponData?.ExplosionCurve;

		var damagedObjects = new HashSet<GameObject>();

		// Collect everything in radius
		var hits = Game.SceneTrace.Sphere( radius, origin, origin ).RunAll();

		foreach ( var hit in hits )
		{
			if ( !hit.GameObject.IsValid() ) continue;

			// LOS check: skip if solid geometry is in the way
			var los = Game.SceneTrace.Ray( origin, hit.GameObject.WorldPosition )
				.WithoutTags( "grenade", "player" )
				.Run();

			if ( los.Hit && los.GameObject != hit.GameObject ) continue;

			hit.GameObject.Components.TryGet<IDamageable>(
				out var dmgTarget, FindMode.EverythingInSelfAndParent );

			if ( dmgTarget is null ) continue;

			var root = hit.GameObject.Root;
			if ( damagedObjects.Contains( root ) ) continue;
			damagedObjects.Add( root );

			var delta = hit.GameObject.WorldPosition - origin;
			var dist = delta.Length;
			var t = MathX.Clamp( dist / radius, 0f, 1f );

			var damageFraction = curve?.Evaluate( 1f - t ) ?? (1f - t); // closer = more damage
			var forceFraction = pushBack?.Evaluate( 1f - t ) ?? (1f - t);

			var force = delta.Normal * forceFraction * radius * 6.5f;

			// Extra vertical lift: players actually get launched
			if ( root.Tags.Has( "player" ) )
			{
				float lift = MathF.Max( 0.25f, 1f - MathX.Clamp( delta.z / radius, 0f, 1f ) );
				force += Vector3.Up * forceFraction * radius * 6.5f * lift;
			}

			dmgTarget.OnDamage( new CoreDamageInfo
			{
				Attacker = GameObject,
				Weapon = GameObject,
				Damage = damage * damageFraction,
				Force = force,
				Tags = { "explosion", "bullet" },
				Position = origin
			} );
		}
	}

	/// <summary>
	/// Voxel-wave explosion, respects solid geometry occlusion.
	/// </summary>
	protected virtual void ExplodeVolumetric( Vector3 origin )
	{
		var radius = ExplosionRadius;
		var voxelSize = MathF.Max( radius * 2f / ExplosionMaxVoxels, 0.5f );
		var dim = (int)MathF.Ceiling( radius * 2f / voxelSize );

		var volume = new BaseVoxelVolume<byte>(
			center: origin,
			voxelSize: voxelSize,
			dimX: dim,
			dimY: dim,
			dimZ: dim
		);
		volume.PrecomputeVoxelWorldPositions();

		var dmgtype = new DamageTagSet();

		ExplosionSystem.DoExplosionWave(
			origin: origin,
			maxRadius: radius,
			baseDamage: ExplosionDamage,
			ExplosionDuration: ExplosionDuration,
			voxelVolume: volume,
			explosionSource: GameObject,
			explosionCurve: WeaponData?.ExplosionCurve,
			dmgType: dmgtype,
			rate: 1.0f,
			isDebug: GrenadeDebug,
			PushForce: WeaponData?.ExplosionPushBack,
			damagedObjects: new HashSet<GameObject>()
		);
	}

	/// <summary>
	/// Spawn the explosion related effects at a given position.
	/// </summary>
	protected virtual void SpawnExplosionEffect( Vector3 origin )
	{
		if ( WeaponData?.ExplosionParticle is not null )
		{
			var go = SceneUtility.GetPrefabScene( WeaponData.ExplosionParticle )
			.Clone( origin );
		}
#if FMOD
		FMODSound.Play( "event:/Weapons/3P/Explosion", origin );
#endif
	}

	/// <summary>
	/// Player held past MaxCookTime. Either explodes in hand or auto-throws.
	/// </summary>
	protected virtual void OnOvercooked()
	{
		if ( ExplodeOnOvercook )
		{
			IsCooking = false;
			ExplodeInHand();
		}
		else
		{
			Throw(); // auto-throw; fuse may already be 0
		}
	}

	/// <summary>
	/// Grenade exploded while held — spawn at player origin with zero fuse.
	/// </summary>
	protected virtual void ExplodeInHand() => FinishAndSpawn( 0f, 0f );

	/// <summary>
	/// Override per grenade type (underhand lob, roll, smoke pop, etc.)
	/// Not triggered unless UsesSecondary() returns true on the .wpn.
	/// </summary>
	protected override void SecondaryAttack()
	{
		if ( !AttackConditions() ) return;
		if ( !UsesSecondary() ) return;
	}

	protected float TryGetCustomData( string key, float fallback )
	{
		if ( WeaponData?.CustomDataFloat is not null && WeaponData.CustomDataFloat.TryGetValue( key, out float val ) )
			return val;
		return fallback;
	}

	private void DrawDebug()
	{
		float x = Screen.Width - 320f;
		float y = 20f;

		var scope = new TextRendering.Scope
		{
			FontName = "RobotoMono",
			FontSize = 12f,
			TextColor = Color.White,
			LineHeight = 0.85f
		};

		scope.Outline.Enabled = true;
		scope.Outline.Color = Color.Black;
		scope.Outline.Size = 3.25f;

		scope.Text += "GRENADE DEBUG\n\n";

		// --- Core State ---
		scope.Text += "State:\n";
		scope.Text += $"   IsCooking:        {IsCooking}\n";
		scope.Text += $"   CookedSeconds:    {CookedSeconds:F2}\n";
		scope.Text += $"   Current Cooked Fuse: {CurrentCookedFuse:F2}\n";
		scope.Text += $"   Live Grenades: {_liveGrenades.Count}\n";
		for ( int i = 0; i < _liveGrenades.Count; i++ )
			scope.Text += $"     [{i + 1}]: Fuse {_liveGrenades[i].RemainingFuse:F2}\n";
		scope.Text += "\n";

		// --- Ammo ---
		scope.Text += "Ammo:\n";
		scope.Text += $"   InfiniteAmmo:     {BasePlayer.InfiniteAmmo}\n";
		scope.Text += $"   PrimaryLoaded:    {PrimaryAmmoLoaded}\n";
		scope.Text += $"   HasGrenades():    {HasGrenades()}\n";
		scope.Text += "\n";

		// --- Conditions ---
		bool attackOk = AttackConditions();

		scope.Text += "Conditions:\n";
		scope.Text += $"   AttackConditions: {attackOk}\n";
		scope.Text += "\n";

		// --- Timing ---
		scope.Text += "Timing:\n";
		scope.Text += $"   Time.Now:         {Time.Now:F2}\n";
		scope.Text += $"   CookStartTime:    {CookStartTime:F2}\n";
		scope.Text += $"   MaxCookTime:      {MaxCookTime:F2}\n";
		scope.Text += "\n";

		// --- Weapon Data ---
		scope.Text += "WeaponData:\n";
		scope.Text += $"   IsValid():            {WeaponData.IsValid()}\n";

		if ( WeaponData.IsValid() )
		{
			scope.Text += $"   Name:             {WeaponData.Name}\n";
			scope.Text += $"   ExplosionRadius:  {ExplosionRadius:F1}\n";
			scope.Text += $"   ExplosionDamage:  {ExplosionDamage:F1}\n";
		}
		else
		{
			scope.Text += "   NULL (!!!)\n";
		}

		scope.Text += "\n";

		DebugOverlaySystem.Current.ScreenText(
			new Vector2( x, y ),
			scope,
			TextFlag.Left,
			0f
		);
	}
}
