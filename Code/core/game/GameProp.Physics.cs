namespace Core;

public partial class GameProp
{
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
		if ( Model.Physics is null || Model.Physics.Parts.Count == 0 ) return;

		if ( IsStatic )
		{
			ModelCollider orCreate = Components.GetOrCreate<ModelCollider>();
			orCreate.Static = true;
			orCreate.Model = Model;
			AddProcedural( orCreate );
			return;
		}

		if ( Model.Physics.Parts.Count == 1 )
		{
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

		var p = Components.GetOrCreate<ModelPhysics>();
		p.Renderer = ProceduralComponents?.OfType<SkinnedModelRenderer>().FirstOrDefault();
		p.Model = Model;
		AddProcedural( p );
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
			rb?.PhysicsBody.ApplyImpulse( f );
			rb?.PhysicsBody.ApplyAngularImpulse( af );
		}
	}

}
