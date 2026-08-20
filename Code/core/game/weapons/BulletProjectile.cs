namespace Core;

public class BulletProjectile : BaseProjectile
{
	// Speed in Feet per second / 50 physics fps = speed in feet (12hu) per tick
	protected override float _velocityPerTick => Ammo.FtPerSec * 12f / ProjectSettings.Physics.FixedUpdateFrequency; // this feels... uhhhhhhhhh
	/// <summary>
	/// Gravitational Constant G = 9.80665f, thank you Issac Newton
	/// </summary>
	private static float _dropPerSecond => MathX.MeterToInch( 9.80665f );
	protected override float _entityGizmoSize => 8f;
	[Property, Feature( "Debug" ), ReadOnly] public CoreDamageInfo DamageInfo { get; set; }

	[Property, Feature( "Debug" ), ReadOnly] private TimeSince _travelTime { get; set; }

	[Property, Feature( "Debug" ), ReadOnly] private Vector3 _startPos { get; set; }
	[ConVar( "debug_bullet_distance", Help = "Show info of the travel (distance, drop)" )] static public bool TravelDebug { get; set; } = false;
	[ConVar( "debug_bullet_rays", Help = "Show the bullet trajectory, the number is how long the debug stays on (in seconds)" )] static public float RayDebug { get; set; } = 0;

	[Property, Feature( "Debug" ), ReadOnly] public Vector3 Spread { get; set; }
	[Property, Feature( "Debug" ), ReadOnly] private int _castCount { get; set; }
	[Property, Feature( "Debug" ), ReadOnly] public AttackManager.AttackResult Result { get; private set; }
	private float _dropFormula => 0.5f * _dropPerSecond * (_travelTime * _travelTime);

	private Color _rayColor { get; set; }

	protected override void OnEnabled()
	{
		_travelTime = 0;
		_startPos = LocalPosition;
		_rayColor = Color.Random;
		//	Log.Info( "shot speed: " + VelocityPerTick );
	}

	[ConVar( "debug_bullet_cast" )] static public bool CountDebug { get; set; } = false;

	protected override void FixedThink()
	{
		AttackManager.AttackResult trace = AttackManager.TraceGenericAttack( new Ray( LocalPosition, LocalTransform.Forward ), _velocityPerTick, DamageInfo );

		if ( CountDebug ) Log.Info( "This is bullet ray number " + _castCount );

		if ( RayDebug > 0 )
		{
			Vector3 endpos = LocalPosition + LocalTransform.Forward * _velocityPerTick;
			endpos = endpos.WithZ( endpos.z - _dropFormula );
			DebrisManager.Instance.DebugOverlay.Line( LocalPosition, endpos, _rayColor, RayDebug );
		}

		// if travelling for 5 years force clean up, theres no way we need to fire something for this long
		// or if we fall out the map too far, i dont think we are going to have a map 13 kilometers down from origin
		if ( _travelTime > 20 || LocalPosition.z < -512000 )
		{
			Destroy();
			return;
		}

		Result = trace;

		if ( !trace.Hit )
		{
			LocalPosition += LocalTransform.Forward * _velocityPerTick;
			LocalPosition = LocalPosition.WithZ( LocalPosition.z - _dropFormula );
			_castCount++;
		}
		else
		{
			if ( TravelDebug )
			{
				var distance = Vector3.DistanceBetween( _startPos, trace.Last.HitPosition );
				Log.Info( "Distance: " + MathX.InchToMeter( distance ) + "m / " + distance + "in at the speed of " + MathX.InchToMeter( _velocityPerTick * ProjectSettings.Physics.FixedUpdateFrequency ) + "m/s or " + Ammo.FtPerSec + "ft/s" );
				Log.Info( "Time: " + _travelTime.Relative + "s" );
				Log.Info( "Drop: " + MathX.InchToMeter( _dropFormula ) + "m / " + _dropFormula + "in" );
			}

			AttackManager.HandleHitBullet( trace, DamageInfo, LocalTransform );
			Destroy();
		}
	}
}
