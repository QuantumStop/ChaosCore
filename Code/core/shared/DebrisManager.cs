using Core;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Schema;

public class DebrisManager : BaseEntity
{
	// public static bool IsSpawned { get; set; }
	public static DebrisManager StaticRef { get; set; }
	[Property, ReadOnly, Feature( "Debug" )] public int TrackedDebris { get; set; } = 0;

	[ConVar( "debug_surfacedecal", ConVarFlags.Cheat )] public static bool ShowSurfaceDebug { get; set; } = false;
	[Property, Feature( "Debug" ), MakeDirty, Title( "Show Debris Objects" )] public bool DisplayDebrisObj { get; set; } = false;

	[Property, Feature( "Debug" ), ShowIf( "DisplayDebrisObj", true )] private List<GameObject> DebrisObjects = new();

	public DebrisManager() { StaticRef = this; }

	protected override void OnStart()
	{
		base.OnStart();

		GameObject.Name = "Debris Manager";
	}

	protected override void OnDirty()
	{
		base.OnDirty();

		GameObjectFlags desiredFlag = DisplayDebrisObj ? GameObjectFlags.None : GameObjectFlags.Hidden;

		if ( DebrisObjects.Count > 0 )
		{
			foreach ( var obj in DebrisObjects )
			{
				if ( obj.Flags != desiredFlag )
					obj.Flags = desiredFlag;
			}
		}
	}

	public GameObject CreateDebris( string modelname, Vector3 position, Rotation rotation, Vector3 posimpulse )
	{
		var model = Model.Load( modelname );
		//		new gameobject
		var gameobject = Scene.CreateObject();
		gameobject.Tags.Add( "allow_to_transition" );
		gameobject.Name = model.ResourceName + "_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() ).Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );
		gameobject.SetParent( GameObject );
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

	private void ProcessDecal( Decal decal, Surface decalSurface )
	{
		var decalSurf_default = ResourceLibrary.Get<DecalDefinition>( "scripts/decals/default.decal" );
		var decalSurf = SurfaceExtension.FindForResourceOrDefault( decalSurface ) ?? SurfaceExtension.FindForResourceOrDefault( decalSurface.GetBaseSurface() );
		//		var defaultSurf = SurfaceExtension.FindForResourceOrDefault( decalSurface.GetBaseSurface() );

		// just to check if anything is in there, because actual path 
		//		var decalPath = decalSurf?.DecalList.FirstOrDefault().ResourcePath
		//			 ?? defaultSurf?.DecalList.FirstOrDefault().ResourcePath
		//			 ?? default_decalPath?.ResourcePath;

		// Make sure we are using our decals, and as a fall back for legacy override others as our default
		// We could eventually override anything that just makes sense like metal, paper, concrete etc legacy wise
		//		if ( Path.GetExtension( decalPath ) != Path.GetExtension( ".decal" ) )
		//			decal.Decals.Add( ResourceLibrary.Get<DecalDefinition>( decalPath ) );

		//	if we have anything on the surface - use that, or force default_decalPath otherwise
		decal.Decals = decalSurf.IsValid() ? decalSurf.DecalList : [decalSurf_default];

		decal.Depth = 4;
	}

	public void CreateHitSound( Vector3 position, Surface surface, GameObject HitObject )
	{
		var sound = surface.SoundCollection.Bullet ?? surface.GetBaseSurface().SoundCollection.Bullet;

		if ( sound == null )
			return;

		Sound.Play( sound, position );

		if ( HitObject.Components.TryGet<BaseNpc>( out var npc ) ) Sound.Play( "hit_marker" );
	}

	public GameObject CreateBulletDecal( Vector3 position, Vector3 normal, Surface surface, GameObject parent, float scale = 0.25f )
	{
		if ( surface.HasTag( "noimpactdecal" ) || surface.HasTag( "noimpact" ) )
		{
			if ( ShowSurfaceDebug )
				Log.Info( $"[TEMP DEBUG] {surface} set to not have a decal" );
			return null;
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

		GameObject decalobject = Scene.CreateObject();
		var decal = decalobject.Components.Create<Decal>();
		decal.Transient = true; // abide by maxdecals command but doesnt seem to work
		var temp = decalobject.Components.Create<TemporaryEffect>();

		//		var decalcollider = decalobject.Components.Create<SphereCollider>();
		//		decalcollider.Radius = 8;
		//		decalcollider.IsTrigger = true;

		ProcessDecal( decal, surface );
		decal.Scale = scale;

		decalobject.Tags.Add( "allow_to_transition" );
		decalobject.WorldPosition = position + normal;
		decalobject.WorldRotation = normal.EulerAngles.ToRotation();
		//		decalobject.LocalRotation = new Vector3( LocalRotation.Pitch(), LocalRotation.Yaw(), Random.Rotation().Roll() ).EulerAngles.ToRotation();
		decalobject.Transform.ClearInterpolation();

		decalobject.Tags.Add( "debris" );
		decalobject.Tags.Add( "decal" );
		decalobject.Name = "bullet_hole_decal_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() ).Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );

		//	TODO: add support for skinned meshes (parent to bone)
		if ( parent != null )
			decalobject.SetParent( parent );
		else
			decalobject.SetParent( GameObject );

		IncreaseAmount( decalobject );

		return decalobject;
	}

	public void CreateBulletImpact( Vector3 position, Vector3 normal, Surface surface, Material material )
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

		string prefabPath = bulletEffects?.PrefabInstanceSource
					?? baseBulletEffects?.PrefabInstanceSource
					?? defaultPrefabPath;

		if ( ShowSurfaceDebug )
		{
			// All the Log info for us to know what's going on, its otherwise ruled 
			// by the string pefabPath with its hefty checks!
			if ( bulletEffects == null )
				Log.Warning( $"No particles defined for {surface}. Using Base Option override particles for this surface." );

			if ( prefabPath == defaultPrefabPath && bulletEffects == null && baseBulletEffects == null )
				Log.Warning( $"No valid particles on this surface or {baseSurface}. Falling back to default particles for {surface}." );
		}

		GameObject prefabObject = PrefabScene.GetPrefab( prefabPath );

		// Now we can support overriding legacy particles! In the future we might not even need this
		// TODO: In a year(!) check if this is even needed, but otherwise neat failsafe
		if ( Path.GetExtension( prefabPath ) != Path.GetExtension( ".prefab" ) )
		{
			if ( ShowSurfaceDebug )
				Log.Warning( $"Failed to load this particles: {prefabPath}. Not supported {Path.GetExtension( prefabPath )} format is being used! Replacing with override particle on {surface}!" );

			prefabObject = PrefabScene.GetPrefab( defaultPrefabPath );
		}

		// Texture texture          = null;
		// g_tColor reliance is kind of annoying here :( Not that it works rn.
		// TODO: This is trying to get a texture from the mat to apply to the particle. Not working, not bieng done. Need to evaluate if plausible
		// if ( material != null && ( texture = material.GetTexture( "g_tColor" )) != null  && texture.IsValid() ) 
		// { 
		//	var prefabParticleRender     = prefabObject?.GetComponent<ParticleSpriteRenderer>();
		// 	prefabParticleRender.Texture = texture;
		// }


		Rotation rotation = (-normal).EulerAngles.ToRotation();
		GameObject particleObject = prefabObject.Clone( position, rotation );
		ParticleEffect effect = particleObject.Components.Get<ParticleEffect>();

		IncreaseAmount( particleObject );

		particleObject.Tags.Add( "debris" );
		particleObject.Name = "bullet_impact_particle_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() )
					.Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );

		particleObject.SetParent( GameObject );

		GameObject particleGO = particleObject;

		effect.OnComponentDestroy = () =>
		{
			if ( particleGO.IsValid )
			{
				particleGO.Destroy();
				DecreaseAmount( particleGO );
			}
		};
	}

	public void CreateBulletTracer( GameObject attacker, WeaponParse weapon, Vector3 position, Vector3 normal )
	{
		var weaponMuzzle = BasePlayer.Local?.ViewmodelWeapon?.GetAttachmentObject( "muzzle" );

		if ( weaponMuzzle == null )
		{
			if ( ShowSurfaceDebug )
				Log.Warning( "[TEMP DEBUG] Weapon muzzle is null, nowhere to shoot the particle from!" );
			return;
		}

		// Default fall backs for tracer
		string defaultTracerPath = "prefabs/game/particles/weapons/weapon_tracer.prefab";

		// Default falllback 
		Vector3 muzzleForward = weaponMuzzle.WorldRotation.Forward;
		Vector3 muzzlePos = weaponMuzzle.WorldPosition;
		Vector3 tracerEnd = muzzlePos + muzzleForward * 4096f;

		if ( attacker.Components.TryGet<BasePlayer>( out var player ) )
		{
			float offsetDistance = -1.5f;

			muzzleForward = weaponMuzzle.WorldRotation.Forward;
			muzzlePos = weaponMuzzle.WorldPosition + muzzleForward * offsetDistance;

			var traceDistance = 4096f;

			var trace = Scene.Trace.Ray( muzzlePos, muzzlePos + muzzleForward * traceDistance )
				.WithoutTags( "player" )
				.Run();

			// Pick endpoint — hit point or max distance
			tracerEnd = trace.Hit
				? trace.EndPosition
				: muzzlePos + muzzleForward * traceDistance;
		}
		else if ( attacker.Components.TryGet<BaseNpc>( out var npc ) )
		{
			float offsetDistance = -1.5f;

			muzzleForward = weaponMuzzle.WorldRotation.Forward;
			muzzlePos = weaponMuzzle.WorldPosition + muzzleForward * offsetDistance;

			var traceDistance = 4096f;

			var trace = Scene.Trace.Ray( muzzlePos, muzzlePos + muzzleForward * traceDistance )
				.WithoutTags( "player" )
				.Run();

			// Pick endpoint — hit point or max distance
			tracerEnd = trace.Hit
				? trace.EndPosition
				: muzzlePos + muzzleForward * traceDistance;
		}

		GameObject prefabObject = GameObject.GetPrefab( weapon?.TracerEffect?.ResourcePath ?? defaultTracerPath );
		GameObject tracer = prefabObject.Clone( weaponMuzzle.WorldPosition, weaponMuzzle.WorldRotation );

		tracer.SetParent( GameObject );
		tracer.Tags.Add( "debris" );
		tracer.Name = "tracer_particle_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() )
						.Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );

		IncreaseAmount( tracer );
		GameObject.Name = "Debris Manager (" + TrackedDebris + ")";

		tracer.Components.TryGet<BeamEffect>( out var beam );

		if ( !beam.IsValid )
			return;

		beam.WorldPosition = muzzlePos;
		beam.TargetPosition = tracerEnd;

		ParticleFloat travel = beam.TravelLerp;

		travel.Evaluation = ParticleFloat.EvaluationType.Life;
		travel.Type = ParticleFloat.ValueType.Range;
		travel.ConstantA = 0f;
		travel.ConstantB = 1f;

		beam.TravelBetweenPoints = true;
		beam.TravelLerp = travel;

		beam.Lighting = true;
		beam.Additive = true;

		beam.SpawnBeam();
	}

	public void CreateMuzzleflash( WeaponParse weapon, Vector3 position )
	{
		var weaponMuzzle = BasePlayer.Local?.ViewmodelWeapon?.GetAttachmentObject( "muzzle" );

		if ( weaponMuzzle == null )
		{
			if ( ShowSurfaceDebug )
				Log.Warning( "[TEMP DEBUG] Weapon muzzle is null, muzzleflash cannot be fired" );
			return;
		}

		// Default fall backs for tracer
		string defaultMuzzleflashPath = "prefabs/game/particles/weapons/weapon_muzzleflash.prefab";

		GameObject prefabObject = GameObject.GetPrefab( weapon?.TracerEffect?.ResourcePath ?? defaultMuzzleflashPath );
		GameObject muzzleflash = prefabObject.Clone( position );

		muzzleflash.SetParent( GameObject );
		muzzleflash.Tags.Add( "debris" );
		muzzleflash.Name = "muzzleflash_particle_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() )
						.Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );

		IncreaseAmount( muzzleflash );
		GameObject.Name = "Debris Manager (" + TrackedDebris + ")";
	}

	public void CreateShellCasing( string prefabPath, Vector3 position, Rotation rotation, Vector3 velocity )
	{
		if ( string.IsNullOrEmpty( prefabPath ) )
		{
			Log.Warning( "[DebrisManager] Invalid shell casing prefab path!" );
		}

		var prefabObject = PrefabScene.GetPrefab( prefabPath );
		if ( prefabObject == null )
		{
			Log.Warning( $"[DebrisManager] Failed to load prefab at path: {prefabPath}" );
		}

		GameObject casingObject = prefabObject.Clone( position, rotation );

		casingObject.SetParent( GameObject );
		casingObject.Tags.Add( "debris" );
		casingObject.Name = "casing_particle_" + Convert.ToBase64String( Guid.NewGuid().ToByteArray() )
			.Replace( "=", "" ).Replace( "+", "" ).Replace( "/", "" ).Truncate( 5 );

		IncreaseAmount( casingObject );
		GameObject.Name = $"Debris Manager ({TrackedDebris})";

		ParticleEffect effect = casingObject.Components.Get<ParticleEffect>();
		ParticleBoxEmitter emitter = casingObject.Components.Get<ParticleBoxEmitter>();

		if ( effect != null && emitter != null )
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

		GameObject casingGO = casingObject;

		emitter.OnComponentDestroy = () =>
		{
			if ( casingGO.IsValid() )
			{
				DecreaseAmount( casingGO );
				casingGO.Destroy();
			}
		};
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
	}

	private void handleWeaponCasings( ParticleEffect effect, Vector3 initialVelocity )
	{
		_ = AdjustShellForce( effect, initialVelocity );
		_ = AdjustShellForceAndRotation( effect, initialVelocity );
	}

	private void DecreaseAmount( GameObject debrisObj )
	{
		if ( debrisObj == null || !debrisObj.IsValid() )
			return;

		if ( DebrisObjects.Contains( debrisObj ) )
			DebrisObjects.Remove( debrisObj );

		int objCount = DebrisObjects.Count;
		TrackedDebris = objCount;

		if ( GameObject.IsValid() )
			GameObject.Name = $"Debris Manager ({objCount})";
	}

	private void IncreaseAmount( GameObject debrisObj )
	{
		debrisObj.Flags = DisplayDebrisObj ? GameObjectFlags.None : GameObjectFlags.Hidden;
		DebrisObjects.Add( debrisObj );

		int objCount = DebrisObjects.Count();

		TrackedDebris = objCount;
		GameObject.Name = "Debris Manager (" + objCount + ")";
	}


	// ====== Weapong casings helpers ====== //
	private async Task AdjustShellForce( ParticleEffect effect, Vector3 initialVelocity )
	{
		float duration = 2.5f; // how long the initial velocity lasts
		float t = 0f;

		Vector3 start = initialVelocity;
		Vector3 end = new Vector3( initialVelocity.x, initialVelocity.y, -90f ); // drop on Z axis


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
		Vector3 endVelocity = new Vector3( initialVelocity.x, initialVelocity.y, -120f );

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

[GameResource( "Surface Extension", "extsurf", "Unshitting the surface", Category = "Physics", Icon = "iron" )]
public class SurfaceExtension : ResourceExtension<Surface, SurfaceExtension>
{
	public List<DecalDefinition> DecalList { get; set; }
}
