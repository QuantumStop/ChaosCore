namespace Core;

using Sandbox;
using System;

public sealed partial class SpatialDirector
{
	/// <summary>
	/// Updates the occlusion state of the target, determining whether 
	/// it is blocked by geometry and should be revealed via cutaway.
	/// </summary>
	private void UpdateOcclusion()
	{
		var start = Target.WorldPosition + TargetOffset;
		var end = GetCutawayRayEnd( start );

		CutawayEndWorld = end;

		if ( !UseTargetOcclusionTrace )
		{
			TargetTraceBlocked = true;
			_occlusionGraceRemaining = 0f;
			return;
		}

		// A nearby wall must not trigger reveal unless it intersects this viewing ray.
		// This way we only peek through walls that are actually between the camera 
		// and the target, not just nearby.
		var trace = Scene.Trace.Ray( start, end );
		trace = trace.IgnoreGameObjectHierarchy( Target.GameObject );

		var hit = trace.Run();
		if ( !hit.Hit )
		{
			// Keep the current obstruction briefly so we avoid edge flicker!
			if ( _occlusionGraceRemaining > 0f )
			{
				TargetTraceBlocked = true;
				return;
			}

			TargetTraceBlocked = false;
			return;
		}

		CutawayHitNormalWorld = hit.Normal;

		TargetTraceBlocked = true;
		_occlusionGraceRemaining = 0.12f;
		_targetHitWorld = hit.EndPosition;

		if ( !_hasHitPosition )
		{
			CutawayHitWorld = _targetHitWorld;
			_hitFollowStartWorld = CutawayHitWorld;
			_hitFollowTime = GetCurveDuration( HitFollowCurve );
			_hasHitPosition = true;
		}
		else if ( (_targetHitWorld - CutawayHitWorld).Length > 0.01f )
		{
			_hitFollowStartWorld = CutawayHitWorld;
			_hitFollowTime = 0f;
		}
	}

	/// <summary>
	/// Orthographic rays are parallel, even when the target is off camera center.
	/// </summary>
	private Vector3 GetCutawayRayEnd( Vector3 start )
	{
		var towardCamera = -Camera.WorldRotation.Forward.Normal;
		var distance = MathF.Max( Vector3.Dot( Camera.WorldPosition - start, towardCamera ), 1f );
		return start + towardCamera * distance;
	}

	private void UpdateIgnoredRenderers()
	{
		if ( Scene is null )
			return;

		foreach ( var renderer in Scene.GetAllComponents<Renderer>() )
		{
			if ( renderer is null || !renderer.IsValid )
				continue;

			var ignored = renderer.GameObject.Tags.Has( "cutaway_ignore" );
			renderer.Attributes.Set( "CutawayIgnore", ignored ? 1f : 0f );
		}
	}
}
