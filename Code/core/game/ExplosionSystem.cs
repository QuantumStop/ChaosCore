namespace Core;

using System;
using Core.Voxels;

[Hide]
public class ExplosionSystem : BaseEntity
{

	/// <summary>
	/// Explosion wave using voxel system to only damage entities in acceptable range.
	/// Uses curves to determine progression from 0.0 to 1.0, provides a time scale, duration and functions as a blast progression helper.
	/// </summary>
	public static async void DoExplosionWave(
		Vector3 origin,
		float maxRadius,
		float baseDamage,
		float ExplosionDuration,
		BaseVoxelVolume<byte> voxelVolume,
		GameObject explosionSource,
		DamageTagSet dmgType = null,
		Curve? explosionCurve = null,
		bool bPhysicsDebris = true,
		float rate = 0.1f,
		float dt = 0.05f,
		bool? isDebug = false,
		Curve? PushForce = null,
		HashSet<GameObject> damagedObjects = null
	)
	{
		var curve = explosionCurve ?? new Curve(
			new[]
			{
				new Curve.Frame(0f, 0f),
				new Curve.Frame(1f, 1f)
			}
		);

		if ( voxelVolume == null ) return;

		VoxelDebugQueue.Clear();

		// --- Step 0: Precompute static geometry mask ---
		for ( int x = 0; x < voxelVolume.DimX; x++ )
			for ( int y = 0; y < voxelVolume.DimY; y++ )
				for ( int z = 0; z < voxelVolume.DimZ; z++ )
				{
					var g = new Vector3Int( x, y, z );
					var wpos = voxelVolume.VoxelToWorld( g );
					var tr = Game.SceneTrace.Ray( origin, wpos ).Run();

					var meshComponent = tr.GameObject?.GetComponent<MeshComponent>();

					if ( tr.Hit && ((tr.GameObject != null && tr.GameObject.Tags.Has( "static" ))
					|| tr.GameObject.Tags.Has( "world" )
					|| meshComponent != null && meshComponent.Static && !meshComponent.IsTrigger) )
					{
						voxelVolume.SetVoxel( g, 0x4 ); // mark as static-blocked
					}
				}

		// --- Step 1: Prepare shell radii ---
		float shellStep = voxelVolume.VoxelSize * 0.5f; // smaller than voxel size to prevent gaps
		int numShells = (int)MathF.Ceiling( maxRadius / shellStep );
		numShells = Math.Min( numShells, 200 ); // cap max shells for performance

		float[] shellRadii = new float[numShells];
		for ( int i = 0; i < numShells; i++ )
			shellRadii[i] = (i + 1) * shellStep;

		// --- Step 2: Visual pacing ---
		int targetFrames = 30; // frames to spread the explosion
		float safeDuration = MathF.Max( 0.015f, ExplosionDuration ); // minimum duration
		float dtStep = safeDuration / targetFrames;
		dtStep = MathF.Max( 0.005f, dtStep ); // safety clamp

		// --- Step 3: Iterate frames ---
		for ( int f = 0; f < targetFrames; f++ )
		{
			// --- Determine which shells to process in this frame ---
			int startShell = f * numShells / targetFrames;
			int endShell = (f + 1) * numShells / targetFrames;

			// Collect voxels for debug per frame to reduce overhead
			List<(Vector3 pos, byte flags)> debugVoxelsThisFrame = new();

			for ( int s = startShell; s < endShell; s++ )
			{
				// --- Current shell radius ---
				float radiusForStep = shellRadii[s];

				// --- Normalized progression for curve evaluation ---
				float tNormForShell = radiusForStep / maxRadius;

				// --- Energy/force scaling based on curve ---
				float energyScale = curve.Evaluate( tNormForShell );

				// --- Update voxels in this shell ---
				voxelVolume.UpdateRegionShell( origin, radiusForStep, shellStep, ( g, wpos, prev ) =>
				{
					// Skip if voxel is already static-blocked
					if ( (prev & 0x4) != 0 ) return prev;

					// --- Trace from explosion origin to voxel world position ---
					var tr = Game.SceneTrace.Ray( origin, wpos ).Run();
					bool blocked = tr.Hit && (
						tr.GameObject.Tags.Has( "static" ) ||
						tr.GameObject.Tags.Has( "world" ) ||
						tr.GameObject.GetComponent<MeshComponent>()?.Static == true
					);

					if ( blocked )
					{
						GameObject hitObj = tr.GameObject;

						// Try to get IDamageable component in self or parent
						hitObj.Components.TryGet<IDamageable>( out IDamageable dmgTarget, FindMode.EverythingInSelfAndParent );

						if ( dmgTarget != null )
						{
							var targetObject = hitObj;

							// --- Apply damage only once per explosion ---
							if ( !damagedObjects.Contains( targetObject ) )
							{
								// --- Prepare damage tags ---
								DamageTagSet damageTypeSet = dmgType ?? new DamageTagSet();
								damageTypeSet.Add( "explosion" );

								// --- Determine push curve (fallback linear if none) ---
								Curve? pushCurve = PushForce ?? new Curve(
									new[] { new Curve.Frame( 0f, 1f ), new Curve.Frame( 1f, 0f ) }
								);

								Vector3 delta = hitObj.WorldPosition - origin;
								float distance = delta.Length;
								float distanceFraction = MathX.Clamp( distance / maxRadius, 0f, 1f );

								// --- Determine explosion force magnitude ---
								float rawCurve = pushCurve?.Evaluate( distanceFraction ) ?? (1f - distanceFraction);
								float maxForcePerUnit = 6.5f;
								float forceMagnitude = maxForcePerUnit * maxRadius * rawCurve * energyScale;

								Vector3 explosionDir = delta.Normal;
								Vector3 explosionForce = explosionDir * forceMagnitude;

								// --- Apply damage to players only ---
								if ( targetObject.Parent.Tags.Has( "player" ) )
								{
									GameObject rootTarget = targetObject.Parent;

									// --- Compute vertical lift factor ---
									float verticalLiftFactor = MathF.Max( 0.25f, 1f - MathX.Clamp( delta.z / maxRadius, 0f, 1f ) );

									// --- Apply upward lift ---
									explosionForce += Vector3.Up * forceMagnitude * verticalLiftFactor;

									if ( !damagedObjects.Contains( rootTarget ) )
									{
										damagedObjects.Add( rootTarget );

										dmgTarget.OnDamage( new CoreDamageInfo
										{
											Force = explosionForce,
											Attacker = explosionSource,
											Damage = baseDamage,
											Tags = { damageTypeSet }
										} );
									}
								}
								else
								{

									// --- Accumulate physics impulses for this target ---
									Vector3 totalForce = Vector3.Zero;
									Vector3 totalAngularForce = Vector3.Zero;

									foreach ( var rb in targetObject.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndParent ) )
									{
										Vector3 physicsForce = Vector3.Up * forceMagnitude * 0.8f; // keep upward scaling
										physicsForce += Vector3.Random.Normal * forceMagnitude * 0.15f;

										// Random spin torque
										float torqueMagnitude = physicsForce.Length * Random.Shared.Float( 0.5f, 1.0f );
										Vector3 randomAxis = Vector3.Random.Normal;
										Vector3 angularImpulse = randomAxis * torqueMagnitude;

										physicsForce *= rb.Mass;
										angularImpulse *= rb.Mass;

										// Accumulate
										totalForce += physicsForce;
										totalAngularForce += angularImpulse;
									}

									// --- Apply damage once ---
									if ( !damagedObjects.Contains( targetObject ) )
									{
										damagedObjects.Add( targetObject );

										dmgTarget.OnDamage( new CoreDamageInfo
										{
											Force = totalForce,
											AngularForce = totalAngularForce,
											Attacker = explosionSource,
											Damage = baseDamage,
											Tags = { damageTypeSet }
										} );
									}
								}
							}

							return (byte)(prev | 0x1); // mark voxel as "solid"
						}

					}

					return prev; // leave unchanged if not blocked
				}, voxelPadding: 0.5f );

				// --- Compute visibility for current shell ---
				voxelVolume.ComputeVisibilityShell( origin, radiusForStep, shellStep, v =>
				{
					byte val = voxelVolume.GetVoxel( v );
					val |= 0x2; // mark as visible
					voxelVolume.SetVoxel( v, val );

					if ( isDebug == true && (val & 0x4) == 0 )
						debugVoxelsThisFrame.Add( (voxelVolume.VoxelToWorld( v ), val) );
				} );
			}

			// --- Enqueue debug voxels once per frame ---
			foreach ( var (pos, flags) in debugVoxelsThisFrame )
			{
				VoxelDebugQueue.Enqueue( BBox.FromPositionAndSize( pos, voxelVolume.VoxelSize * 0.95f ),
					Color.White, draw: true, flags: flags, lifetime: 0.5f );
			}

			// --- Small delay between frames to pace visual explosion ---
			await GameTask.DelaySeconds( dtStep );
		}
	}
}
