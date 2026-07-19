#if IGNIS
using Sandbox.Rendering;
using System;
using System.Threading;

namespace Core;

public class ParticleDecal : Particle.BaseListener
{
	private readonly ParticleDecalRenderer _renderer;
	private readonly DecalDefinition _selectedDecal;
	private readonly List<ActiveDecal> _activeDecals = [];
	private static int _nextDebugKey;
	private readonly int _debugKey;

	private bool _spawnedAny;
	private uint _spawnCounter;
	private float _lastCollisionTime = -1.0f;
	private Vector3 _lastCollisionPos;
	private bool _hasCachedCollisionLock;
	private CollisionLock _cachedCollisionLock;
	private bool _lastPassedFilters = true;
	private string _lastStatus = "created";
	private string _lastHitObjectName = "<none>";
	private Vector3 _lastLockedPosition;
	private Vector3 _lastLockedNormal = Vector3.Up;
	private Rotation _lastLockedRotation = Rotation.Identity;
	private Vector3 _lastLockedVolume = Vector3.Zero;

	private struct CollisionLock
	{
		public Vector3 Position;
		public Vector3 Normal;
		public string HitObjectName;
		public GameObject HitObject;
		public Vector3 LocalPosition;
		public Vector3 LocalNormal;
		public bool StickToHitObject;
	}

	private sealed class ActiveDecal
	{
		public DecalSceneObject SceneObject;
		public CollisionLock Lock;
		public uint SpawnIndex;
		public float ScaleMul = 1.0f;
		public float RotationJitter;
		public float BrightnessMul = 1.0f;
		public float ColorMixMul = 1.0f;
		public float ParallaxMul = 1.0f;
	}

	public ParticleDecal( ParticleDecalRenderer renderer, DecalDefinition selectedDecal )
	{
		_renderer = renderer;
		_selectedDecal = selectedDecal;
		_debugKey = Interlocked.Increment( ref _nextDebugKey );
	}

	public override void OnEnabled( Particle p )
	{
		if ( _renderer.ShouldCollectDebugState() )
			_renderer.ClearDebugState( _debugKey );

		_spawnedAny = false;
		_spawnCounter = 0;
		_lastCollisionTime = -1.0f;
		_lastCollisionPos = Vector3.Zero;
		_hasCachedCollisionLock = false;
		_cachedCollisionLock = default;
		_lastPassedFilters = true;
		_lastStatus = "enabled";
		_lastHitObjectName = "<none>";
		_lastLockedPosition = Vector3.Zero;
		_lastLockedNormal = Vector3.Up;
		_lastLockedRotation = Rotation.Identity;
		_lastLockedVolume = Vector3.Zero;
		DeleteAllActiveDecals();
	}

	public override void OnDisabled( Particle p )
	{
		bool diedOnCollision = p.HitTime > 0 && p.HitNormal.LengthSquared > 0.0001f;

		if ( diedOnCollision )
		{
			// Collision death persistence is controlled solely by SpawnOnCollisionDeath.
			if ( !_renderer.SpawnOnCollisionDeath )
			{
				_renderer.ClearDebugState( _debugKey );
				DeleteAllActiveDecals();
				return;
			}

			// Seed one at collision death if none exists yet.
			if ( !_spawnedAny )
			{
				if ( _hasCachedCollisionLock )
				{
					SpawnDecal( in p, in _cachedCollisionLock );
				}
				else if ( TryGetCollisionLock( p, true, out var lockData )
					|| (_renderer.ForceSpawnOnDisableCollision && TryGetDisableFallbackLock( p, out lockData )) )
				{
					SpawnDecal( in p, in lockData );
				}
			}
		}
		else if ( !_renderer.PersistOnDeath )
		{
			_renderer.ClearDebugState( _debugKey );
			DeleteAllActiveDecals();
			return;
		}

		if ( _renderer.ShouldCollectDebugState() )
			_renderer.PersistDebugState( _debugKey );

		if ( _renderer.LifetimeMode != ParticleDecalRenderer.DecalLifetimeMode.OwnLifetime )
		{
			_renderer.ClearDebugState( _debugKey );
			DeleteAllActiveDecals();
			return;
		}

		float life = _renderer.DecalLifetime.Evaluate( p, 7341 );
		if ( !float.IsFinite( life ) ) life = 0.0f;
		life = MathF.Max( life, 0.0f );

		for ( int i = 0; i < _activeDecals.Count; i++ )
		{
			var active = _activeDecals[i];
			var so = active.SceneObject;
			if ( so is null || !so.IsValid() )
				continue;

			float pseudoBump = MathF.Max( _renderer.PseudoBumpStrength, 0.0f );
			float bumpOver = MathF.Max( pseudoBump - 1.0f, 0.0f );
			float colorSuppression = 1.0f - (MathF.Min( bumpOver, 1.0f ) * Math.Clamp( _renderer.PseudoBumpColorSuppression, 0.0f, 1.0f ));
			float parallaxBoost = 1.0f + (bumpOver * MathF.Max( _renderer.PseudoBumpParallaxBoost, 0.0f ));

			var tx = so.Transform;
			// Apply persistent cull decision immediately on handoff to avoid one-frame pop.
			so.RenderingEnabled = !_renderer.ShouldCullByDistance( tx.Position, persistent: true );

			_renderer.AdoptPersistentDecal(
				so,
				life,
				active.SpawnIndex + 1,
				tx.Position,
				tx.Rotation,
				_renderer.WorldScale,
				active.ScaleMul,
				active.BrightnessMul,
				_selectedDecal.ColorMix * colorSuppression * active.ColorMixMul,
				_selectedDecal.ParallaxStrength * parallaxBoost * active.ParallaxMul,
				_selectedDecal.Width,
				_selectedDecal.Height,
				_selectedDecal.Tint );
		}

		_activeDecals.Clear();
	}

	private void DeleteAllActiveDecals()
	{
		for ( int i = 0; i < _activeDecals.Count; i++ )
		{
			var so = _activeDecals[i].SceneObject;
			if ( so.IsValid() && so.IsValid() )
			{
				_renderer.UnregisterActiveDecal( so );
				so.Delete();
			}
		}

		_activeDecals.Clear();
	}

	private void DeleteActiveDecalAt( int i )
	{
		var so = _activeDecals[i].SceneObject;
		if ( so.IsValid() && so.IsValid() )
		{
			_renderer.UnregisterActiveDecal( so );
			so.Delete();
		}

		_activeDecals.RemoveAt( i );
	}

	private DecalSceneObject CreateSceneObject()
	{
		var so = new DecalSceneObject( _renderer.Scene.SceneWorld );
		so.RenderingEnabled = false;
		so.Color = Color.Transparent;
		so.ColorMix = 0.0f;
		so.ParallaxStrength = 0.0f;
		so.Tags.SetFrom( _renderer.GameObject.Tags );
		so.ColorTexture = _selectedDecal.ColorTexture;
		so.NormalTexture = _selectedDecal.NormalTexture;
		so.RMOTexture = _selectedDecal.RoughMetalOcclusionTexture;
		so.HeightTexture = _selectedDecal.HeightTexture;
		so.EmissionTexture = _selectedDecal.EmissiveTexture;
		so.EmissionEnergy = _selectedDecal.EmissionEnergy;
		so.SamplerIndex = SamplerState.GetBindlessIndex(
			new SamplerState { AddressModeU = TextureAddressMode.Clamp, AddressModeV = TextureAddressMode.Clamp, Filter = _selectedDecal.FilterMode } );
		return so;
	}

	private static bool IsMeaningfulTag( string tag )
	{
		return !string.IsNullOrWhiteSpace( tag )
			&& !string.Equals( tag, "null", StringComparison.OrdinalIgnoreCase );
	}

	private static bool HasMeaningfulTags( ITagSet set )
	{
		if ( set is null )
			return false;

		foreach ( var tag in set.TryGetAll() )
		{
			if ( IsMeaningfulTag( tag ) )
				return true;
		}

		return false;
	}

	private static bool HitHasAnyMeaningfulTag( GameObject hitObject, ITagSet filterSet )
	{
		if ( !hitObject.IsValid() || filterSet is null )
			return false;

		foreach ( var tag in filterSet.TryGetAll() )
		{
			if ( !IsMeaningfulTag( tag ) )
				continue;

			if ( hitObject.Tags.Has( tag ) )
				return true;
		}

		return false;
	}

	private bool PassesTagFilters( GameObject hitObject )
	{
		var hasExclude = HasMeaningfulTags( _renderer.ExcludeHitTags );
		if ( hasExclude && HitHasAnyMeaningfulTag( hitObject, _renderer.ExcludeHitTags ) )
			return false;

		var hasInclude = HasMeaningfulTags( _renderer.IncludeHitTags );
		if ( !hasInclude )
			return true;

		return HitHasAnyMeaningfulTag( hitObject, _renderer.IncludeHitTags );
	}

	private bool ShouldBlockByDieOnCollision( Particle p )
	{
		if ( _renderer.ParticleEffect is null || !_renderer.ParticleEffect.Collision )
			return false;

		float dieChance = _renderer.ParticleEffect.DieOnCollisionChance.Evaluate( p, 4582 );
		dieChance = Math.Clamp( dieChance, 0.0f, 1.0f );

		float roll = p.Rand( 4582 );
		bool particleWouldDie = dieChance > roll;
		if ( !particleWouldDie )
			return false;

		// If this particle would die on collision, defer spawning to OnDisabled
		// so we avoid one-frame active to persistent handoff pops.
		return true;
	}

	private bool WouldDieOnCollision( Particle p )
	{
		if ( _renderer.ParticleEffect is null || !_renderer.ParticleEffect.Collision )
			return false;

		float dieChance = _renderer.ParticleEffect.DieOnCollisionChance.Evaluate( p, 4582 );
		dieChance = Math.Clamp( dieChance, 0.0f, 1.0f );
		return dieChance > p.Rand( 4582 );
	}

	private bool TryGetCollisionLock( Particle p, bool ignoreDieOnCollisionBlock, out CollisionLock lockData )
	{
		lockData = default;

		if ( p.HitTime <= 0 || p.HitNormal.LengthSquared < 0.0001f )
		{
			_lastStatus = "waiting_for_collision";
			_lastPassedFilters = false;
			_lastHitObjectName = "<none>";
			return false;
		}

		if ( !ignoreDieOnCollisionBlock && ShouldBlockByDieOnCollision( p ) )
		{
			_lastStatus = "blocked_by_die_on_collision";
			_lastPassedFilters = false;
			_lastHitObjectName = "<particle_hit>";
			return false;
		}

		var n = p.HitNormal.Normal;
		var hasInclude = HasMeaningfulTags( _renderer.IncludeHitTags );
		var hasExclude = HasMeaningfulTags( _renderer.ExcludeHitTags );
		var needTrace = hasInclude || hasExclude || _renderer.StickToHitObjects;
		SceneTraceResult hitTrace = default;

		if ( needTrace )
		{
			hitTrace = _renderer.Scene.Trace
				.Ray( p.HitPos + (n * 2.0f), p.HitPos - (n * 2.0f) )
				.Radius( 1.0f )
				.UsePhysicsWorld( true )
				.UseHitPosition( true )
				.Run();

			_lastHitObjectName = hitTrace.GameObject?.Name ?? "<none>";
		}
		else
		{
			_lastHitObjectName = "<particle_hit>";
		}

		if ( hasInclude || hasExclude )
		{
			if ( !PassesTagFilters( hitTrace.GameObject ) )
			{
				_lastStatus = "blocked_by_tag_filter";
				_lastPassedFilters = false;
				return false;
			}
		}

		lockData.Position = p.HitPos;
		lockData.Normal = -n;
		lockData.HitObjectName = _lastHitObjectName;

		if ( _renderer.StickToHitObjects && hitTrace.Hit && hitTrace.GameObject.IsValid() )
		{
			var hitObjectTx = hitTrace.GameObject.WorldTransform;
			lockData.HitObject = hitTrace.GameObject;
			lockData.LocalPosition = hitObjectTx.PointToLocal( lockData.Position );
			lockData.LocalNormal = hitObjectTx.NormalToLocal( lockData.Normal ).Normal;
			lockData.StickToHitObject = true;
		}

		_lastStatus = lockData.StickToHitObject ? "locked_following_object" : "locked_world";
		_lastPassedFilters = true;
		return true;
	}

	private bool TryGetDisableFallbackLock( Particle p, out CollisionLock lockData )
	{
		lockData = default;

		Vector3 dir = p.Velocity.LengthSquared > 0.0001f ? (-p.Velocity).Normal : Vector3.Down;
		float radius = MathF.Max( p.Radius, 0.5f );

		var trace = _renderer.Scene.Trace
			.Ray( p.Position - (dir * 1.0f), p.Position + (dir * 24.0f) )
			.Radius( radius )
			.UsePhysicsWorld( true )
			.UseHitPosition( true )
			.Run();

		if ( !trace.Hit || trace.Normal.LengthSquared < 0.0001f )
		{
			_lastStatus = "disable_fallback_no_hit";
			_lastPassedFilters = false;
			_lastHitObjectName = "<none>";
			return false;
		}

		_lastHitObjectName = trace.GameObject?.Name ?? "<none>";

		if ( !PassesTagFilters( trace.GameObject ) )
		{
			_lastStatus = "disable_fallback_blocked_by_tag_filter";
			_lastPassedFilters = false;
			return false;
		}

		lockData.Position = trace.HitPosition;
		lockData.Normal = -trace.Normal.Normal;
		lockData.HitObjectName = _lastHitObjectName;
		if ( _renderer.StickToHitObjects && trace.GameObject.IsValid() )
		{
			var hitObjectTx = trace.GameObject.WorldTransform;
			lockData.HitObject = trace.GameObject;
			lockData.LocalPosition = hitObjectTx.PointToLocal( lockData.Position );
			lockData.LocalNormal = hitObjectTx.NormalToLocal( lockData.Normal ).Normal;
			lockData.StickToHitObject = true;
		}
		_lastStatus = "disable_fallback_locked";
		_lastPassedFilters = true;
		return true;
	}

	private bool IsNewCollisionEvent( in Particle p )
	{
		if ( _lastCollisionTime < 0.0f )
			return true;

		if ( MathF.Abs( p.HitTime - _lastCollisionTime ) > 0.0001f )
			return true;

		if ( p.HitPos.Distance( _lastCollisionPos ) > 0.25f )
			return true;

		return false;
	}

	private void RegisterCollisionEvent( in Particle p )
	{
		_lastCollisionTime = p.HitTime;
		_lastCollisionPos = p.HitPos;
	}

	private static float NextSignedRand( in Particle p, uint spawnIndex, int seed )
	{
		var s = seed + (int)(spawnIndex * 13u);
		return (p.Rand( s ) * 2.0f) - 1.0f;
	}

	private void UpdateDebugLockPreview( in Particle p, in CollisionLock lockData )
	{
		float depth = MathF.Max( 0.01f, _renderer.MaxProjectionDepth - _renderer.MinProjectionDepth );
		float scale = _renderer.Scale.Evaluate( p, 238 );
		var size = new Vector3( depth, _selectedDecal.Width * scale, _selectedDecal.Height * scale );
		var pos = lockData.Position;
		var normal = lockData.Normal;

		if ( lockData.StickToHitObject && lockData.HitObject.IsValid() )
		{
			var hitObjectTx = lockData.HitObject.WorldTransform;
			pos = hitObjectTx.PointToWorld( lockData.LocalPosition );
			normal = hitObjectTx.NormalToWorld( lockData.LocalNormal ).Normal;
		}

		var tx = new Transform( pos + (normal * _renderer.SurfaceBias), normal.EulerAngles.ToRotation(), _renderer.WorldScale * size );
		tx.Rotation *= new Angles( 0, 0, _renderer.Rotation.Evaluate( p, 512 ) ).ToRotation();

		_lastLockedPosition = tx.Position;
		_lastLockedNormal = normal;
		_lastLockedRotation = tx.Rotation;
		_lastLockedVolume = tx.Scale;
	}

	private void BuildVariation( in Particle p, uint spawnIndex, ActiveDecal active )
	{
		if ( !_renderer.SurfaceVariation )
			return;

		active.RotationJitter = NextSignedRand( in p, spawnIndex, 6112 ) * _renderer.SurfaceYawJitter;
		active.ScaleMul = MathF.Max( 0.01f, 1.0f + (NextSignedRand( in p, spawnIndex, 6113 ) * _renderer.SurfaceScaleJitter) );
		active.BrightnessMul = MathF.Max( 0.0f, 1.0f + (NextSignedRand( in p, spawnIndex, 6114 ) * _renderer.SurfaceBrightnessJitter) );
		active.ColorMixMul = MathF.Max( 0.0f, 1.0f + (NextSignedRand( in p, spawnIndex, 6115 ) * _renderer.SurfaceColorMixJitter) );
		active.ParallaxMul = MathF.Max( 0.0f, 1.0f + (NextSignedRand( in p, spawnIndex, 6116 ) * _renderer.SurfaceParallaxJitter) );
	}

	private void ApplyTransform( DecalSceneObject so, in Particle p, in ActiveDecal active )
	{
		var lockData = active.Lock;
		float depth = MathF.Max( 0.01f, _renderer.MaxProjectionDepth - _renderer.MinProjectionDepth );
		float scale = _renderer.Scale.Evaluate( p, 238 ) * active.ScaleMul;
		var size = new Vector3( depth, _selectedDecal.Width * scale, _selectedDecal.Height * scale );
		var pos = lockData.Position;
		var normal = lockData.Normal;

		if ( lockData.StickToHitObject && lockData.HitObject.IsValid() )
		{
			var hitObjectTx = lockData.HitObject.WorldTransform;
			pos = hitObjectTx.PointToWorld( lockData.LocalPosition );
			normal = hitObjectTx.NormalToWorld( lockData.LocalNormal ).Normal;
		}

		var tx = new Transform( pos + (normal * _renderer.SurfaceBias), normal.EulerAngles.ToRotation(), _renderer.WorldScale * size );
		tx.Rotation *= new Angles( 0, 0, _renderer.Rotation.Evaluate( p, 512 ) + active.RotationJitter ).ToRotation();

		so.Transform = tx;
		_lastLockedPosition = tx.Position;
		_lastLockedNormal = normal;
		_lastLockedRotation = tx.Rotation;
		_lastLockedVolume = tx.Scale;
	}

	private void ApplyVisuals( DecalSceneObject so, in Particle p, in ActiveDecal active, bool enableRendering = true )
	{
		var color = _selectedDecal.Tint;
		color *= _renderer.ConstantColorTint;
		color *= _renderer.ColorTint.Evaluate( p, 928 );

		float brightness = _renderer.Brightness.Evaluate( p, 4626 ) * active.BrightnessMul;
		if ( !float.IsFinite( brightness ) ) brightness = 1.0f;
		brightness = MathF.Max( brightness, 0.0f );

		float alpha = _renderer.Alpha.Evaluate( p, 8525 );
		if ( !float.IsFinite( alpha ) ) alpha = 1.0f;
		alpha = Math.Clamp( alpha, 0.0f, 1.0f );

		color = color.WithColorMultiplied( brightness );
		color = color.WithAlpha( Math.Clamp( color.a * alpha, 0.0f, 1.0f ) );

		float pseudoBump = MathF.Max( _renderer.PseudoBumpStrength, 0.0f );
		float bumpOver = MathF.Max( pseudoBump - 1.0f, 0.0f );
		float colorSuppression = 1.0f - (MathF.Min( bumpOver, 1.0f ) * Math.Clamp( _renderer.PseudoBumpColorSuppression, 0.0f, 1.0f ));
		float parallaxBoost = 1.0f + (bumpOver * MathF.Max( _renderer.PseudoBumpParallaxBoost, 0.0f ));

		so.Color = color;
		so.ColorMix = _selectedDecal.ColorMix * _renderer.ColorMix.Evaluate( p, 324 ) * colorSuppression * active.ColorMixMul;
		so.ParallaxStrength = _selectedDecal.ParallaxStrength * _renderer.Parallax.Evaluate( p, 245 ) * parallaxBoost * active.ParallaxMul * 0.25f;
		so.AttenuationAngle = _renderer.AttenuationAngle;
		if ( enableRendering )
			so.RenderingEnabled = true;
	}

	private uint GetSortBaseId( in CollisionLock lockData )
	{
		GameObject source = _renderer.GameObject;
		if ( _renderer.SortPerHitObject && lockData.HitObject.IsValid() && lockData.HitObject.IsValid() )
			source = lockData.HitObject;

		var bytes = source.Id.ToByteArray();
		return (uint)(bytes[0] | (bytes[1] << 8) | (bytes[2] << 16));
	}

	private uint BuildSortOrder( uint spawnIndex, in CollisionLock lockData )
	{
		const uint mask = 0x00FFFFFF;
		uint baseId = GetSortBaseId( in lockData ) & mask;
		uint step = Math.Max( _renderer.SortPerDecalStep, 1u );
		uint ordinal = (spawnIndex * step) & mask;
		uint low;

		switch ( _renderer.SortMode )
		{
			case ParticleDecalRenderer.DecalSortMode.Stable:
				low = baseId;
				break;
			case ParticleDecalRenderer.DecalSortMode.OldestOnTop:
				low = (baseId + ((mask - ordinal) & mask)) & mask;
				break;
			default:
				low = (baseId + ordinal) & mask;
				break;
		}

		return ((_renderer.SortLayer & 0xFF) << 24) | low;
	}

	private void EnforcePerParticleLimits()
	{
		if ( !_renderer.AllowMultipleActiveDecals )
		{
			while ( _activeDecals.Count > 1 )
				DeleteActiveDecalAt( 0 );
			return;
		}

		var max = Math.Max( _renderer.MaxDecalsPerParticle, 1 );
		while ( _activeDecals.Count > max )
			DeleteActiveDecalAt( 0 );
	}

	private void SpawnDecal( in Particle p, in CollisionLock lockData )
	{
		var so = CreateSceneObject();
		var spawnIndex = _spawnCounter++;
		var active = new ActiveDecal
		{
			SceneObject = so,
			Lock = lockData,
			SpawnIndex = spawnIndex
		};

		BuildVariation( in p, spawnIndex, active );
		ApplyTransform( so, in p, in active );
		ApplyVisuals( so, in p, in active, enableRendering: false );
		bool treatAsPersistent = _renderer.PersistOnDeath;
		so.RenderingEnabled = !_renderer.ShouldCullByDistance( so.Transform.Position, persistent: treatAsPersistent );

		so.SortOrder = BuildSortOrder( spawnIndex, in lockData );

		_activeDecals.Add( active );
		_renderer.RegisterActiveDecal( so );
		_spawnedAny = true;
		EnforcePerParticleLimits();
	}

	public override void OnUpdate( Particle p, float dt )
	{
		if ( _selectedDecal is null )
			return;

		for ( int i = _activeDecals.Count - 1; i >= 0; i-- )
		{
			var so = _activeDecals[i].SceneObject;
			if ( so is null || !so.IsValid() )
			{
				_activeDecals.RemoveAt( i );
				continue;
			}
		}

		bool wouldDieOnCollision = WouldDieOnCollision( p );
		CollisionLock lockData;
		bool hasCollision = TryGetCollisionLock( p, false, out lockData );
		bool hasDebugLock = hasCollision;
		var debugLockData = lockData;
		if ( !hasDebugLock && p.HitTime > 0 && p.HitNormal.LengthSquared > 0.0001f )
		{
			if ( TryGetCollisionLock( p, true, out var previewLock ) )
			{
				hasDebugLock = true;
				debugLockData = previewLock;
			}
		}

		if ( hasDebugLock )
		{
			UpdateDebugLockPreview( in p, in debugLockData );
			_hasCachedCollisionLock = true;
			_cachedCollisionLock = debugLockData;
		}

		if ( hasCollision )
		{
			bool isNewHit = IsNewCollisionEvent( in p );
			if ( !_spawnedAny || (_renderer.SpawnNewDecalOnEveryCollision && isNewHit) )
			{
				SpawnDecal( in p, in lockData );
			}

			RegisterCollisionEvent( in p );
		}

		bool hasAnyVisible = false;
		for ( int i = 0; i < _activeDecals.Count; i++ )
		{
			var active = _activeDecals[i];
			var so = active.SceneObject;
			if ( so is null || !so.IsValid() )
				continue;

			// Keep hidden until this frame explicitly passes culling.
			so.RenderingEnabled = false;

			ApplyTransform( so, in p, in active );
			bool treatAsPersistent = _renderer.PersistOnDeath;
			if ( _renderer.ShouldCullByDistance( so.Transform.Position, persistent: treatAsPersistent ) )
				continue;

			ApplyVisuals( so, in p, in active );
			hasAnyVisible = true;
		}

		if ( _renderer.ShouldCollectDebugState() )
		{
			_renderer.ReportDebugState(
				_debugKey,
				in p,
				hasCollision: p.HitTime > 0,
				passedFilters: hasDebugLock || _lastPassedFilters,
				locked: hasAnyVisible || hasDebugLock,
				visible: hasAnyVisible,
				collisionDeath: wouldDieOnCollision,
				lockedPosition: _lastLockedPosition,
				lockedNormal: _lastLockedNormal,
				volumeSize: _lastLockedVolume,
				rotation: _lastLockedRotation );
		}
		else
		{
			_renderer.ClearDebugState( _debugKey );
		}
	}
}
#endif
