namespace Core;

public partial class GameProp
{
	private ModelPhysics _modelPhysics { get; set; }

#if IGNIS
	[DebugExpose]
#endif
	[Group( "Physics Properties" )]
	[Property, Order( 13 )]
	[Description( "This object doesn't move (like prop_static)." )]
	public bool IsStatic
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;

				CanBeHeldAccessor = !value;

				if ( Active && ProceduralComponents is not null )
				{
					ClearProcedurals();
					UpdateComponents();
				}
			}
		}
	}

#if IGNIS
	[DebugExpose]
#endif
	[Group( "Physics Properties" )]
	[Property, Order( 13 )]
	[ShowIf( nameof( IsStatic ), false )]
	[Description( "Physics will be asleep until it's woken up." )]
	public bool StartAsleep { get; set; }

#if IGNIS
	[DebugExpose]
#endif
	[Sync]
	[Group( "Physics Properties" )]
	[Property, Order( 13 )]
	[ShowIf( nameof( IsStatic ), false )]
	[Description( "For multi-body models, lets physics drive the skinned renderer." )]
	public bool RagdollActive { get; set; } = true;

#if IGNIS
	[DebugExpose]
#endif
	[Group( "Physics Properties" )]
	[Property, Order( 13 )]
	public bool OverrideMass
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				OnMassChange();
			}
		}
	} = false;

	[Property, Order( 13 )]
	[Group( "Physics Properties" )]
	[ShowIf( nameof( OverrideMass ), true )]
	public float NewMass
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				OnMassChange();
			}
		}
	} = 10f;

	private void OnMassChange()
	{
		Rigidbody rb = Components.Get<Rigidbody>();

		if ( rb.IsValid() )
		{
			if ( OverrideMass )
				rb.MassOverride = NewMass;
			else
				rb.MassOverride = 0;
		}
	}

	void CreatePhysicsComponent()
	{
		if ( Model.Physics is null || Model.Physics.Parts.Count == 0 )
		{
			DestroyRagdollPhysicsComponent();
			return;
		}

		if ( IsStatic )
		{
			DestroyRagdollPhysicsComponent();

			ModelCollider orCreate = Components.GetOrCreate<ModelCollider>();
			orCreate.Static = true;
			orCreate.Model = Model;
			AddProcedural( orCreate );
			return;
		}

		if ( Model.Physics.Parts.Count == 1 )
		{
			DestroyRagdollPhysicsComponent();

			var collider = Components.GetOrCreate<ModelCollider>();
			collider.Model = Model;
			collider.Static = false;

			// collider.Flags |= procFlags;

			AddProcedural( collider );

			var rigidBody = Components.GetOrCreate<Rigidbody>();

			// Need to initially sync mass here if we override it, can
			// continue changing the mass from there on too as well
			if ( OverrideMass )
				rigidBody.MassOverride = NewMass;

			if ( StartAsleep )
			{
				rigidBody.StartAsleep = true;
				if ( rigidBody.PhysicsBody.IsValid() )
				{
					rigidBody.PhysicsBody.Sleeping = true;
				}
			}

			AddProcedural( rigidBody );

			return;
		}

		CreateRagdollPhysicsComponent();
	}

	private void CreateRagdollPhysicsComponent()
	{
		_modelPhysics = Components.GetOrCreate<ModelPhysics>();
		_modelPhysics.Model = Model;
		_modelPhysics.Renderer = _modelRenderer as SkinnedModelRenderer ?? Components.Get<SkinnedModelRenderer>();
		_modelPhysics.StartAsleep = StartAsleep;
		_modelPhysics.MotionEnabled = RagdollActive;
		_modelPhysics.Flags |= ComponentFlags.Hidden;
	}

	private void DestroyRagdollPhysicsComponent()
	{
		_modelPhysics ??= Components.Get<ModelPhysics>();
		if ( !_modelPhysics.IsValid() )
			return;

		_modelPhysics.Destroy();
		_modelPhysics = null;
	}

	public void SetRagdollActive( bool active, bool copyCurrentPose = true )
	{
		if ( IsProxy )
			return;

		RagdollActive = active;
		ApplyRagdollState( copyCurrentPose );
		NetworkSetRagdollActive( active, copyCurrentPose );
	}

	[Rpc.Broadcast]
	private void NetworkSetRagdollActive( bool active, bool copyCurrentPose )
	{
		if ( !IsProxy )
			return;

		RagdollActive = active;
		ApplyRagdollState( copyCurrentPose );
	}

	private void ApplyRagdollState( bool copyCurrentPose )
	{
		_modelPhysics ??= Components.Get<ModelPhysics>();
		if ( !_modelPhysics.IsValid() )
			return;

		_modelPhysics.Model = Model;
		_modelPhysics.Renderer ??= _modelRenderer as SkinnedModelRenderer ?? Components.Get<SkinnedModelRenderer>();

		if ( copyCurrentPose && _modelPhysics.Renderer.IsValid() )
			_modelPhysics.CopyBonesFrom( _modelPhysics.Renderer, true );

		_modelPhysics.MotionEnabled = RagdollActive;
	}

	public void PassImpulse( Vector3? force = null, Vector3? angularForce = null, bool? includeChildren = false )
	{
		Vector3 f = force ?? Vector3.Zero;
		Vector3 af = angularForce ?? Vector3.Zero;

		bool childInclude = includeChildren ?? false;

		if ( childInclude )
		{
			foreach ( var rb in Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndChildren ) )
			{
				rb?.PhysicsBody.ApplyImpulse( f );
				rb?.PhysicsBody.ApplyAngularImpulse( af );
			}
		}
		else
		{
			Components.TryGet<Rigidbody>( out var rb );
			if ( rb.IsValid() )
			{
				rb.PhysicsBody.ApplyImpulse( f );
				rb.PhysicsBody.ApplyAngularImpulse( af );
				return;
			}

			_modelPhysics ??= Components.Get<ModelPhysics>();
			if ( !_modelPhysics.IsValid() )
				return;

			var body = _modelPhysics.Bodies.FirstOrDefault().Component;
			if ( !body.IsValid() || !body.PhysicsBody.IsValid() )
				return;

			body.PhysicsBody.ApplyImpulse( f );
			body.PhysicsBody.ApplyAngularImpulse( af );
		}
	}

}
