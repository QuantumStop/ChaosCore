#if IGNIS
namespace Core;

using System;

[Title( "Particle Decal Renderer" )]
[Category( "Particles" )]
[Icon( "lens_blur" )]
public class ParticleDecalRenderer : ParticleController, Component.ExecuteInEditor
{
	public enum DecalSelectionMode
	{
		Random,
		Next,
		Previous
	}

	public enum DecalSortMode
	{
		Stable,
		NewestOnTop,
		OldestOnTop
	}

	public enum DecalLifetimeMode
	{
		MatchParticleLifetime,
		OwnLifetime
	}

	[Property, WideMode, Header( "General" )]
	public List<DecalDefinition> Decals { get; set; } = [];

	[Property]
	public DecalSelectionMode SelectionMode { get; set; } = DecalSelectionMode.Random;

	[Property, Header( "Projection" )]
	public ParticleFloat Scale { get; set; } = 1.0f;

	[Property]
	public ParticleFloat Rotation { get; set; } = 0.0f;

	[Property, Range( 0.0f, 8.0f )]
	public float SurfaceBias { get; set; } = 0.5f;

	[Property]
	public float MinProjectionDepth { get; set; } = -8f;

	[Property]
	public float MaxProjectionDepth { get; set; } = 8f;

	[Property, Header( "Visuals" )]
	public Color ConstantColorTint { get; set; } = Color.White;

	[Property]
	public ParticleGradient ColorTint { get; set; } = Color.White;

	[Property]
	public ParticleFloat Brightness { get; set; } = 1.0f;

	[Property, Range( 0, 1 )]
	public ParticleFloat Alpha { get; set; } = 1.0f;

	[Property, Range( 0, 1 )]
	public ParticleFloat ColorMix { get; set; } = 1.0f;

	[Property]
	public ParticleFloat Parallax { get; set; } = 1.0f;

	[Property, Range( 0, 1 )]
	public float AttenuationAngle { get; set; } = 1.0f;

	[Property, Header( "Pseudo Bump" )]
	public float PseudoBumpStrength { get; set; } = 1.0f;

	[Property, Range( 0.0f, 1.0f )]
	public float PseudoBumpColorSuppression { get; set; } = 0.5f;

	[Property, Range( 0.0f, 4.0f )]
	public float PseudoBumpParallaxBoost { get; set; } = 1.0f;

	[Title( "Surface Variation" )]
	[Property, FeatureEnabled( "SurfaceVariation", Icon = "shuffle" )]
	public bool SurfaceVariation { get; set; }

	[Property, Feature( "SurfaceVariation" ), Range( 0.0f, 180.0f )]
	public float SurfaceYawJitter { get; set; } = 0.0f;

	[Property, Feature( "SurfaceVariation" ), Range( 0.0f, 1.0f )]
	public float SurfaceScaleJitter { get; set; } = 0.0f;

	[Property, Feature( "SurfaceVariation" ), Range( 0.0f, 1.0f )]
	public float SurfaceBrightnessJitter { get; set; } = 0.0f;

	[Property, Feature( "SurfaceVariation" ), Range( 0.0f, 1.0f )]
	public float SurfaceColorMixJitter { get; set; } = 0.0f;

	[Property, Feature( "SurfaceVariation" ), Range( 0.0f, 1.0f )]
	public float SurfaceParallaxJitter { get; set; } = 0.0f;

	[Property, Group( "Collision" )]
	[Title( "Spawn On Collision Death" )]
	public bool SpawnOnCollisionDeath { get; set; } = true;

	[Property, Group( "Collision" )]
	public bool SpawnNewDecalOnEveryCollision { get; set; } = false;

	[Property, Group( "Collision" )]
	public bool StickToHitObjects { get; set; } = true;

	[Property, Group( "Collision" )]
	public bool AllowMultipleActiveDecals { get; set; } = false;

	[Property, Group( "Collision" ), Range( 1, 512 )]
	public int MaxDecalsPerParticle { get; set; } = 8;

	[Property, Group( "Collision" )]
	[Title( "Persist On Death" )]
	public bool PersistOnDeath { get; set; } = false;

	[Property, Group( "Collision" )]
	public bool ForceSpawnOnDisableCollision { get; set; } = true;

	[Property, Group( "Collision" )]
	public DecalLifetimeMode LifetimeMode { get; set; } = DecalLifetimeMode.OwnLifetime;

	[Property, Group( "Collision" ), ShowIf( nameof( LifetimeMode ), DecalLifetimeMode.OwnLifetime ), Range( 0.0f, 100f )]
	public ParticleFloat DecalLifetime { get; set; } = 8.0f;

	[Title( "Persistent Fade" )]
	[Property, Group( "Collision" ), FeatureEnabled( "PersistentFade", Icon = "animation" )]
	public bool PersistentFade { get; set; } = false;

	[Property, Feature( "PersistentFade" )]
	public ParticleFloat PersistentAlphaOverLife { get; set; } = new ParticleFloat( 1, 0 );

	[Property, Group( "Projection Filters" )]
	public TagSet IncludeHitTags { get; set; } = [];

	[Property, Group( "Projection Filters" )]
	public TagSet ExcludeHitTags { get; set; } = [];

	[Property, Group( "Sorting" )]
	public DecalSortMode SortMode { get; set; } = DecalSortMode.NewestOnTop;

	[Property, Group( "Sorting" ), Range( 0, 255 )]
	public uint SortLayer { get; set; } = 0;

	[Property, Group( "Sorting" ), Range( 1, 255 )]
	public uint SortPerDecalStep { get; set; } = 1;

	[Property, Group( "Sorting" )]
	public bool SortPerHitObject { get; set; } = true;

	[Property, Group( "Performance" ), Range( 1, 8192 )]
	public int MaxTotalDecals { get; set; } = 512;

	[Property, Group( "Performance" ), Range( 1, 4096 )]
	public int MaxPersistentDecals { get; set; } = 128;

	[Title( "Distance Culling" )]
	[Property, FeatureEnabled( "DistanceCulling", Icon = "near_me" )]
	public bool EnableDistanceCulling { get; set; } = false;

	[Property, Feature( "DistanceCulling" ), Range( 0.0f, 50000.0f )]
	public float MaxDecalDrawDistance { get; set; } = 5000.0f;

	[Property, Feature( "DistanceCulling" )]
	public bool CullPersistentByDistance { get; set; } = true;

	[Property, Feature( "DistanceCulling" )]
	public bool CullActiveByDistance { get; set; } = false;

	[Title( "Debug" )]
	[Property, FeatureEnabled( "EnableDebug", Icon = "bug_report" )]
	public bool EnableDebug { get; set; }

	[Property, Feature( "EnableDebug" )]
	public bool DebugDrawGizmos { get; set; } = true;

	[Property, Feature( "EnableDebug" )]
	public bool DebugDrawVolume { get; set; } = true;

	[Property, Feature( "EnableDebug" ), Range( 0.001f, 5.0f )]
	public float DebugStateMaxAge { get; set; } = 0.35f;

	[Property, Feature( "EnableDebug" ), Range( 0.1f, 30.0f )]
	public float DebugPersistentStateMaxAge { get; set; } = 3.0f;

	[Property, Feature( "EnableDebug" ), Range( 1, 8192 )]
	public int MaxDebugDrawCount { get; set; } = 256;

	[Property, Feature( "EnableDebug" ), Range( 0.0f, 50000.0f )]
	public float DebugMaxDrawDistance { get; set; } = 2000.0f;

	private int _sequenceIndex = -1;
	private Vector3 _lastCullingCameraPosition;
	private float _lastCullingCameraSampleTime = -1.0f;

	private readonly object _debugSync = new();
	private readonly Dictionary<int, DebugState> _debugStates = [];
	private readonly List<DebugState> _debugSnapshot = [];
	private readonly List<int> _debugStaleKeys = [];
	private readonly List<DebugDrawEntry> _debugDrawEntries = [];
	private static readonly DebugDrawEntryComparer _debugDrawEntryComparer = new();

	private sealed class DebugState
	{
		public float LastUpdateTime;
		public Vector3 ParticlePos;
		public Vector3 HitPos;
		public Vector3 HitNormal;
		public Vector3 LockedPos;
		public Vector3 LockedNormal;
		public Vector3 VolumeSize;
		public Rotation Rotation;
		public bool HasCollision;
		public bool PassedFilters;
		public bool Locked;
		public bool Visible;
		public bool IsPersistent;
		public bool DrawParticle = true;
		public bool CollisionDeath;
	}

	private struct DebugDrawEntry
	{
		public DebugState State;
		public float DistSq;
	}

	private sealed class DebugDrawEntryComparer : IComparer<DebugDrawEntry>
	{
		public int Compare( DebugDrawEntry x, DebugDrawEntry y ) => x.DistSq.CompareTo( y.DistSq );
	}

	private readonly object _persistentSync = new();
	private readonly List<PersistentDecal> _persistentDecals = [];

	private readonly object _trackedSync = new();
	private readonly List<TrackedDecal> _trackedDecals = [];

	private sealed class PersistentDecal
	{
		public DecalSceneObject SceneObject;
		public float StartAt;
		public float ExpireAt;
		public float LifeTime;
		public Vector3 BasePosition;
		public Rotation BaseRotation;
		public Vector3 BaseWorldScale;
		public float ScaleMul;
		public float BrightnessMul;
		public float ColorMixMul;
		public float ParallaxMul;
		public float Width;
		public float Height;
		public Color Tint;
		public uint Seed;
	}

	private sealed class TrackedDecal
	{
		public DecalSceneObject SceneObject;
		public float SpawnAt;
	}

	internal DecalDefinition SelectDecal( Particle p )
	{
		if ( Decals is null || Decals.Count == 0 )
			return null;

		if ( Decals.Count == 1 )
			return Decals[0];

		switch ( SelectionMode )
		{
			case DecalSelectionMode.Next:
				_sequenceIndex = _sequenceIndex < 0 ? 0 : (_sequenceIndex + 1) % Decals.Count;
				return Decals[_sequenceIndex];

			case DecalSelectionMode.Previous:
				_sequenceIndex = _sequenceIndex < 0 ? Decals.Count - 1 : (_sequenceIndex - 1 + Decals.Count) % Decals.Count;
				return Decals[_sequenceIndex];

			default:
				return Decals[(int)(p.Rand( 123 ) * Decals.Count) % Decals.Count];
		}
	}

	internal void RegisterActiveDecal( DecalSceneObject sceneObject )
	{
		if ( sceneObject is null || !sceneObject.IsValid() )
			return;

		lock ( _trackedSync )
		{
			RegisterTracked_NoLock( sceneObject );
			CullTrackedByBudget_NoLock();
		}
	}

	internal void UnregisterActiveDecal( DecalSceneObject sceneObject )
	{
		if ( sceneObject is null )
			return;

		lock ( _trackedSync )
		{
			for ( int i = _trackedDecals.Count - 1; i >= 0; i-- )
			{
				if ( ReferenceEquals( _trackedDecals[i].SceneObject, sceneObject ) )
					_trackedDecals.RemoveAt( i );
			}
		}
	}

	internal bool ShouldCullByDistance( Vector3 worldPos, bool persistent )
	{
		// Distance culling is gameplay behavior only.
		if ( Scene.IsEditor )
			return false;

		if ( !EnableDistanceCulling )
			return false;

		if ( persistent && !CullPersistentByDistance )
			return false;

		if ( !persistent && !CullActiveByDistance )
			return false;

		if ( MaxDecalDrawDistance <= 0.0f )
			return false;

		if ( !TryGetCullingCameraPosition( out var cameraPos ) )
			return true;

		return worldPos.Distance( cameraPos ) > MaxDecalDrawDistance;
	}

	internal bool ShouldCollectDebugState()
	{
		if ( !EnableDebug )
			return false;

		// In normal gameplay, skip debug work entirely.
		if ( !Scene.IsEditor )
		{
			// Allow debug while playing only when in editor and no player camera is active
			// (typically ejected/spectator editor view).
			if ( !(Scene?.IsEditor ?? false) )
				return false;

			return BasePlayer.Local?.Controller?.Camera is null;
		}

		return true;
	}

	private bool TryGetCullingCameraPosition( out Vector3 cameraPos )
	{
		cameraPos = default;

		if ( !Scene.IsEditor )
		{
			var localPlayerCamera = BasePlayer.Local?.Controller?.Camera;
			if ( localPlayerCamera is not null )
			{
				cameraPos = localPlayerCamera.WorldPosition;
				_lastCullingCameraPosition = cameraPos;
				_lastCullingCameraSampleTime = Time.Now;
				return true;
			}

			if ( _lastCullingCameraSampleTime >= 0.0f && (Time.Now - _lastCullingCameraSampleTime) <= 0.5f )
			{
				cameraPos = _lastCullingCameraPosition;
				return true;
			}

			return false;
		}

		var sceneCamera = Scene?.Camera;
		if ( sceneCamera is not null && sceneCamera.IsValid )
		{
			cameraPos = sceneCamera.WorldPosition;
			return true;
		}

		return false;
	}

	private bool TryGetDebugCameraPosition( out Vector3 cameraPos )
	{
		cameraPos = default;

		if ( Scene?.IsEditor ?? false )
		{
			var gizmoCamera = Gizmo.Camera;
			if ( gizmoCamera is not null )
			{
				cameraPos = gizmoCamera.Position;
				return true;
			}

			var manager = GameManagerSystem.Current;
			if ( manager is not null && manager.Scene == Scene )
			{
				var editorPos = manager.LastEditorCameraPosition.Position;
				if ( editorPos.LengthSquared > 0.001f )
				{
					cameraPos = editorPos;
					return true;
				}
			}
		}

		return TryGetCullingCameraPosition( out cameraPos );
	}

	private void PruneDebugStates( float now )
	{
		float maxAge = MathF.Max( DebugStateMaxAge, 0.001f );
		float maxPersistentAge = MathF.Max( DebugPersistentStateMaxAge, 0.001f );

		lock ( _debugSync )
		{
			_debugStaleKeys.Clear();
			foreach ( var pair in _debugStates )
			{
				float age = now - pair.Value.LastUpdateTime;
				// Persistent entries should survive for whichever debug age is longer.
				float limit = pair.Value.IsPersistent ? MathF.Max( maxPersistentAge, maxAge ) : maxAge;
				if ( age > limit )
					_debugStaleKeys.Add( pair.Key );
			}

			for ( int i = 0; i < _debugStaleKeys.Count; i++ )
				_debugStates.Remove( _debugStaleKeys[i] );
		}
	}

	internal void AdoptPersistentDecal(
		DecalSceneObject sceneObject,
		float lifeTime,
		uint seed,
		Vector3 basePosition,
		Rotation baseRotation,
		Vector3 baseWorldScale,
		float scaleMul,
		float brightnessMul,
		float colorMixMul,
		float parallaxMul,
		float width,
		float height,
		Color tint )
	{
		if ( sceneObject is null || !sceneObject.IsValid() )
			return;

		var now = Time.Now;
		var expireAt = lifeTime > 0.0f ? now + lifeTime : float.PositiveInfinity;
		var resolvedSeed = seed != 0 ? seed : (uint)(now * 1000.0f);

		lock ( _persistentSync )
		{
			_persistentDecals.Add( new PersistentDecal
			{
				SceneObject = sceneObject,
				StartAt = now,
				ExpireAt = expireAt,
				LifeTime = lifeTime,
				BasePosition = basePosition,
				BaseRotation = baseRotation,
				BaseWorldScale = baseWorldScale,
				ScaleMul = scaleMul,
				BrightnessMul = brightnessMul,
				ColorMixMul = colorMixMul,
				ParallaxMul = parallaxMul,
				Width = width,
				Height = height,
				Tint = tint,
				Seed = resolvedSeed
			} );

			CullPersistentByCount_NoLock();
		}

		lock ( _trackedSync )
		{
			RegisterTracked_NoLock( sceneObject );
			CullTrackedByBudget_NoLock();
		}
	}

	internal void ReportDebugState(
		int debugKey,
		in Particle p,
		bool hasCollision,
		bool passedFilters,
		bool locked,
		bool visible,
		bool collisionDeath,
		Vector3 lockedPosition,
		Vector3 lockedNormal,
		Vector3 volumeSize,
		Rotation rotation )
	{
		lock ( _debugSync )
		{
			_debugStates[debugKey] = new DebugState
			{
				LastUpdateTime = Time.Now,
				ParticlePos = p.Position,
				HitPos = p.HitPos,
				HitNormal = p.HitNormal,
				LockedPos = lockedPosition,
				LockedNormal = lockedNormal,
				VolumeSize = volumeSize,
				Rotation = rotation,
				HasCollision = hasCollision,
				PassedFilters = passedFilters,
				Locked = locked,
				Visible = visible
				,
				CollisionDeath = collisionDeath
			};
		}
	}

	internal void ClearDebugState( int debugKey )
	{
		lock ( _debugSync )
		{
			_debugStates.Remove( debugKey );
		}
	}

	internal void PersistDebugState( int debugKey )
	{
		lock ( _debugSync )
		{
			if ( !_debugStates.TryGetValue( debugKey, out var state ) )
				return;

			state.IsPersistent = true;
			state.DrawParticle = false;
		}
	}

	protected override void OnParticleCreated( Particle p )
	{
		var selected = SelectDecal( p );
		if ( selected is null )
			return;

		p.AddListener( new ParticleDecal( this, selected ), this );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		var now = Time.Now;
		PruneDebugStates( now );

		lock ( _persistentSync )
		{
			for ( int i = _persistentDecals.Count - 1; i >= 0; i-- )
			{
				var p = _persistentDecals[i];
				if ( p.SceneObject is null || !p.SceneObject.IsValid() )
				{
					_persistentDecals.RemoveAt( i );
					continue;
				}

				// Hidden by default each frame; only enable after cull passes.
				p.SceneObject.RenderingEnabled = false;

				float d = p.LifeTime > 0.0f ? Math.Clamp( (now - p.StartAt) / p.LifeTime, 0.0f, 1.0f ) : 0.0f;

				float scale = Scale.Evaluate( d, 238 ) * p.ScaleMul;
				if ( !float.IsFinite( scale ) ) scale = 1.0f;
				scale = MathF.Max( scale, 0.01f );

				float depth = MathF.Max( 0.01f, MaxProjectionDepth - MinProjectionDepth );
				var size = new Vector3( depth, p.Width * scale, p.Height * scale );
				p.SceneObject.Transform = new Transform( p.BasePosition, p.BaseRotation, p.BaseWorldScale * size );

				var color = p.Tint;
				color *= ConstantColorTint;
				color *= ColorTint.Evaluate( d, 928 );

				float brightness = Brightness.Evaluate( d, 4626 ) * p.BrightnessMul;
				if ( !float.IsFinite( brightness ) ) brightness = 1.0f;
				brightness = MathF.Max( brightness, 0.0f );

				float alpha = Alpha.Evaluate( d, 8525 );
				if ( !float.IsFinite( alpha ) ) alpha = 1.0f;
				alpha = Math.Clamp( alpha, 0.0f, 1.0f );

				if ( PersistentFade && p.LifeTime > 0.0f && float.IsFinite( p.ExpireAt ) )
				{
					float alphaMul = PersistentAlphaOverLife.Evaluate( d, (int)p.Seed );
					if ( !float.IsFinite( alphaMul ) ) alphaMul = 1.0f;
					alpha = Math.Clamp( alpha * alphaMul, 0.0f, 1.0f );
				}

				color = color.WithColorMultiplied( brightness );
				color = color.WithAlpha( Math.Clamp( color.a * alpha, 0.0f, 1.0f ) );
				p.SceneObject.Color = color;

				p.SceneObject.ColorMix = p.ColorMixMul * ColorMix.Evaluate( d, 324 );
				p.SceneObject.ParallaxStrength = p.ParallaxMul * Parallax.Evaluate( d, 245 ) * 0.25f;
				p.SceneObject.AttenuationAngle = AttenuationAngle;

				if ( !ShouldCullByDistance( p.SceneObject.Transform.Position, persistent: true ) )
					p.SceneObject.RenderingEnabled = true;

				if ( now >= p.ExpireAt )
				{
					p.SceneObject.Delete();
					_persistentDecals.RemoveAt( i );
				}
			}

			CullPersistentByCount_NoLock();
		}

		lock ( _trackedSync )
		{
			CullTrackedByBudget_NoLock();
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		lock ( _debugSync )
		{
			_debugStates.Clear();
		}

		lock ( _persistentSync )
		{
			for ( int i = 0; i < _persistentDecals.Count; i++ )
			{
				var p = _persistentDecals[i];
				if ( p.SceneObject is not null && p.SceneObject.IsValid() )
					p.SceneObject.Delete();
			}

			_persistentDecals.Clear();
		}

		lock ( _trackedSync )
		{
			for ( int i = 0; i < _trackedDecals.Count; i++ )
			{
				var d = _trackedDecals[i];
				if ( d.SceneObject is not null && d.SceneObject.IsValid() )
					d.SceneObject.Delete();
			}

			_trackedDecals.Clear();
		}
	}

	private void RegisterTracked_NoLock( DecalSceneObject sceneObject )
	{
		for ( int i = 0; i < _trackedDecals.Count; i++ )
		{
			if ( ReferenceEquals( _trackedDecals[i].SceneObject, sceneObject ) )
				return;
		}

		_trackedDecals.Add( new TrackedDecal
		{
			SceneObject = sceneObject,
			SpawnAt = Time.Now
		} );
	}

	private void CullTrackedByBudget_NoLock()
	{
		for ( int i = _trackedDecals.Count - 1; i >= 0; i-- )
		{
			if ( _trackedDecals[i].SceneObject is null || !_trackedDecals[i].SceneObject.IsValid() )
				_trackedDecals.RemoveAt( i );
		}

		var max = Math.Max( MaxTotalDecals, 1 );
		while ( _trackedDecals.Count > max )
		{
			var oldest = _trackedDecals[0];
			if ( oldest.SceneObject is not null && oldest.SceneObject.IsValid() )
				oldest.SceneObject.Delete();
			_trackedDecals.RemoveAt( 0 );
		}
	}

	private void CullPersistentByCount_NoLock()
	{
		var max = Math.Max( MaxPersistentDecals, 1 );
		while ( _persistentDecals.Count > max )
		{
			var oldest = _persistentDecals[0];
			if ( oldest.SceneObject is not null && oldest.SceneObject.IsValid() )
				oldest.SceneObject.Delete();
			_persistentDecals.RemoveAt( 0 );
		}
	}

	protected override void DrawGizmos()
	{
		if ( !DebugDrawGizmos || !ShouldCollectDebugState() )
			return;

		float now = Time.Now;
		PruneDebugStates( now );
		int maxDraw = Math.Max( MaxDebugDrawCount, 1 );
		float maxDrawDist = MathF.Max( DebugMaxDrawDistance, 0.0f );
		bool useDistanceCull = maxDrawDist > 0.0f;
		float maxDrawDistSq = maxDrawDist * maxDrawDist;
		bool hasCameraPos = TryGetDebugCameraPosition( out var cameraPos );

		lock ( _debugSync )
		{
			if ( _debugStates.Count == 0 )
				return;

			_debugSnapshot.Clear();
			foreach ( var state in _debugStates.Values )
			{
				_debugSnapshot.Add( state );
			}
		}

		_debugDrawEntries.Clear();
		for ( int i = 0; i < _debugSnapshot.Count; i++ )
		{
			var state = _debugSnapshot[i];
			var drawPos = state.Locked ? state.LockedPos : (state.HasCollision ? state.HitPos : state.ParticlePos);
			float distSq = 0.0f;

			if ( useDistanceCull && hasCameraPos )
			{
				distSq = (drawPos - cameraPos).LengthSquared;
				if ( distSq > maxDrawDistSq )
					continue;
			}

			_debugDrawEntries.Add( new DebugDrawEntry { State = state, DistSq = distSq } );
		}

		if ( _debugDrawEntries.Count == 0 )
			return;

		int drawCount = _debugDrawEntries.Count;
		if ( drawCount > maxDraw )
		{
			if ( hasCameraPos )
				_debugDrawEntries.Sort( _debugDrawEntryComparer );

			drawCount = maxDraw;
		}

		var world = WorldTransform;
		for ( int i = 0; i < drawCount; i++ )
		{
			var state = _debugDrawEntries[i].State;
			var particlePosL = world.PointToLocal( state.ParticlePos );
			var hitPosL = world.PointToLocal( state.HitPos );
			var hitNormalL = world.NormalToLocal( state.HitNormal ).Normal;
			var lockedPosL = world.PointToLocal( state.LockedPos );
			var lockedNormalL = world.NormalToLocal( state.LockedNormal ).Normal;
			var lockedRotL = world.RotationToLocal( state.Rotation );

			Gizmo.Draw.LineThickness = 1;

			if ( state.DrawParticle )
			{
				Gizmo.Draw.Color = Color.Cyan;
				Gizmo.Draw.LineSphere( particlePosL, 1.5f );
			}

			if ( state.HasCollision )
			{
				Gizmo.Draw.Color = state.PassedFilters ? Color.Yellow : Color.Red;
				Gizmo.Draw.LineSphere( hitPosL, 2.0f );
				Gizmo.Draw.Line( hitPosL, hitPosL + hitNormalL * 14.0f );

				if ( state.CollisionDeath )
				{
					Gizmo.Draw.Color = Color.Red;
					Gizmo.Draw.LineSphere( hitPosL, 1.0f );
					Gizmo.Draw.Line( hitPosL, hitPosL + Vector3.Up * 8.0f );
				}
			}

			if ( state.Locked )
			{
				Gizmo.Draw.Color = state.Visible ? Color.Green : Color.Orange;
				Gizmo.Draw.LineSphere( lockedPosL, 2.5f );
				Gizmo.Draw.Line( lockedPosL, lockedPosL + lockedNormalL * 18.0f );

				if ( DebugDrawVolume )
				{
					using ( Gizmo.Scope() )
					{
						Gizmo.Transform = new Transform( state.LockedPos, state.Rotation );
						Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( Vector3.Zero, state.VolumeSize ) );
					}
				}
			}
		}
	}
}
#endif
