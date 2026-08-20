#if FMOD
using FMODSbox;
#endif
namespace Core;

public partial class GameProp
{
	public enum PropEffectOverride
	{
		[Icon( "data_array" )]
		[Description( "Use prop data to determine if the effect applies." )]
		UseModelData,
		[Icon( "motion_photos_on" )]
		[Description( "This prop always does the effect." )]
		Always,
		[Icon( "rule" )]
		[Description( "This prop does the effect based on a script. Use the condition property to control this." )]
		Conditional,
		[Icon( "motion_photos_off" )]
		[Description( "This prop never does the effect." )]
		Never
	}

	/// <summary>
	/// True if this prop can be set on fire.
	/// </summary>
#if IGNIS
	[DebugExpose]
#endif
	[Feature( "Debug" ), Property]
	public bool IsFlammable => FlammableOverride switch
	{
		PropEffectOverride.Always => true,
		PropEffectOverride.Conditional => FlammableCondition,
		PropEffectOverride.Never => false,
		_ => ModelFlammable
	};

	/// <summary>
	/// True if this prop will explode when destroyed.
	/// </summary>
#if IGNIS
	[DebugExpose]
#endif
	[Feature( "Debug" ), Property]
	public bool IsExplosive => ExplosiveOverride switch
	{
		PropEffectOverride.Always => true,
		PropEffectOverride.Conditional => ExplosiveCondition,
		PropEffectOverride.Never => false,
		_ => ModelExplosive
	};

	/// <summary>
	/// Override flammable behavior.
	/// </summary>
	[Sync]
	[Group( "Breakable Properties" ), Property, Order( 13 )]
	public PropEffectOverride FlammableOverride { get; set; } = PropEffectOverride.UseModelData;

	[Sync]
	[Group( "Breakable Properties" ), Property, Order( 13 ), ShowIf( nameof( FlammableOverride ), PropEffectOverride.Conditional )]
	public bool FlammableCondition { get; set; } = false;

	/// <summary>
	/// Override explosive behavior.
	/// </summary>
	[Sync]
	[Group( "Breakable Properties" ), Property, Order( 13 )]
	public PropEffectOverride ExplosiveOverride { get; set; } = PropEffectOverride.UseModelData;

	[Sync]
	[Group( "Breakable Properties" ), Property, Order( 13 ), ShowIf( nameof( ExplosiveOverride ), PropEffectOverride.Conditional )]
	public bool ExplosiveCondition { get; set; } = false;

	[Sync]
	[Group( "Breakable Properties" ), Property, Order( 13 ), ShowIf( nameof( UseAuthoredExplosiveData ), true )]
	public float ExplosionDamageOverride { get; set; } = -1f;

	[Sync]
	[Group( "Breakable Properties" ), Property, Order( 13 ), ShowIf( nameof( UseAuthoredExplosiveData ), true )]
	public float ExplosionRadiusOverride { get; set; } = -1f;

	[Sync]
	[Group( "Breakable Properties" ), Property, Order( 13 ), Title( "Force Scale" ), ShowIf( nameof( UseAuthoredExplosiveData ), true )]
	public float ExplosionForceOverride { get; set; } = -1f;

	[Sync]
	[Group( "Breakable Properties" ), Property, Order( 13 ), ShowIf( nameof( UseAuthoredExplosiveData ), true )]
	public float MinImpactDamageSpeedOverride { get; set; } = -1f;

	[Sync]
	[Group( "Breakable Properties" ), Property, Order( 13 ), ShowIf( nameof( UseAuthoredExplosiveData ), true )]
	public float ImpactDamageOverride { get; set; } = -1f;

	[Sync]
	public bool IsOnFire { get; protected set; }

#if IGNIS
	[DebugExpose]
#endif
	[Feature( "Debug" ), Group( "Velocity" ), Property, ReadOnly, ShowIf( nameof( _hasRigidbody ), true )]
	public Vector3 CurrentVelocity => Components.Get<Rigidbody>() is { } rigidBody && rigidBody.IsValid() ? rigidBody.Velocity : default;
#if IGNIS
	[DebugExpose]
	[Feature( "Debug" ), Group( "Velocity" ), Property, ReadOnly, ShowIf( nameof( _hasRigidbody ), true )]
	public Vector3 PreImpactVelocity => Components.Get<Rigidbody>() is { } rigidBody && rigidBody.IsValid() ? rigidBody.PreVelocity : default;
	[DebugExpose]
	[Feature( "Debug" ), Group( "Velocity" ), Property, ReadOnly, ShowIf( nameof( _hasRigidbody ), true )]
	public Vector3 PreImpactAngularVelocity => Components.Get<Rigidbody>() is { } rigidBody && rigidBody.IsValid() ? rigidBody.PreAngularVelocity : default;

	[DebugExpose]
#endif
	[Feature( "Debug" ), Group( "Velocity" ), Property, ReadOnly, ShowIf( nameof( _hasRigidbody ), true )]
	public float CurrentSpeed => CurrentVelocity.Length;
#if IGNIS
	[DebugExpose]

	[Feature( "Debug" ), Group( "Velocity" ), Property, ReadOnly, ShowIf( nameof( _hasRigidbody ), true )]
	public float PreImpactSpeed => PreImpactVelocity.Length;

	[DebugExpose]
#endif
	[Feature( "Debug" ), Group( "Velocity" ), Property, ReadOnly, ShowIf( nameof( _hasRigidbody ), true )]
	public float ImpactSpeedThreshold => ResolvedMinImpactDamageSpeed;

#if IGNIS
	[DebugExpose]
#endif
	[Feature( "Debug" ), Group( "Velocity" ), Property, ReadOnly, ShowIf( nameof( _hasRigidbody ), true )]
	public float ImpactDamageAmount => ResolvedImpactDamage;

#if IGNIS
	[DebugExpose]
#endif
	[Feature( "Debug" ), Group( "Velocity" ), Property, ReadOnly, ShowIf( nameof( _hasRigidbody ), true )]
	public float LastImpactSpeed { get; private set; }
#if IGNIS
	[DebugExpose]
#endif
	[Feature( "Debug" ), Group( "Velocity" ), Property, ReadOnly, ShowIf( nameof( _hasRigidbody ), true )]
	public bool LastImpactPassedThreshold { get; private set; }

	private bool ModelFlammable => Model?.Data.Flammable ?? false;
	private bool ModelExplosive => Model?.Data.Explosive ?? false;

	[Property, Hide]
	protected bool UseAuthoredExplosiveData =>
		ExplosiveOverride is PropEffectOverride.Always or PropEffectOverride.Conditional;

	private float AuthoredOrModelExplosionDamage =>
		UseAuthoredExplosiveData ? ExplosionDamageOverride : (Model?.Data.ExplosionDamage ?? -1f);
	private float AuthoredOrModelExplosionRadius =>
		UseAuthoredExplosiveData ? ExplosionRadiusOverride : (Model?.Data.ExplosionRadius ?? -1f);
	private float AuthoredOrModelExplosionForce =>
		UseAuthoredExplosiveData ? ExplosionForceOverride : (Model?.Data.ExplosionForce ?? -1f);
	private float AuthoredOrModelMinImpactDamageSpeed =>
		UseAuthoredExplosiveData ? MinImpactDamageSpeedOverride : (Model?.Data.MinImpactDamageSpeed ?? -1f);
	private float AuthoredOrModelImpactDamage =>
		UseAuthoredExplosiveData ? ImpactDamageOverride : (Model?.Data.ImpactDamage ?? -1f);
	private float ResolvedExplosionDamage =>
		AuthoredOrModelExplosionDamage <= 0 ? 80 : AuthoredOrModelExplosionDamage;
	private float ResolvedExplosionRadius =>
		AuthoredOrModelExplosionRadius <= 0 ? 256 : AuthoredOrModelExplosionRadius;
	private float ResolvedExplosionForce =>
		AuthoredOrModelExplosionForce <= 0 ? 1 : AuthoredOrModelExplosionForce;
	private float ResolvedMinImpactDamageSpeed =>
		AuthoredOrModelMinImpactDamageSpeed <= 0 ? 500f : AuthoredOrModelMinImpactDamageSpeed;
	private float ResolvedImpactDamage =>
		AuthoredOrModelImpactDamage <= 0 ? 10f : AuthoredOrModelImpactDamage;

	bool CanIgniteFromDamage( in DamageInfo damage )
	{
		if ( IsOnFire )
			return false;

		return IsFlammable && ShouldDamageIgnite( damage );
	}

	bool ShouldDetonateFromDamage( in DamageInfo damage )
	{
		return IsExplosive && IsStrongImpact( damage );
	}

	bool IsStrongImpact( in DamageInfo damage )
	{
		if ( !damage.Tags.Contains( "impact" ) )
			return false;

		LastImpactSpeed = GetImpactSpeed();
		LastImpactPassedThreshold = LastImpactSpeed >= ResolvedMinImpactDamageSpeed;

		return LastImpactPassedThreshold;
	}

	private float GetImpactSpeed()
	{
		var rigidBody = Components.Get<Rigidbody>();
		if ( !rigidBody.IsValid() )
			return 0f;
#if IGNIS
		return rigidBody.PreVelocity.Length;
#else
		return rigidBody.Velocity.Length;
#endif
	}

	private bool ShouldCreateExplosionOnBreak()
	{
		return ExplosiveOverride switch
		{
			PropEffectOverride.Always => true,
			PropEffectOverride.Conditional => ExplosiveCondition,
			PropEffectOverride.Never => false,
			_ => ModelExplosive
		};
	}

	private bool ShouldDamageIgnite( in DamageInfo damage )
	{
		// Physics impacts only ignite if they do lots of damage
		if ( damage.Tags.Contains( "impact" ) )
		{
			return damage.Damage > Health * 0.5f;
		}

		return true;
	}

	public void Ignite()
	{
		if ( Scene.IsEditor )
			return;

		if ( IsProxy ) return;
		if ( IsOnFire ) return;

		IsOnFire = true;

		var firePrefab = ResourceLibrary.Get<PrefabFile>( "/prefabs/engine/ignite.prefab" );
		if ( firePrefab == null )
		{
			Log.Warning( "Can't find /prefabs/engine/ignite.prefab" );
			return;
		}

		// Spawn it, and send it to children on the network
		var fire = GameObject.Clone( firePrefab, new CloneConfig { Parent = GameObject, Transform = global::Transform.Zero, StartEnabled = true } );
		if ( !fire.IsValid() ) return;

		OnPropIgnite?.Invoke( null );

		fire.RunEvent<ParticleModelEmitter>( x => x.Target = GameObject );

		if ( Network.Active )
		{
			fire.Network.Refresh( fire );
		}
	}

	public void CreateExplosion()
	{
		if ( Scene.IsEditor || !ShouldCreateExplosionOnBreak() )
			return;

		var radius = ResolvedExplosionRadius;
		var damage = ResolvedExplosionDamage;
		var force = ResolvedExplosionForce;

		var explosionPrefab = ResourceLibrary.Get<PrefabFile>( "/prefabs/game/particles/explosion_med.prefab" );
		if ( !explosionPrefab.IsValid() )
		{
			Log.Warning( "Can't find /prefabs/game/particles/explosion_med.prefab" );
			return;
		}

		// Spawn it, and send it to children on the network
		var go = GameObject.Clone( explosionPrefab, new CloneConfig { Transform = WorldTransform.WithScale( 1 ), StartEnabled = false } );
		if ( !go.IsValid() ) return;

		go.Tags.Add( "debris", "particle" );

		OnPropExplode?.Invoke( null );
#if FMOD
		FMODSound.Play( "event:/Weapons/Explosion", go.WorldPosition );
#endif
		// set up the damage appropriately
		go.RunEvent<RadiusDamage>( x =>
		{
			x.Radius = radius;
			x.PhysicsForceScale = force;
			x.DamageAmount = damage;
			x.Attacker = LastAttacker;
			x.DamageTags?.Add( "explosion" );

		}, FindMode.EverythingInSelfAndDescendants );

		go.Parent = DebrisManager.Instance.GameObject;
		go.Enabled = true;
		go.NetworkSpawn( true, null );
	}

	private void PlayBreakSound()
	{
		if ( _proceduralComponents is null )
			return;

		if ( !Components.TryGet<Rigidbody>( out var rigidBody ) || !rigidBody.PhysicsBody.IsValid() )
			return;

		var surfaces = rigidBody.PhysicsBody.Shapes
			.Select( x => x.Surface )
			.Distinct();

		foreach ( var surface in surfaces )
		{
			if ( !surface.IsValid() )
				continue;

#if FMOD
			var sound = "event:/Physics/Break";

			var surf = surface?.SoundCollection.SurfaceParameter;

			var s = FMODSound.Play( sound, WorldPosition );
			if ( !string.IsNullOrWhiteSpace( surf ) )
				FMODSound.SetParameter( s, "parameter:/Physics/MaterialType", surf );

#else
			var sound = surface.SoundCollection.Break;
			if ( !sound.IsValid() )
				continue;

			Sound.Play( sound, WorldPosition );
#endif

		}
	}
}
