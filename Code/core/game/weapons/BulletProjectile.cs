namespace Core;

public class BulletProjectile : BaseProjectile
{
	// Speed in Feet per second / 50 physics fps = speed in feet (12hu) per tick
	protected override float VelocityPerTick => Ammo.FtPerSec * 12f / ProjectSettings.Physics.FixedUpdateFrequency; // this feels... uhhhhhhhhh
	/// <summary>
	/// Gravitational Constant G = 9.80665f, thank you Issac Newton
	/// </summary>
	private float DropPerSecond => MathX.MeterToInch( 9.80665f );
	protected override float EntityGizmoSize => 8f;
	[Property, Feature( "Debug" ), ReadOnly] public CoreDamageInfo damageInfo { get; set; }

	[Property, Feature( "Debug" ), ReadOnly] private TimeSince TravelTime { get; set; }

	[Property, Feature( "Debug" ), ReadOnly] private Vector3 StartPos { get; set; }
	[ConVar( "debug_bullet_distance", Help = "Show info of the travel (distance, drop)" )] static public bool TravelDebug { get; set; } = false;
	[ConVar( "debug_bullet_rays", Help = "Show the bullet trajectory, the number is how long the debug stays on (in seconds)" )] static public float RayDebug { get; set; } = 0;

	[Property, Feature( "Debug" ), ReadOnly] public Vector3 Spread { get; set; }
	[Property, Feature( "Debug" ), ReadOnly] private int _castCount { get; set; }
	[Property, Feature( "Debug" ), ReadOnly] public AttackManager.AttackResult Result { get; private set; }
	private float _dropFormula => 0.5f * DropPerSecond * (TravelTime * TravelTime);

	private Color _rayColor { get; set; }

	protected override void OnEnabled()
	{
		TravelTime = 0;
		StartPos = LocalPosition;
		_rayColor = Color.Random;
		//	Log.Info( "shot speed: " + VelocityPerTick );
	}
	/// <summary>
	/// We only want to apply spread to the first raycast, so it only matters for the first direction modification (like it would be with hitscan, where there is only one cast), instead of applying random spread to the whole bullet path, which would make it more "spread"
	/// </summary>
	Vector3 FinalSpread = 0;

	[ConVar( "debug_bullet_cast" )] static public bool CountDebug { get; set; } = false;

	protected override void FixedThink()
	{
		AttackManager.AttackResult trace = AttackManager.TraceGenericAttack( new Ray( LocalPosition, LocalTransform.Forward ), VelocityPerTick, damageInfo );

		if ( CountDebug ) Log.Info( "This is bullet ray number " + _castCount );

		if ( RayDebug > 0 )
		{
			Vector3 endpos = LocalPosition + LocalTransform.Forward * VelocityPerTick;
			endpos = endpos.WithZ( endpos.z - _dropFormula );
			DebrisManager.Instance.DebugOverlay.Line( LocalPosition, endpos, _rayColor, RayDebug );
		}

		// if travelling for 5 years force clean up, theres no way we need to fire something for this long
		// or if we fall out the map too far, i dont think we are going to have a map 13 kilometers down from origin
		if ( TravelTime > 20 || LocalPosition.z < -512000 )
		{
			Destroy();
			return;
		}

		Result = trace;

		if ( !trace.Hit )
		{
			LocalPosition += LocalTransform.Forward * VelocityPerTick;
			LocalPosition = LocalPosition.WithZ( LocalPosition.z - _dropFormula );
			_castCount++;
		}
		else
		{
			if ( TravelDebug )
			{
				var distance = Vector3.DistanceBetween( StartPos, trace.Last.HitPosition );
				Log.Info( "Distance: " + MathX.InchToMeter( distance ) + "m / " + distance + "in at the speed of " + MathX.InchToMeter( VelocityPerTick * ProjectSettings.Physics.FixedUpdateFrequency ) + "m/s or " + Ammo.FtPerSec + "ft/s" );
				Log.Info( "Time: " + TravelTime.Relative + "s" );
				Log.Info( "Drop: " + MathX.InchToMeter( _dropFormula ) + "m / " + _dropFormula + "in" );
			}

			AttackManager.HandleHitBullet( trace, damageInfo, LocalTransform );
			Destroy();
		}
	}
}
