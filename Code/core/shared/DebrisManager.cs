namespace Core;

using AI;


#if FMOD
using FMODSbox;
#endif
using System;
using System.Threading.Tasks;

public class DebrisManager : BaseEntity
{
	public static DebrisManager Instance { get; set; }
	[Property, ReadOnly, Feature( "Debug" )] public int TrackedDebris { get; set; } = 0;

	[ConVar( "debug_surfacedecal", ConVarFlags.Cheat )] public static bool ShowSurfaceDebug { get; set; } = false;
	[Property, Feature( "Debug" ), Title( "Show Debris Objects" )]
	public bool DisplayDebrisObj
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;

				GameObjectFlags desiredFlag = value ? GameObjectFlags.None : GameObjectFlags.Hidden;

				if ( _debrisObjects.Count > 0 )
				{
					foreach ( var obj in _debrisObjects )
					{
						if ( obj.Flags != desiredFlag )
							obj.Flags = desiredFlag;
					}
				}
			}
		}
	} = false;

	[Property, Feature( "Debug" ), ShowIf( nameof( DisplayDebrisObj ), true )] private List<GameObject> _debrisObjects = [];

	public DebrisManager() => Instance = this;

	protected override void OnStart()
	{
		base.OnStart();

		GameObject.Name = "Debris Manager";
	}

	[ConCmd( "cleardecals" )]
	static void ClearDecals()
	{
		var damn = Instance._debrisObjects.ToList();

		foreach ( var decal in damn )
		{
			if ( decal.Components.TryGet<Decal>( out var dcl ) )
			{
				dcl?.GameObject.Destroy();
			}

			Instance._debrisObjects?.Remove( decal );
		}
	}

	public static GameObject CreateDebris( string modelname, Vector3 position, Rotation rotation, Vector3 posimpulse )
	{
		var model = Model.Load( modelname );
		//		new gameobject
		var gameobject = Instance.Scene.CreateObject();
		gameobject.Tags.Add( "allow_to_transition" );
		gameobject.Name = model.ResourceName + "_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() ).Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );
		gameobject.SetParent( Instance.GameObject );
		gameobject.WorldPosition = position;
		gameobject.WorldRotation = rotation;
		gameobject.Transform.ClearInterpolation();

		//		new prop
		var prop = gameobject.Components.Create<ModelRenderer>();
		prop.Model = model;

		var physprop = gameobject.Components.Create<Rigidbody>();
		physprop.Tags.Add( "debris" );
		physprop.Tags.Add( "rigidbody" );
		physprop.ApplyImpulse( posimpulse );

		var collision = gameobject.Components.Create<ModelCollider>();
		collision.Model = model;
		IncreaseAmount( gameobject );

		return gameobject;
	}

	[Rpc.Broadcast]
	public static void CreateHitSound( Vector3 position, Surface surface, GameObject HitObject )
	{
#if FMOD
		BasePlayer.SolveNullStringsInSurface( surface, out string surfstring );

		var soundh = FMODSound.Play( "event:/Physics/BulletImpact", position );
		if ( !string.IsNullOrEmpty( surfstring ) )
			FMODSound.SetParameter( soundh, "parameter:/Physics/MaterialType", surfstring );

		soundh.setVolume( 0.5f );

		if ( HitObject.Components.TryGet<AIController>( out var npc ) ) FMODSound.Play( "event:/Player/HUD/Hitmarker" );
#else
		var sound = surface.SoundCollection.Bullet ?? surface.GetBaseSurface().SoundCollection.Bullet;

		if ( !sound.IsValid() ) return;

		var soundh = Sound.Play( sound, position );
		soundh?.Volume *= 0.5f;
		if ( HitObject.Components.TryGet<AIController>( out var npc ) ) Sound.Play( "hit_marker" );
#endif
	}

	[Rpc.Broadcast]
	public static void CreateBulletDecal( Vector3 position, Vector3 normal, Surface surface, GameObject parent, float scale = 0.25f )
	{
		if ( surface.HasTag( "noimpactdecal" ) || surface.HasTag( "noimpact" ) )
		{
			if ( ShowSurfaceDebug ) Log.Info( $"[TEMP DEBUG] {surface} set to not have a decal" );
			return;
		}

		/*
				SceneTraceResult cleantr = Scene.Trace
				.Sphere( 6, position, position + new Vector3(4, 0, 0) )
				.WithTag( "decal" )
				.HitTriggersOnly()
				.Run();

				if ( cleantr.Hit )
				{
					Gizmo.Draw.IgnoreDepth = true;
					Gizmo.Draw.Color = Color.Red;
					Gizmo.Draw.LineSphere( cleantr.HitPosition, 6 );
					cleantr.GameObject.Destroy();
					DecreaseAmount();
				}
		*/

		GameObject decalobject = Instance.Scene.CreateObject();

		if ( GameManagerSystem.Rules.IsOnline ) decalobject.NetworkSpawn();

		var decal = decalobject.Components.Create<Decal>();
		decal.Transient = true; // abide by maxdecals command but doesnt seem to work
		var temp = decalobject.Components.Create<TemporaryEffect>();

		//		var decalcollider = decalobject.Components.Create<SphereCollider>();
		//		decalcollider.Radius = 8;
		//		decalcollider.IsTrigger = true;

		decal.Decals = SurfaceExtension.FindForResourceOrDefault( surface ).DecalList;
		decal.Depth = 4;
		decal.Scale = scale;

		decalobject.Tags.Add( "allow_to_transition" );
		decalobject.WorldPosition = position + normal;
		decalobject.WorldRotation = normal.EulerAngles.ToRotation();
		//		decalobject.LocalRotation = new Vector3( LocalRotation.Pitch(), LocalRotation.Yaw(), Random.Rotation().Roll() ).EulerAngles.ToRotation();
		decalobject.Transform.ClearInterpolation();

		decalobject.Tags.Add( "debris" );
		decalobject.Tags.Add( "decal" );
		decalobject.Name = "bullet_hole_decal_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() ).Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );

		decalobject.SetParent( parent.IsValid() ? parent : Instance.GameObject );

		IncreaseAmount( decalobject );

		//	return decalobject;
	}

	[Rpc.Broadcast]
	public static void CreateBulletImpact( Vector3 position, Vector3 normal, Surface surface, bool wantColor = false, Color color = default )
	{
		if ( surface.HasTag( "noimpactparticle" ) || surface.HasTag( "noimpact" ) )
		{
			if ( ShowSurfaceDebug )
				Log.Info( $"[TEMP DEBUG] {surface} set to not have a particle" );
			return;
		}

		var baseSurface = surface?.GetBaseSurface();
		var bulletEffects = surface?.PrefabCollection.BulletImpact;
		var baseBulletEffects = baseSurface?.PrefabCollection.BulletImpact;

		string defaultPrefabPath = "prefabs/game/particles/impact_generic_smokepuff.prefab";

		GameObject prefabPath = bulletEffects ?? baseBulletEffects ?? GameObject.GetPrefab( defaultPrefabPath );

		if ( ShowSurfaceDebug )
		{
			// All the Log info for us to know what's going on, its otherwise ruled 
			// by the string pefabPath with its hefty checks!
			if ( !bulletEffects.IsValid() )
				Log.Warning( $"No particles defined for {surface}. Using Base Option override particles for this surface." );

			if ( prefabPath.PrefabInstanceSource == defaultPrefabPath && !bulletEffects.IsValid() && !baseBulletEffects.IsValid() )
				Log.Warning( $"No valid particles on this surface or {baseSurface}. Falling back to default particles for {surface}." );
		}

		GameObject prefabObject = bulletEffects ?? baseBulletEffects;

		/*
				// Now we can support overriding legacy particles! In the future we might not even need this
				// TODO: In a year(!) check if this is even needed, but otherwise neat failsafe
				if ( Path.GetExtension( prefabPath ) != Path.GetExtension( ".prefab" ) )
				{
					if ( ShowSurfaceDebug )
						Log.Warning( $"Failed to load this particles: {prefabPath}. Not supported {Path.GetExtension( prefabPath )} format is being used! Replacing with override particle on {surface}!" );

					prefabObject = PrefabScene.GetPrefab( defaultPrefabPath );
				}
		*/
		// Texture texture          = null;
		// g_tColor reliance is kind of annoying here :( Not that it works rn.
		// TODO: This is trying to get a texture from the mat to apply to the particle. Not working, not bieng done. Need to evaluate if plausible
		// if ( material .IsValid() && ( texture = material.GetTexture( "g_tColor" )) .IsValid()  && texture.IsValid() ) 
		// { 
		//	var prefabParticleRender     = prefabObject?.GetComponent<ParticleSpriteRenderer>();
		// 	prefabParticleRender.Texture = texture;
		// }


		Rotation rotation = (-normal).EulerAngles.ToRotation();
		GameObject particleObject = prefabObject.Clone( position, rotation );

		if ( wantColor )
		{
			foreach ( var particle in particleObject.Components.GetAll<ParticleEffect>() )
			{
				particle.Tint = color;
			}
		}

		if ( GameManagerSystem.Rules.IsOnline ) particleObject.NetworkSpawn();

		IncreaseAmount( particleObject );

		particleObject.Tags.Add( "debris" );
		particleObject.Name = "bullet_impact_particle_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() )
					.Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );

		particleObject.SetParent( Instance.GameObject );

		if ( particleObject.Components.TryGet<ParticleEffect>( out var effect ) )
		{
			effect.OnComponentDestroy = () =>
			{
				if ( particleObject.IsValid )
				{
					particleObject.Destroy();
					DecreaseAmount( particleObject );
				}
			};
		}
	}

	[Rpc.Broadcast]
	public void CreateBulletTracer( GameObject attacker, WeaponParse weapon, Vector3 position, Vector3 normal )
	{
		float minTracerDistance = 300f;
		float tracerSpeed = 8000f;
		float tracerStreakLength = 300f;
		float fixedDuration = 0.3f;

		// Default fall backs for tracer
		string defaultTracerPath = "prefabs/game/particles/weapons/weapon_tracer.prefab";

		// Default falllback 
		Vector3 muzzleForward = normal;

		var traceDistance = 4096f;

		var trace = Scene.Trace.Ray( position, position + muzzleForward * traceDistance )
			.WithoutTags( "player" )
			.Run();

		Vector3 tracerEnd = trace.Hit
			? trace.EndPosition
			: position + muzzleForward * traceDistance;

		GameObject prefabObject = GameObject.GetPrefab( weapon?.TracerEffect?.ResourcePath ?? defaultTracerPath );
		GameObject tracer = prefabObject.Clone( position, muzzleForward.EulerAngles );

		if ( GameManagerSystem.Rules.IsOnline ) tracer.NetworkSpawn();

		tracer.SetParent( GameObject );
		tracer.Tags.Add( "debris" );
		tracer.Name = "tracer_particle_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() )
						.Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );

		IncreaseAmount( tracer );
		GameObject.Name = "Debris Manager (" + TrackedDebris + ")";

		tracer.Components.TryGet<BeamEffect>( out var beam );

		if ( !beam.IsValid )
			return;

		float travelDistance = Vector3.DistanceBetween( position, tracerEnd );

		if ( travelDistance < minTracerDistance )
		{
			tracer.Destroy();
			return;
		}

		float travelDuration = travelDistance / tracerSpeed;
		if ( travelDuration > fixedDuration ) travelDuration = fixedDuration;

		// Streak is just a short length from muzzle forward, not all the way to endpoint
		beam.WorldPosition = position;
		beam.TargetPosition = position + muzzleForward * tracerStreakLength;

		ParticleFloat travel = beam.TravelLerp;
		travel.Evaluation = ParticleFloat.EvaluationType.Life;
		travel.Type = ParticleFloat.ValueType.Range;
		travel.ConstantA = 0f;
		travel.ConstantB = 1f;

		ParticleFloat lifetime = beam.BeamLifetime;
		lifetime.ConstantA = travelDuration * 1.1f;
		beam.BeamLifetime = lifetime;

		beam.TravelBetweenPoints = true;
		beam.TravelLerp = travel;

		beam.Lighting = true;
		beam.Additive = true;

		beam.SpawnBeam();
	}

	public static GameObject CreateProjectileObject( CoreDamageInfo coreDamageInfo, Transform transform, out BulletProjectile bullet, Vector3 spread )
	{
		// for some reason its really shitting itself when i spawn a prefab, it doesnt even break from prefab for some reason
		//		string defaultTracerPath = "prefabs/game/particles/weapons/weapon_tracer.prefab";
		//		GameObject prefabObject = GameObject.GetPrefab( defaultTracerPath );

		//		prefabObject.Clone( transform );
		//		prefabObject.BreakFromPrefab(); // dont want the prefab, so we can add shit to it

		GameObject prefabObject = new( " Projectile" );
		prefabObject.SetParent( Instance.GameObject );
		prefabObject.LocalRotation = transform.Rotation;
		prefabObject.LocalPosition = transform.Position;

		bullet = prefabObject.AddComponent<BulletProjectile>(); // just a bullet for now but later we should decide which component
		bullet.Ammo = coreDamageInfo.Ammo;
		bullet.DamageInfo = coreDamageInfo;
		bullet.Spread = spread;

		if ( prefabObject.Components.TryGet<BulletProjectile>( out var bulletref ) ) // we just created it, it will probably be not null but you will never know
		{
			bulletref.OnComponentDestroy = () =>
			{
				if ( bulletref.IsValid )
				{
					DecreaseAmount( prefabObject );
					prefabObject.Destroy();
				}
			};
		}

		return prefabObject;
	}

	public GameObject CreateMuzzleflashObject( WeaponParse weapon, GameObject muzzleObject )
	{
		// Default fall backs for muzzleflash
		string defaultMuzzleflashPath = "prefabs/game/particles/weapons/weapon_muzzleflash.prefab";
		GameObject prefabObject = GameObject.GetPrefab( weapon?.MuzzleFlashEffect?.ResourcePath ?? defaultMuzzleflashPath );

		if ( prefabObject is null )
			return null;

		Vector3 spawnPos = muzzleObject.WorldPosition;
		Rotation spawnRot = muzzleObject.WorldRotation;

		GameObject muzzleflashObj = prefabObject.Clone( spawnPos, spawnRot );

		if ( GameManagerSystem.Rules.IsOnline ) muzzleflashObj.NetworkSpawn();

		muzzleflashObj.WorldPosition = spawnPos;
		muzzleflashObj.WorldRotation = spawnRot;
		muzzleflashObj.Tags.Add( "debris" );
		muzzleflashObj.Name = "muzzleflash_particle_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() )
						.Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );

		IncreaseAmount( muzzleflashObj );
		GameObject.Name = "Debris Manager (" + TrackedDebris + ")";

		return muzzleflashObj;
	}

	public static GameObject CreateViewMuzzleflashObject( WeaponParse weapon, Vector3 muzzlePos, Rotation muzzleRot, GameObject muzzleObject )
	{
		// Default fallback for muzzleflash
		string defaultMuzzleflashPath = "prefabs/game/particles/weapons/weapon_muzzleflash.prefab";
		GameObject prefabObject = GameObject.GetPrefab( weapon?.MuzzleFlashEffect?.ResourcePath ?? defaultMuzzleflashPath );

		if ( prefabObject is null )
			return null;

		GameObject muzzleflashObj = prefabObject.Clone( position: muzzlePos, rotation: muzzleRot, parent: muzzleObject, scale: 1f );

		if ( GameManagerSystem.Rules.IsOnline ) muzzleflashObj.NetworkSpawn();

		muzzleflashObj.WorldPosition = muzzlePos;
		muzzleflashObj.WorldRotation = muzzleRot;
		muzzleflashObj.Tags.Add( "debris" );
		muzzleflashObj.Name = "muzzleflash_particle_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() )
						.Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );

		IncreaseAmount( muzzleflashObj );
		Instance.GameObject.Name = "Debris Manager (" + Instance.TrackedDebris + ")";

		return muzzleflashObj;
	}

	[Rpc.Broadcast]
	public void CreateShellCasing( string prefabPath, Vector3 position, Rotation rotation, Vector3 velocity )
	{
		if ( string.IsNullOrEmpty( prefabPath ) )
		{
			Log.Warning( "[DebrisManager] Invalid shell casing prefab path!" );
		}

		var prefabObject = GameObject.GetPrefab( prefabPath );
		if ( !prefabObject.IsValid() )
		{
			Log.Warning( $"[DebrisManager] Failed to load prefab at path: {prefabPath}" );
		}

		GameObject casingObject = prefabObject.Clone( position, rotation );
		if ( GameManagerSystem.Rules.IsOnline ) casingObject.NetworkSpawn();

		casingObject.SetParent( GameObject );
		casingObject.Tags.Add( "debris" );
		casingObject.Name = "casing_particle_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() )
			.Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );

		IncreaseAmount( casingObject );
		GameObject.Name = $"Debris Manager ({TrackedDebris})";

		ParticleEffect effect = casingObject.Components.Get<ParticleEffect>();
		ParticleBoxEmitter emitter = casingObject.Components.Get<ParticleBoxEmitter>();

		if ( effect.IsValid() && emitter.IsValid() )
		{
			emitter.Loop = false;
			effect.Lifetime = 10f;

			effect.Pitch = rotation.Pitch();
			effect.Yaw = rotation.Yaw();
			effect.Roll = rotation.Roll();

			Vector3 initialVelocity = velocity;
			effect.ForceDirection = initialVelocity;

			effect.ApplyRotation = true;
			emitter.DestroyOnEnd = true;

			// Handle casings logic with helpers
			handleWeaponCasings( effect, initialVelocity );
		}

		emitter.OnComponentDestroy = () =>
		{
			if ( casingObject.IsValid )
			{
				DecreaseAmount( casingObject );
				casingObject.Destroy();
			}
		};
	}

	private void handleWeaponCasings( ParticleEffect effect, Vector3 initialVelocity )
	{
		_ = AdjustShellForce( effect, initialVelocity );
		_ = AdjustShellForceAndRotation( effect, initialVelocity );
	}

	private static void DecreaseAmount( GameObject debrisObj )
	{
		if ( !debrisObj.IsValid() )
			return;

		Instance._debrisObjects.Remove( debrisObj );

		int objCount = Instance._debrisObjects.Count;
		Instance.TrackedDebris = objCount;

		if ( Instance.GameObject.IsValid() )
			Instance.GameObject.Name = $"Debris Manager ({objCount})";
	}

	private static void IncreaseAmount( GameObject debrisObj )
	{
		debrisObj.Flags = Instance.DisplayDebrisObj ? GameObjectFlags.None : GameObjectFlags.Hidden;
		Instance._debrisObjects.Add( debrisObj );

		int objCount = Instance._debrisObjects.Count;

		Instance.TrackedDebris = objCount;
		Instance.GameObject.Name = "Debris Manager (" + objCount + ")";
	}


	// ====== Weapong casings helpers ====== //
	private async Task AdjustShellForce( ParticleEffect effect, Vector3 initialVelocity )
	{
		float duration = 2.5f; // how long the initial velocity lasts
		float t = 0f;

		Vector3 start = initialVelocity;
		Vector3 end = new( initialVelocity.x, initialVelocity.y, -90f ); // drop on Z axis


		while ( t < duration && effect.IsValid() )
		{
			t += Time.Delta;

			float progress = Math.Clamp( t / duration, 0f, 1f );
			float eased = EasingPlus.EaseOutQuad( progress );

			effect.ForceDirection = Vector3.Lerp( start, end, eased );

			await Task.Yield();
		}

		t = 0f;
		duration = 0.25f;
		Vector3 settledStart = effect.ForceDirection;

		while ( t < duration && effect.IsValid() )
		{
			t += Time.Delta;
			float progress = Math.Clamp( t / duration, 0f, 1f );
			float eased = EasingPlus.EaseOutExpo( progress );

			effect.ForceDirection = Vector3.Lerp( settledStart, Vector3.Zero, eased );

			await Task.Yield();
		}

		// Maintain downward force after easing finishes
		if ( effect.IsValid() )
		{
			effect.ForceDirection = new Vector3( 0f, 0f, -90f ); ;
		}
	}
	private async Task AdjustShellForceAndRotation( ParticleEffect effect, Vector3 initialVelocity )
	{
		float duration = 0.5f;
		float t = 0f;

		Vector3 startVelocity = initialVelocity;
		Vector3 endVelocity = new( initialVelocity.x, initialVelocity.y, -120f );

		// Store original effect rotation
		float initialPitch = effect.Pitch.ToString().ToFloat();
		float initialYaw = effect.Yaw.ToString().ToFloat();
		float initialRoll = effect.Roll.ToString().ToFloat();

		// Generate random rotation deltas for tumbling
		float targetPitchOffset = Random.Shared.Float( 20f, 80f ) * (Random.Shared.Next( 2 ) == 0 ? 1 : -1);
		float targetYawOffset = Random.Shared.Float( 10f, 50f ) * (Random.Shared.Next( 2 ) == 0 ? 1 : -1);
		float targetRollOffset = Random.Shared.Float( 50f, 180f ) * (Random.Shared.Next( 2 ) == 0 ? 1 : -1);

		while ( t < duration && effect.IsValid() )
		{
			t += Time.Delta;
			float progress = Math.Clamp( t / duration, 0f, 1f );
			float eased = EasingPlus.EaseOutCubic( progress );

			effect.ForceDirection = Vector3.Lerp( startVelocity, endVelocity, eased );

			// Interpolate rotation offsets (simulate tumbling)
			effect.Pitch = initialPitch + targetPitchOffset * eased;
			effect.Yaw = initialYaw + targetYawOffset * eased;
			effect.Roll = initialRoll + targetRollOffset * eased;

			await Task.Yield();
		}

		if ( effect.IsValid() )
		{
			effect.ForceDirection = endVelocity;
		}
	}

}

//[GameResource( "Surface Extension", "extsurf", "Unshitting the surface", Category = "Physics", Icon = "iron" )]
/// <summary>
/// Unshitting the surface
/// </summary>
[AssetType( Name = "Surface Extension", Extension = "extsurf", Category = "Physics" )]
public class SurfaceExtension : ResourceExtension<Surface, SurfaceExtension>
{
	public List<DecalDefinition> DecalList { get; set; }
	protected override Bitmap CreateAssetTypeIcon( int width, int height ) { return CreateSimpleAssetTypeIcon( "iron", width, height ); }
}
