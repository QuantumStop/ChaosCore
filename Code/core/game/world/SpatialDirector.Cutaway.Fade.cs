namespace Core;

using Sandbox;
using System;

public sealed partial class SpatialDirector
{
	/// <summary>
	/// Eases the cut hit point to avoid visible popping when the occlusion trace is blocked/unblocked.
	/// </summary>
	private void UpdateHitFollow()
	{
		if ( !_hasHitPosition )
			return;

		var duration = GetCurveDuration( HitFollowCurve );
		if ( _hitFollowTime >= duration )
		{
			CutawayHitWorld = _targetHitWorld;
			return;
		}

		_hitFollowTime = MathF.Min( _hitFollowTime + Time.Delta, duration );
		var eased = EvaluateTimedCurve( HitFollowCurve, _hitFollowTime );

		CutawayHitWorld = _hitFollowStartWorld + (_targetHitWorld - _hitFollowStartWorld) * eased;
	}

	/// <summary>
	/// Fades cut strength when the occlusion state changes.
	/// </summary>
	private void UpdateFade()
	{
		if ( TargetTraceBlocked != _wasBlocked )
		{
			_fadeTime = 0f;
			_fadeStartStrength = CutawayStrength;
			_fadeTargetStrength = TargetTraceBlocked ? 1f : 0f;
			_wasBlocked = TargetTraceBlocked;
		}

		var curve = TargetTraceBlocked
			? FadeInCurve
			: FadeOutCurve;

		var duration = GetCurveDuration( curve );
	
		if ( _fadeTime >= duration )
		{
			CutawayStrength = _fadeTargetStrength;
			return;
		}

		_fadeTime = MathF.Min( _fadeTime + Time.Delta, duration );
		var eased = EvaluateTimedCurve( curve, _fadeTime );

		CutawayStrength = _fadeStartStrength + (_fadeTargetStrength - _fadeStartStrength) * eased;
	}

	/// <summary>
	/// Blends the view between outside and inside.
	/// </summary>
	private void UpdateViewTransition()
	{
		if ( IsTargetInside != _wasTargetInside )
		{
			_viewTime = 0f;
			_viewStartBlend = ViewBlend;
			_viewTargetBlend = IsTargetInside ? 1f : 0f;
			_wasTargetInside = IsTargetInside;
		}

		var duration = GetCurveDuration( ViewTransitionCurve );
		
		if ( _viewTime >= duration )
		{
			ViewBlend = _viewTargetBlend;
			return;
		}

		_viewTime = MathF.Min( _viewTime + Time.Delta, duration );
		var eased = EvaluateTimedCurve( ViewTransitionCurve, _viewTime );

		ViewBlend = _viewStartBlend + (_viewTargetBlend - _viewStartBlend) * eased;
	}

	private void UpdateTextureAnimation()
	{
		var hasCookie = CutawayCookie is not null && CutawayCookie.IsValid;
		var hasNoise = CutawayNoise is not null && CutawayNoise.IsValid && CutawayNoiseStrength > 0f;

		if ( hasCookie && Shape != CutawayShapeMode.Custom && MathF.Abs( CutawayCookieScrollSpeed ) > 0.0001f )
			_cookieScroll = Wrap01( _cookieScroll + WorldTime.Delta * CutawayCookieScrollSpeed );
		else if ( Shape == CutawayShapeMode.Custom )
			_cookieScroll = 0f;

		if ( hasNoise && MathF.Abs( CutawayNoiseScrollSpeed ) > 0.0001f )
			_noiseScroll = Wrap01( _noiseScroll + WorldTime.Delta * CutawayNoiseScrollSpeed );
	}

	private static float Wrap01( float value )
	{
		value -= MathF.Floor( value );
		return value < 0f ? value + 1f : value;
	}

	private static float GetCurveDuration( Curve curve ) => MathF.Max( curve.TimeRange.y - curve.TimeRange.x, 0.001f );

	private static float EvaluateTimedCurve( Curve curve, float elapsed )
	{
		var duration = GetCurveDuration( curve );
		var time = curve.TimeRange.x + Math.Clamp( elapsed, 0f, duration );

		return Math.Clamp( curve.Evaluate( time ), 0f, 1f );
	}
}
