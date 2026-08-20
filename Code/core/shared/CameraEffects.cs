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

	private static readonly List<ActiveEffect> activeEffects = new();

	// Public API

	public static void Push( CameraEffect effect, float initialTriggerTime = -1f, float initialRampTime = 0f )
	{
		if ( IsForceDisabled ) return;

		activeEffects.Add( new ActiveEffect
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
		for ( int i = 0; i < activeEffects.Count; i++ )
		{
			var e = activeEffects[i];
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

			activeEffects[i] = e;
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

	private static float trauma = 0f;

	/// <summary>
	/// Accumulates trauma (0-1). Trauma is squared when applied to shake strength,
	/// so stacking hits feel progressively worse. Decays passively over time.
	/// </summary>
	public static void AddTrauma( float amount )
	{
		if ( IsForceDisabled ) return;

		trauma = Math.Clamp( trauma + amount, 0f, 1f );
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

		if ( !IsForceDisabled )
		{
			for ( int i = activeEffects.Count - 1; i >= 0; i-- )
			{
				var e = activeEffects[i];
				bool finished = false;

				if ( e.IsRampBased )
					finished = UpdateRampEffect( camera, ref e, lastTriggerTime ?? -1f );
				else
					finished = UpdateFadeEffect( camera, ref e );

				if ( finished )
					activeEffects.RemoveAt( i );
				else
					activeEffects[i] = e;
			}
		}

		if ( IsDebug )
			DrawDebug( lastTriggerTime ?? -1f );
	}

	public static void Reset() => activeEffects.Clear();


	// Evaluators

	private static bool UpdateRampEffect( CameraComponent camera, ref ActiveEffect e, float lastTriggerTime )
	{
		bool isActive = (WorldTime.Now - lastTriggerTime) <= e.Data.ResetThreshold;
		bool newPulse = lastTriggerTime > e.LastProcessedAttackTime;

		if ( isActive )
		{
			if ( !e.Data.RampPerPulse )
			{
				e.RampTime += Time.Delta;
				e.RampTime = Math.Clamp( e.RampTime, 0f, e.Data.RampSpeed );
			}

			e.HoldTimer = 0f;
		}
		else
		{
			e.HoldTimer += Time.Delta;

			if ( e.HoldTimer > e.Data.ResetThreshold )
			{
				e.RampTime -= Time.Delta;
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
			e.TargetRoll = Math.Clamp( recoilImpulse, -maxRollAngle, maxRollAngle );
			e.CurrentRoll = MoveTowards( e.CurrentRoll, e.TargetRoll, rollSpeed * Time.Delta );

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
			e.DelayTimer = Math.Min( e.DelayTimer + Time.Delta, e.Data.Delay );
			return false;
		}

		e.Timer += Time.Delta;

		if ( e.Timer >= e.Data.Duration )
			return true;

		// Trauma-based strength squaring for stacking hits
		float traumaStrength = trauma * trauma;
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
		trauma = Math.Max( trauma - Time.Delta * 0.5f, 0f );

		return false;
	}

	private static void DrawDebug( float lastTriggerTime )
	{
		// Find ramp effect for recoil section
		ActiveEffect? ramp = null;
		foreach ( var fx in activeEffects )
		{
			if ( !fx.IsRampBased ) continue;
			ramp = fx;
			break;
		}

		bool isActive = ramp.HasValue && (WorldTime.Now - lastTriggerTime) <= ramp.Value.Data.ResetThreshold;
		bool newPulse = ramp.HasValue && lastTriggerTime > ramp.Value.LastProcessedAttackTime;

		float rampT = 0f;
		float targetStrength = 0f;

		if ( ramp.HasValue )
		{
			var e = ramp.Value;
			rampT = Math.Clamp( e.RampTime / e.Data.RampSpeed, 0f, 1f );
			targetStrength = e.Data.StrengthCurve.Evaluate( EasingPlus.EaseOutCubic( rampT ) );
			targetStrength = Math.Clamp( targetStrength, 0f, e.MaxCurveStrength );
		}

		float x = Screen.Width - 300f;
		float y = 20f;
		float lineHeight = 0.85f;

		var scope = new TextRendering.Scope
		{
			FontName = "RobotoMono",
			FontSize = 11f,
			FontWeight = 500,
			TextColor = Color.White,
			LineHeight = lineHeight
		};

		scope.Outline.Enabled = true;
		scope.Outline.Color = Color.Black;
		scope.Outline.Size = 3.25f;

		// Header
		scope.Text += "CAMERA EFFECTS DEBUG\n";
		scope.Text += "\n";

		// Active effects
		{
			int rampCount = 0;
			int fadeCount = 0;
			foreach ( var fx in activeEffects )
			{
				if ( fx.IsRampBased ) rampCount++;
				else fadeCount++;
			}

			scope.Text += "Active Effects:\n";
			scope.Text += $"   Total:        {activeEffects.Count}\n";
			scope.Text += $"   Ramp-based:   {rampCount}\n";
			scope.Text += $"   Fade-based:   {fadeCount}\n";
			scope.Text += "\n";
		}

		// Recoil
		{
			scope.Text += "Recoil:\n";

			if ( !ramp.HasValue )
			{
				scope.Text += "   No active recoil\n";
			}
			else
			{
				var e = ramp.Value;
				scope.Text += $"   RampT:        {rampT:F2}\n";
				scope.Text += $"   Strength:     {targetStrength:F2} / Max: {e.MaxCurveStrength:F2}\n";
				scope.Text += $"   IsActive:     {isActive}\n";
				scope.Text += $"   NewPulse:     {newPulse}\n";
				scope.Text += $"   RollDir:      {e.RollDirection:F0}\n";
				scope.Text += $"   CurrentRoll:  {e.CurrentRoll:F2}\n";
				scope.Text += $"   TargetRoll:   {e.TargetRoll:F2}\n";
				scope.Text += $"   HoldTimer:    {e.HoldTimer:F2}\n";
				scope.Text += $"   RampTime:     {e.RampTime:F2}\n";
			}

			scope.Text += "\n";
		}

		// Shake
		{
			int fadeCount = 0;
			foreach ( var fx in activeEffects )
				if ( !fx.IsRampBased ) fadeCount++;

			scope.Text += "Shake:\n";

			if ( fadeCount == 0 )
			{
				scope.Text += "   No active shake\n";
			}
			else
			{
				foreach ( var fx in activeEffects )
				{
					if ( fx.IsRampBased ) continue;
					scope.Text += $"   Timer:        {fx.Timer:F2} / {fx.Data.Duration:F2}\n";
					scope.Text += $"   Strength:     {fx.Data.ShakeStrength:F2}\n";
					scope.Text += $"   Frequency:    {fx.Data.Frequency:F2}\n";
					scope.Text += $"   Axes:         P:{fx.Data.ShakePitch} Y:{fx.Data.ShakeYaw} R:{fx.Data.ShakeRoll}\n";
					scope.Text += $"   Directional:  {fx.Data.SourcePosition.HasValue}\n";
					scope.Text += "   · · · · · · · · · · · · ·\n";
				}
			}
		}

		DebugOverlaySystem.Current.ScreenText( new Vector2( x, y ), scope, TextFlag.Left, 0f );
	}

	// Config

	public static float maxRollAngle = 3f;
	public static float rollSpeed = 10f;

	[ConVar( "ch_cameraeffect_debug", Help = "Show/Log CameraEffects debug" ),] public static bool IsDebug { get; set; } = false;
	[ConVar( "ch_cameraeffect_disable", Help = "Force Disable CameraEffects" ),] public static bool IsForceDisabled { get; set; } = false;

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
