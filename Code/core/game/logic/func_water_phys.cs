using System;
using Sandbox.UI;

namespace Core;

[Category( "Physics" ), Title( "func_water_phys" )]
public class func_water_phys : BaseEntity
{
	protected override string GetEditorVis() => "materials/editor/waterlodcontrol.tga";

	[Property, Feature( "Debug" ), ReadOnly] public BoxCollider BoxCollider { get; set; }

	protected override void OnEnabled()
	{
		Setup();
	}

	protected override void OnDisabled()
	{
		BoxCollider = null;
	}

	[Button( "Setup Box Collider" ), Feature( "Debug" )]
	private void Setup()
	{
		BoxCollider ??= Components.GetOrCreate<BoxCollider>();
		BoxCollider.IsTrigger = true;
	}

	/// <summary>
	/// If this is hammer/scene mesh water, do needed changes on it too
	/// </summary>

	[Button( "Setup Mesh Component" ), Feature( "Debug" )]
	private void SetupMesh()
	{
		if ( Components.TryGet<MeshComponent>( out var mesh ) )
		{
			mesh.Collision = MeshComponent.CollisionType.None;
			mesh.RenderType = ModelRenderer.ShadowRenderType.Off;
		}
		else
		{
			Log.Warning( "No mesh component found!" );
		}
	}

	private Vector3 _pos => WorldPosition.WithZ( WorldPosition.z - MathF.Abs( BoxCollider.Center.z ) );
	private Plane _plane => new() { Distance = Scene.WorldPosition.Distance( _pos.WithZ( _pos.z + MathF.Abs( BoxCollider.Scale.z * 0.5f ) ) ) };

	protected override void OnFixedUpdate()
	{
		if ( !BoxCollider.IsValid() || !BoxCollider.Touching.Any() ) return;

		foreach ( var touch in BoxCollider.Touching )
		{
			if ( touch.Components.TryGet<Rigidbody>( out var body ) )
				body.ApplyBuoyancy( _plane, Time.Delta );
			else
				continue; // not sure if needed
		}
	}
}
