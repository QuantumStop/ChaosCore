namespace Core;

using System;

/// <summary>
/// Wrapper component for managing physics states of an NPC, allowing for easier switching between gameplay and ragdoll states.
/// </summary>
[Title( "NPC Physics" ), Category( "NPC" ), Icon( "accessibility_new" )]
public sealed class NpcPhysics : Component
{
	public enum PhysicsStateSelection
	{
		Gameplay,
		Ragdoll
	}
	private bool _filterDirty = true;

	[Property]
	public Model Model
	{
		get;
		set
		{
			if ( field == value )
				return;

			field = value;
			SyncWrappedModelPhysics();
			_filterDirty = true;
		}
	}

	[Property]
	public SkinnedModelRenderer Renderer
	{
		get;
		set
		{
			if ( field == value )
				return;

			field = value;
			SyncWrappedModelPhysics();
			_filterDirty = true;
		}
	}

	[Property]
	public PhysicsStateSelection ActiveState
	{
		get;
		set
		{
			if ( field == value )
				return;

			field = value;
			SyncWrappedModelPhysics();
			_filterDirty = true;
		}
	} = PhysicsStateSelection.Gameplay;

	[Property]
	public bool IgnoreRoot
	{
		get;
		set
		{
			if ( field == value )
				return;

			field = value;
			SyncWrappedModelPhysics();
		}
	}

	[Property, Group( "Physics" )]
	public RigidbodyFlags RigidbodyFlags
	{
		get;
		set
		{
			if ( field == value )
				return;

			field = value;
			SyncWrappedModelPhysics();
		}
	}

	[Property, Group( "Physics" )]
	public PhysicsLock Locking
	{
		get;
		set
		{
			if ( field.Equals( value ) )
				return;

			field = value;
			SyncWrappedModelPhysics();
		}
	}

	[Property, Group( "Physics" )]
	public bool StartAsleep
	{
		get;
		set
		{
			if ( field == value )
				return;

			field = value;
			SyncWrappedModelPhysics();
		}
	}

	[Property, Group( "Physics" )]
	public bool MotionEnabled
	{
		get;
		set
		{
			if ( field == value )
				return;

			field = value;
			SyncWrappedModelPhysics();
		}
	} = true;

	private ModelPhysics Wrapped { get; set; }

	public void EnterGameplayState() => ActiveState = PhysicsStateSelection.Gameplay;
	public void EnterRagdollState() => ActiveState = PhysicsStateSelection.Ragdoll;

	protected override void OnAwake()
	{
		Wrapped = Components.GetOrCreate<ModelPhysics>();
		Wrapped.Flags |= ComponentFlags.NotEditable;

		Renderer ??= GetComponent<SkinnedModelRenderer>();

		if ( !Model.IsValid() && Renderer.IsValid() )
			Model = Renderer.Model;

		SyncWrappedModelPhysics();
	}

	protected override void OnEnabled()
	{
		Wrapped ??= Components.GetOrCreate<ModelPhysics>();
		Wrapped.Flags |= ComponentFlags.NotEditable;
		SyncWrappedModelPhysics();

		if ( Wrapped.IsValid() )
			Wrapped.Enabled = true;

		_filterDirty = true;
	}

	protected override void OnDisabled()
	{
		if ( Wrapped.IsValid() )
			Wrapped.Enabled = false;
	}

	protected override void OnUpdate()
	{
		if ( _filterDirty )
			ApplyStateSelection();
	}

	private void SyncWrappedModelPhysics()
	{
		Wrapped ??= Components.GetOrCreate<ModelPhysics>();
		if ( !Wrapped.IsValid() )
			return;

		Wrapped.Model = Model;
		Wrapped.Renderer = Renderer;
		Wrapped.IgnoreRoot = IgnoreRoot;
		Wrapped.RigidbodyFlags = RigidbodyFlags;
		Wrapped.Locking = Locking;
		Wrapped.StartAsleep = StartAsleep;

		// Gameplay is kinematic-styled. Ragdoll needs motion to be enabled for sim, 
		// but we still want the option to disable motion in ragdoll state if desired.

		Wrapped.MotionEnabled = ActiveState == PhysicsStateSelection.Ragdoll && MotionEnabled;
	}

	private void ApplyStateSelection()
	{
		if ( !Wrapped.IsValid() || !Wrapped.PhysicsWereCreated )
			return;

		var colliders = GetComponentsInChildren<Collider>( true )
			.Where( x => x.IsValid() && x.GameObject.Flags.Contains( GameObjectFlags.PhysicsBone ) )
			.ToList();

		foreach ( var collider in colliders )
		{
			collider.Enabled = true;
		}

		var rigidBodies = GetComponentsInChildren<Rigidbody>( true )
			.Where( x => x.IsValid() && x.GameObject.Flags.Contains( GameObjectFlags.PhysicsBone ) );

		foreach ( var body in rigidBodies )
		{
			var hasEnabledCollider = body.GameObject.Components.GetAll<Collider>( FindMode.EnabledInSelf )
				.Any( x => x.Enabled );
			body.Enabled = hasEnabledCollider;
		}

		var enableConstraints = ActiveState == PhysicsStateSelection.Ragdoll;
		var constraintComponents = GetComponentsInChildren<Component>( true )
			.Where( x => x.IsValid()
				&& x.GameObject.Flags.Contains( GameObjectFlags.PhysicsBone )
				&& IsPhysicsConstraintComponent( x ) );

		foreach ( var constraint in constraintComponents )
		{
			constraint.Enabled = enableConstraints;
		}

		_filterDirty = false;
	}

	private static bool IsPhysicsConstraintComponent( Component component )
	{
		var typeName = component.GetType().Name;
		return typeName.Contains( "Joint", StringComparison.OrdinalIgnoreCase )
			|| typeName.Contains( "Hinge", StringComparison.OrdinalIgnoreCase )
			|| typeName.Contains( "Constraint", StringComparison.OrdinalIgnoreCase );
	}
}
