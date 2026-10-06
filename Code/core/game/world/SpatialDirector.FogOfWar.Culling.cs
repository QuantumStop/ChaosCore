namespace Core;

using Sandbox;
using System;

public sealed partial class SpatialDirector
{
	[Property, Feature( "Culling" ), Group( "Renderers" )]
	public bool CullHiddenModelRenderers { get; set; } = true;

	[Property, Feature( "Culling" ), Group( "Renderers" )]
	public bool CullStaticRenderers { get; set; } = false;

	[Property, Feature( "Culling" ), Group( "Renderers" )]
	public bool UseBoundsForVisibility { get; set; } = true;

	/// <summary>
	/// Hides rendered objects that are supposed to be not seen,
	/// while keeping their original vis state.
	/// </summary>
	private void UpdateRendererVisibility()
	{
		ResetRendererCullingDebug();

		if ( Scene is null )
			return;

		if ( !CullHiddenModelRenderers )
		{
			RestoreRendererVisibility();
			return;
		}

		foreach ( var renderer in Scene.GetAllComponents<ModelRenderer>() )
		{
			if ( renderer is null || !renderer.IsValid )
				continue;
			
			var sceneObject = renderer.SceneObject;

			if ( !sceneObject.IsValid() )
				continue;

			// Don't hide our own pawn renderer.
			if ( Target.IsValid() &&
				 (renderer.GameObject == Target.GameObject || Target.GameObject.IsDescendant( renderer.GameObject )) )
			{
				RememberRendererInitialState( renderer );

				sceneObject.RenderingEnabled = _rendererOriginalVisibility[renderer];

				continue;
			}

			// Static map geometry generally shouldn't disappear
			if ( renderer.GameObject.IsStatic && !CullStaticRenderers )
			{
				RememberRendererInitialState( renderer );

				sceneObject.RenderingEnabled = _rendererOriginalVisibility[renderer];

				continue;
			}

			RememberRendererInitialState( renderer );

			var originallyVisible = _rendererOriginalVisibility[renderer];

			if ( !originallyVisible )
			{
				sceneObject.RenderingEnabled = false;

				continue;
			}

			_rendererSamplesInRange = 0;
			var visible = !renderer.GameObject.IsStatic
				? IsDynamicRendererVisible( renderer )
				: UseBoundsForVisibility
					? IsRendererBoundsVisible( renderer.Bounds )
					: IsVisible( renderer.WorldPosition );

			sceneObject.RenderingEnabled = visible;
			RecordRendererCullingDebug( renderer, visible );
		}
	}

	private bool IsDynamicRendererVisible( ModelRenderer renderer )
	{
		if ( !Target.IsValid() )
			return false;

		var origin = VisionWorldPosition;
		var bounds = renderer.Bounds;
		var radius = MathF.Max( VisionRadius, 0f );
		var radiusSquared = radius * radius;

		// Only reject bounds when their nearest XY point is outside vision range.
		var nearest = new Vector3( Math.Clamp( origin.x, bounds.Mins.x, bounds.Maxs.x ),
			Math.Clamp( origin.y, bounds.Mins.y, bounds.Maxs.y ), origin.z );

		if ( UseBoundsForVisibility && (nearest - origin).LengthSquared > radiusSquared )
			return false;

		var entity = renderer.Components.Get<BaseEntity>( FindMode.EverythingInSelfAndAncestors );
		var ignore = entity.IsValid() ? entity.GameObject : renderer.GameObject;
		var center = UseBoundsForVisibility ? bounds.Center : renderer.WorldPosition;

		if ( IsRendererPointVisible( origin, center, ignore, radiusSquared ) )
			return true;

		if ( !UseBoundsForVisibility )
			return false;

		if ( entity is BasePawn )
		{
			// Keep samples within the body: world AABB corners can protrude through walls.
			var heightOffset = (bounds.Maxs.z - bounds.Mins.z) * 0.25f;
			return IsRendererPointVisible( origin, center + Vector3.Up * heightOffset, ignore, radiusSquared )
				|| IsRendererPointVisible( origin, center - Vector3.Up * heightOffset, ignore, radiusSquared );
		}

		if ( renderer.Model is null )
			return false;

		// Inset face centers rotate with the prop, avoiding empty world AABB corners.
		var localBounds = renderer.Model.Bounds;
		var localCenter = localBounds.Center;
		var extent = localBounds.Size * 0.4f;
		var transform = renderer.WorldTransform;
		return IsRendererPointVisible( origin, transform.PointToWorld( localCenter + new Vector3( extent.x, 0f, 0f ) ), ignore, radiusSquared )
			|| IsRendererPointVisible( origin, transform.PointToWorld( localCenter - new Vector3( extent.x, 0f, 0f ) ), ignore, radiusSquared )
			|| IsRendererPointVisible( origin, transform.PointToWorld( localCenter + new Vector3( 0f, extent.y, 0f ) ), ignore, radiusSquared )
			|| IsRendererPointVisible( origin, transform.PointToWorld( localCenter - new Vector3( 0f, extent.y, 0f ) ), ignore, radiusSquared )
			|| IsRendererPointVisible( origin, transform.PointToWorld( localCenter + new Vector3( 0f, 0f, extent.z ) ), ignore, radiusSquared )
			|| IsRendererPointVisible( origin, transform.PointToWorld( localCenter - new Vector3( 0f, 0f, extent.z ) ), ignore, radiusSquared );
	}

	/// <summary>
	/// Checks if a renderer is visible from the given origin point, ignoring the specified object.
	/// Uses a squared radius for distance checks to avoid square root calculations.
	/// </summary>
	/// <param name="origin">The origin point to check visibility from.</param>
	/// <param name="point">The point to check visibility to.</param>
	/// <param name="ignore">The game object to ignore during visibility checks.</param>
	/// <param name="radiusSquared">The squared radius for distance checks.</param>
	/// <returns>true if the point is visible.</returns>
	private bool IsRendererPointVisible( Vector3 origin, Vector3 point, GameObject ignore, float radiusSquared )
	{
		if ( (point - origin).WithZ( 0f ).LengthSquared > radiusSquared || !IsWithinVisionArc( point, origin ) )
		{
			DrawRendererSampleDebug( origin, point, Color.Yellow );
			return false;
		}

		_rendererSamplesInRange++;
		return HasRendererLineOfSight( origin, point, ignore );
	}
	/// <summary>
	/// Checks if there is a clear line of sight between the origin and point, ignoring the specified game object.
	/// </summary>
	/// <param name="origin">The origin point to check line of sight from.</param>
	/// <param name="point">The point to check line of sight to.</param>
	/// <param name="ignore">The game object to ignore during line of sight checks.</param>
	/// <returns>true if there is a clear line of sight; otherwise, false.</returns>
	private bool HasRendererLineOfSight( Vector3 origin, Vector3 point, GameObject ignore )
	{
		// Include all static objects and our own occluders.
		var hit = Scene.Trace.Ray( origin, point )
			.WithAnyTags( "world", "blocker", "occluder" )
			.IgnoreGameObjectHierarchy( Target.GameObject )
			.IgnoreGameObjectHierarchy( ignore )
			.Run();

		_rendererCullingTraces++;
		var visible = !hit.Hit && !hit.StartedSolid;
		DrawRendererSampleDebug( origin, point, visible ? Color.Green : Color.Red );
		if ( DebugMode >= 2 && hit.Hit )
			DebugOverlaySystem.Current.Sphere( new Sphere( hit.EndPosition, 2.5f ), Color.Red, 0f, overlay: true );
		return visible;
	}

	private void RememberRendererInitialState( ModelRenderer renderer )
	{
		if ( _rendererOriginalVisibility.ContainsKey( renderer ) )
			return;
		
		var sceneObject = renderer.SceneObject;

		if ( !sceneObject.IsValid() )
			return;

		_rendererOriginalVisibility[renderer] = sceneObject.RenderingEnabled;
	}

	/// <summary>
	/// Checks the center and four XY corners of static renderer bounds.
	/// </summary>
	private bool IsRendererBoundsVisible( BBox bounds )
	{
		// Visibility is an XY grid, so use the center height for each sample.
		var z = bounds.Center.z;
		return IsVisible( bounds.Center )
			|| IsVisible( new Vector3( bounds.Mins.x, bounds.Mins.y, z ) )
			|| IsVisible( new Vector3( bounds.Maxs.x, bounds.Mins.y, z ) )
			|| IsVisible( new Vector3( bounds.Mins.x, bounds.Maxs.y, z ) )
			|| IsVisible( new Vector3( bounds.Maxs.x, bounds.Maxs.y, z ) );
	}

	/// <summary>
	/// Restores only the rendering states previously captured by this component.
	/// </summary>
	private void RestoreRendererVisibility()
	{
		ResetRendererCullingDebug();

		if ( _rendererOriginalVisibility.Count == 0 )
			return;

		foreach ( var pair in _rendererOriginalVisibility )
		{
			var renderer = pair.Key;

			if ( renderer is null || !renderer.IsValid )
				continue;

			var sceneObject = renderer.SceneObject;

			if ( !sceneObject.IsValid() )
				continue;

			sceneObject.RenderingEnabled = pair.Value;
		}

		_rendererOriginalVisibility.Clear();
	}
}
