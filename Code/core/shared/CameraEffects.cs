namespace Core;

using System;
using System.Collections.Generic;

public static class CameraEffects
{
	public struct CameraEffect
	{
		public float RollStrength;
		public float PitchStrength;
		public float ShakeStrength;
		public float Duration;
		public float Delay;
		public float RampSpeed;
		public float ResetThreshold;
		public float Frequency;
		public Curve StrengthCurve;
		public Curve? FalloffCurve;
		public bool AlternateRoll;
		public bool ShakePitch;
		public bool ShakeYaw;
		public bool ShakeRoll;
		public bool RampPerPulse;
		public Vector3? SourcePosition; // null = omnidirectional
	}

	private struct ActiveEffect
	{
		public CameraEffect Data;
		public float Timer;
		public float DelayTimer;
		public float RampTime;
		public float HoldTimer;
		public float CurrentRoll;
		public float TargetRoll;
		public float RollDirection;
		public float MaxCurveStrength;
		public float LastProcessedAttackTime;
		public bool IsRampBased; // uses ramp/hold logic vs simple duration fadeout
	}

	private static readonly List<ActiveEffect> _activeEffects = [];
	private static float _lastTriggerTime = -1f;

	// Public API

	public static void Push( CameraEffect effect, float initialTriggerTime = -1f, float initialRampTime = 0f )
	{
		if ( IsForceDisabled ) return;

		_activeEffects.Add( new ActiveEffect
		{
			Data = effect,
			RollDirection = 1f,
			LastProcessedAttackTime = initialTriggerTime,
			RampTime = initialRampTime,
			MaxCurveStrength = CacheMaxCurveStrength( effect.StrengthCurve ),
			IsRampBased = effect.RampSpeed > 0f
		} );
	}

	/// <summary>
	/// Translates weapon data into a camera effect and pushes it.
	/// Alternates roll direction per shot.
	/// </summary>
	public static void StartRecoil( WeaponParse weaponData, float lastTriggerTime )
	{
		if ( IsForceDisabled ) return;

		// Find existing recoil effect for this weapon and update it, or push a new one
		for ( int i = 0; i < _activeEffects.Count; i++ )
		{
			var e = _activeEffects[i];
			if ( !e.Data.AlternateRoll ) continue;

			e.Data.Duration = weaponData.RecoilResetThreshold;
			e.Data.RampSpeed = weaponData.RecoilRampSpeed;
			e.Data.ResetThreshold = weaponData.RecoilResetThreshold;
			e.Data.StrengthCurve = weaponData.RecoilStrengthCurve;
			e.Data.RollStrength = 1f;
			e.HoldTimer = 0f;
			e.RollDirection *= -1;
			e.MaxCurveStrength = CacheMaxCurveStrength( weaponData.RecoilStrengthCurve );

			// Increment ramp on this shot
			e.RampTime += e.Data.RampSpeed / 10f;
			e.RampTime = Math.Clamp( e.RampTime, 0f, e.Data.RampSpeed );

			_activeEffects[i] = e;
			return;
		}

		Push( new CameraEffect
		{
			RollStrength = 1f,
			Duration = weaponData.RecoilResetThreshold,
			RampSpeed = weaponData.RecoilRampSpeed,
			ResetThreshold = weaponData.RecoilResetThreshold,
			StrengthCurve = weaponData.RecoilStrengthCurve,
			AlternateRoll = true,
			RampPerPulse = true,
		}, -1f, weaponData.RecoilRampSpeed / 10f );
	}

	private static float _trauma = 0f;

	/// <summary>
	/// Accumulates trauma (0-1). Trauma is squared when applied to shake strength,
	/// so stacking hits feel progressively worse. Decays passively over time.
	/// </summary>
	public static void AddTrauma( float amount )
	{
		if ( IsForceDisabled ) return;

		_trauma = Math.Clamp( _trauma + amount, 0f, 1f );
	}

	/// <summary>
	/// Pushes a shake effect to the camera. Strength and duration define the base feel.
	/// Frequency controls oscillation speed as low values give slow rolling motion, high values give tight jitter.
	/// Axis flags let you restrict shake to specific rotational axes.
	/// Optionally pass a world-space source position to bias the shake direction toward the hit origin.
	/// A falloff curve overrides the default linear fade if provided.
	/// </summary>
	public static void AddShake( float strength, float duration, float delay = 0f, float frequency = 10f, bool shakePitch = true,
		bool shakeYaw = false, bool shakeRoll = true, Vector3? sourcePosition = null, Curve? falloffCurve = null )
	{
		if ( IsForceDisabled ) return;

		Push( new CameraEffect
		{
			ShakeStrength = strength,
			Duration = duration,
			Frequency = frequency,
			ShakePitch = shakePitch,
			ShakeYaw = shakeYaw,
			ShakeRoll = shakeRoll,
			Delay = delay,
			SourcePosition = sourcePosition,
			FalloffCurve = falloffCurve,
		} );
	}

	public static void Update( CameraComponent camera, float? lastTriggerTime = null )
	{
		if ( !camera.IsValid() ) return;
		_lastTriggerTime = lastTriggerTime ?? -1f;

		if ( !IsForceDisabled )
		{
			for ( int i = _activeEffects.Count - 1; i >= 0; i-- )
			{
				var e = _activeEffects[i];
				bool finished = false;

				if ( e.IsRampBased )
					finished = UpdateRampEffect( camera, ref e, lastTriggerTime ?? -1f );
				else
					finished = UpdateFadeEffect( camera, ref e );

				if ( finished )
					_activeEffects.RemoveAt( i );
				else
					_activeEffects[i] = e;
			}
		}
	}

	public static void Reset()
	{
		_activeEffects.Clear();
		_lastTriggerTime = -1f;
	}


	// Evaluators

	private static bool UpdateRampEffect( CameraComponent camera, ref ActiveEffect e, float lastTriggerTime )
	{
		bool isActive = (WorldTime.Now - lastTriggerTime) <= e.Data.ResetThreshold;
		bool newPulse = lastTriggerTime > e.LastProcessedAttackTime;

		if ( isActive )
		{
			if ( !e.Data.RampPerPulse )
			{
				e.RampTime += WorldTime.Delta;
				e.RampTime = Math.Clamp( e.RampTime, 0f, e.Data.RampSpeed );
			}

			e.HoldTimer = 0f;
		}
		else
		{
			e.HoldTimer += WorldTime.Delta;

			if ( e.HoldTimer > e.Data.ResetThreshold )
			{
				e.RampTime -= WorldTime.Delta;
				e.RampTime = Math.Max( e.RampTime, 0f );
			}
		}

		float rampT = Math.Clamp( e.RampTime / e.Data.RampSpeed, 0f, 1f );
		float targetStrength = e.Data.StrengthCurve.Evaluate( EasingPlus.EaseOutCubic( rampT ) );
		targetStrength = Math.Clamp( targetStrength, 0f, e.MaxCurveStrength );
		float strength = targetStrength * 0.1f;

		if ( newPulse )
		{
			e.LastProcessedAttackTime = lastTriggerTime;

			if ( e.Data.AlternateRoll )
				e.RollDirection *= -1;

			float recoilImpulse = Game.Random.Float( 6f, 8f ) * strength * e.RollDirection * e.Data.RollStrength;
			e.TargetRoll = Math.Clamp( recoilImpulse, -MaxRollAngle, MaxRollAngle );
			e.CurrentRoll = MoveTowards( e.CurrentRoll, e.TargetRoll, RollSpeed * WorldTime.Delta );

			camera.WorldRotation *= Rotation.From( new Angles( 0f, 0f, e.CurrentRoll ) );
		}

		// Fully settled
		if ( !isActive && MathF.Abs( e.CurrentRoll ) < 0.01f )
		{
			e.CurrentRoll = 0f;
			e.TargetRoll = 0f;
			return true;
		}

		return false;
	}

	private static bool UpdateFadeEffect( CameraComponent camera, ref ActiveEffect e )
	{
		if ( e.DelayTimer < e.Data.Delay )
		{
			e.DelayTimer = Math.Min( e.DelayTimer + WorldTime.Delta, e.Data.Delay );
			return false;
		}

		e.Timer += WorldTime.Delta;

		if ( e.Timer >= e.Data.Duration )
			return true;

		// Trauma-based strength squaring for stacking hits
		float traumaStrength = _trauma * _trauma;
		float t = e.Data.FalloffCurve.HasValue
			? e.Data.FalloffCurve.Value.Evaluate( e.Timer / e.Data.Duration )
			: 1f - Math.Clamp( e.Timer / e.Data.Duration, 0f, 1f );

		float strength = (e.Data.ShakeStrength + traumaStrength) * t;

		// Frequency: use timer to drive oscillation instead of random
		float freq = e.Data.Frequency > 0f ? e.Data.Frequency : 10f;
		float noise = MathF.Sin( e.Timer * freq * MathF.PI );

		// Directional bias from source position
		Angles directionalBias = default;
		if ( e.Data.SourcePosition.HasValue && camera.GameObject.IsValid() )
		{
			Vector3 toSource = (e.Data.SourcePosition.Value - camera.WorldPosition).Normal;
			Vector3 localDir = camera.Transform.World.NormalToLocal( toSource );
			directionalBias = new Angles( localDir.z * strength, localDir.y * strength, 0f );
		}

		Angles delta = new(
			e.Data.ShakePitch ? (Game.Random.Float( -strength, strength ) + directionalBias.pitch) * noise : 0f,
			e.Data.ShakeYaw ? (Game.Random.Float( -strength, strength ) + directionalBias.yaw) * noise : 0f,
			e.Data.ShakeRoll ? Game.Random.Float( -strength, strength ) * noise : 0f
		);

		camera.WorldRotation *= Rotation.From( delta );

		// Decay trauma over time
		_trauma = Math.Max( _trauma - WorldTime.Delta * 0.5f, 0f );

		return false;
	}

	public static DebugTextBlock GetDebugText()
	{
		var block = new DebugTextBlock();
		ActiveEffect? ramp = null;
		int rampCount = 0;

		foreach ( var effect in _activeEffects )
		{
			if ( !effect.IsRampBased ) continue;
			ramp ??= effect;
			rampCount++;
		}

		int shakeCount = _activeEffects.Count - rampCount;

		block.Header( "CAMERA EFFECTS DEBUG" );
		block.Line( "Active effects", $"{_activeEffects.Count}", Color.White );
		block.Line( "Recoil / Shake", $"{rampCount} / {shakeCount}", Color.Green );
		block.Space();

		block.Header( "Recoil" );
		if ( ramp is ActiveEffect recoil )
		{
			float fraction = Math.Clamp( recoil.RampTime / recoil.Data.RampSpeed, 0f, 1f );
			float strength = Math.Clamp( recoil.Data.StrengthCurve.Evaluate( EasingPlus.EaseOutCubic( fraction ) ), 0f, recoil.MaxCurveStrength );
			bool isActive = WorldTime.Now - _lastTriggerTime <= recoil.Data.ResetThreshold;
			bool newPulse = _lastTriggerTime > recoil.LastProcessedAttackTime;

			block.Line( "Ramp fraction", $"{fraction:0.00}", Color.Green );
			block.Line( "Strength / Max", $"{strength:0.00} / {recoil.MaxCurveStrength:0.00}", Color.Green );
			block.Line( "Active", $"{isActive}", Color.Cyan );
			block.Line( "New pulse", $"{newPulse}", Color.Cyan );
			block.Line( "Roll direction", $"{recoil.RollDirection:0}", Color.Cyan );
			block.Line( "Roll / Target", $"{recoil.CurrentRoll:0.00} / {recoil.TargetRoll:0.00}", Color.Green );
			block.Line( "Hold time", $"{recoil.HoldTimer:0.00}s", Color.Orange );
			block.Line( "Ramp time", $"{recoil.RampTime:0.00}s", Color.Orange );
		}
		else
		{
			block.Line( "Status", "Inactive", Color.White );
		}

		block.Space();

		if ( shakeCount == 0 )
		{
			block.Header( "Shake" );
			block.Line( "Status", "Inactive", Color.White );
		}
		else
		{
			int index = 0;
			foreach ( var effect in _activeEffects )
			{
				if ( effect.IsRampBased ) continue;
				index++;
				if ( index > 1 ) block.Space();
				block.Header( shakeCount > 1 ? $"Shake {index}" : "Shake" );
				block.Line( "Time / Duration", $"{effect.Timer:0.00} / {effect.Data.Duration:0.00}s", Color.Orange );
				block.Line( "Strength", $"{effect.Data.ShakeStrength:0.00}", Color.Green );
				block.Line( "Frequency", $"{effect.Data.Frequency:0.00}", Color.Green );
				block.Line( "Axes", $"P:{effect.Data.ShakePitch} Y:{effect.Data.ShakeYaw} R:{effect.Data.ShakeRoll}", Color.Cyan );
				block.Line( "Directional", $"{effect.Data.SourcePosition.HasValue}", Color.Cyan );
			}
		}
		return block;
	}

	// Config

	public const float MaxRollAngle = 3f;
	public const float RollSpeed = 10f;

	[ConVar( "debug_cameraeffects", Help = "Show/Log CameraEffects debug." )] public static bool IsDebug { get; set; } = false;
	[ConVar( "debug_cameraeffects_disable", Help = "Force disable CameraEffects." )] public static bool IsForceDisabled { get; set; } = false;

	// Helpers

	private static float CacheMaxCurveStrength( Curve curve )
	{
		float max = 0f;

		for ( float t = 0f; t <= 1f; t += 0.01f )
		{
			float value = curve.Evaluate( t );
			if ( value > max )
				max = value;
		}

		return max;
	}

	private static float MoveTowards( float current, float target, float maxDelta )
	{
		if ( MathF.Abs( target - current ) <= maxDelta )
			return target;
		return current + MathF.Sign( target - current ) * maxDelta;
	}
}
