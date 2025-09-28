namespace Core;

public partial class GameProp
{
	private bool _static;

	[DebugExpose]
	[Header( "Physics" )]
	[Property, Order( 20 )]
	[Description( "This object doesn't move (like prop_static)." )]
	public bool IsStatic
	{
		get => _static;
		set
		{
			if ( _static != value )
			{
				_static = value;

				CanBeHeldAccessor = !value;

				if ( this.Active && ProceduralComponents != null )
				{
					ClearProcedurals();
					UpdateComponents();
				}
			}
		}
	}

	[DebugExpose]
	[Property, Order( 21 )]
	[ShowIf( nameof( IsStatic ), false )]
	[Description( "Physics will be asleep until it's woken up." )]
	public bool StartAsleep { get; set; }

	void CreatePhysicsComponent()
	{
		if ( Model.Physics == null || Model.Physics.Parts.Count == 0 ) return;

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
}
