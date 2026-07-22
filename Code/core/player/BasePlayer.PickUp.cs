namespace Core;

#if FMOD
using FMODSbox;
#endif
using Sandbox.Physics;
using Sandbox.Platform;

public partial class BasePlayer
{
	[Property, ReadOnly, Feature( "PickUp" ), Sync] public GameObject HeldProp { get; private set; }
	[Property, ReadOnly, Feature( "PickUp" ), Sync] public Rigidbody PropPhys { get; private set; }
	[Property, ReadOnly, Feature( "PickUp" )] public Angles PropRelativeRot { get; private set; }

	private PhysicsJoint _joint;
	private bool _useSuccess { get; set; } = false;

	private Vector3 _predictedPosition;
	private Rotation _predictedRotation;

	private Vector3 _targetPosition;
	private Rotation _targetRotation;

	[ConVar( "debug_nomass", ConVarFlags.Cheat )]
	public static bool DebugNoMass { get; set; }


	public void DropObject( bool punt = false )
	{
		if ( !HeldProp.IsValid() || !PropPhys.IsValid() )
			return;

		if ( Networking.IsHost )
		{
			ApplyDropPhysics( punt );
		}
		else
		{
			_predictedPosition += Controller.EyeAngles.Forward * (punt ? 400f * Time.Delta : 0f);
		}

		CleanupHeldProp();
	}

	public void UpdatePickup()
	{
		if ( !Input.Pressed( "use" ) || Local.LifeState != LifeState.Alive )
			return;

		_useSuccess = false;

		if ( HeldProp.IsValid() )
		{
			DropObject();
			return;
		}

		var tr = Scene.Trace.Ray( Controller?.AimRay ?? default, 100f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "water" )
			.HitTriggers()
			.Run();

		if ( tr.Hit && tr.GameObject.IsValid() ) TryPickup( tr.GameObject );

#if FMOD
		if ( _useSuccess ) FMODSound.Play( "event:/Player/HUD/UseSuccess" );
		else FMODSound.Play( "event:/Player/HUD/UseDeny" );
#else
		if ( _useSuccess ) Sound.Play( "usesuccess" ).Volume = 0.25f;
		else Sound.Play( "usedeny" ).Volume = 0.25f;
#endif
	}


	private void TryPickup( GameObject obj )
	{
		if ( LifeState != LifeState.Alive || obj.Tags.Has( "HELD_PROP" ) ) // important to filter out already held objects
			return;

		// no rigid body (static) or motion disabled
		if ( obj.Components.TryGet<Rigidbody>( out var rigidbody ) ) { if ( !rigidbody.MotionEnabled ) return; }
		else
			return;

		if ( obj.Components.TryGet<BaseWeaponItem>( out var weapon ) )
		{
			_useSuccess = true; // for weapon holding we want successful sound, to know its actually happening
			return;
		}

		if ( obj.Components.TryGet<BaseUsable>( out var usable ) )
			if ( !usable.CanBeHeld ) return;

		if ( !DebugNoMass && rigidbody.Mass > 35 )
			return;

		RpcPickup( obj, rigidbody );
		PropRelativeRot = HeldProp.WorldRotation.Angles() - Controller.EyeAngles.WithPitch( 0 );

		CurrentWeapon?.Holster();

		if ( Controller.Controller.PhysicsBodyRigidbody?.PhysicsBody is not null )
		{
			var point1 = new PhysicsPoint( PropPhys.PhysicsBody );
			var point2 = new PhysicsPoint( Controller.Controller.PhysicsBodyRigidbody.PhysicsBody );
			_joint = PhysicsJoint.CreateSpring( point1, point2, 0, 99999 );
			_joint.Collisions = false;
		}

		_useSuccess = true;
	}

	[Rpc.Broadcast]
	private void RpcPickup( GameObject obj, Rigidbody rigidbody )
	{
		HeldProp = obj;
		PropPhys = rigidbody;

		_predictedPosition = obj.WorldPosition;
		_predictedRotation = obj.WorldRotation;
		_targetPosition = _predictedPosition;
		_targetRotation = _predictedRotation;

		HeldProp?.Tags.Add( "HELD_PROP" ); // ensure client knows it's held
	}

	private void ApplyDropPhysics( bool punt )
	{
		PropPhys.PhysicsBody.Velocity += Controller.Controller.Velocity;
		PropPhys.PhysicsBody.Velocity = PropPhys.PhysicsBody.Velocity.ClampLength( 350f );

		if ( punt )
			PropPhys.PhysicsBody.Velocity += Controller.EyeAngles.Forward * 400f;

		if ( HeldProp.Components.TryGet<BaseUsable>( out var usable ) )
			usable.OnDropped?.Invoke( Local );

		PropPhys.PhysicsBody.AngularVelocity *= 0.3f;

		_targetPosition = PropPhys.PhysicsBody.Position;
		_targetRotation = PropPhys.PhysicsBody.Rotation;
	}

	[Rpc.Broadcast]
	private void CleanupHeldProp()
	{
		HeldProp?.Tags.Remove( "HELD_PROP" );
		HeldProp = null;
		PropPhys = null;

		if ( _joint.IsValid() ) _joint.Remove();

		CurrentWeapon?.Draw();
	}


	/// <summary>
	/// Helper method for updating pickup physics 
	/// </summary>
	public void UpdatePickupPhysics()
	{
		if ( !Controller.Controller.IsValid() )
			return;

		// Handle new pickups / drop requests first
		UpdatePickup();

		if ( !HeldProp.IsValid() || !PropPhys.IsValid() || !PropPhys.PhysicsBody.IsValid() )
			return;

		// Drop if dead or standing on held prop
		if ( LifeState != LifeState.Alive || Controller.Controller.GroundObject == HeldProp )
		{
			DropObject();
			return;
		}

		// Drop if too far
		if ( Vector3.DistanceBetween( PropPhys.PhysicsBody.MassCenter, Controller.Head.WorldPosition ) > 128f )
		{
			DropObject();
			return;
		}

		if ( Network.IsOwner || !Networking.IsActive )
		{
			var wantedPosition = Controller.Head.WorldPosition + Controller.EyeAngles.Forward * 80f;
			wantedPosition += HeldProp.WorldPosition - PropPhys.PhysicsBody.MassCenter;

			var vel = PropPhys.PhysicsBody.Velocity;
			var angvel = PropPhys.PhysicsBody.AngularVelocity;

			Vector3.SmoothDamp( PropPhys.PhysicsBody.Position, wantedPosition, ref vel, 0.05f, Time.Delta );
			Rotation.SmoothDamp( PropPhys.PhysicsBody.Rotation, (PropRelativeRot + Controller.EyeAngles.WithPitch( 0 )).ToRotation(), ref angvel, 0.05f, Time.Delta );

			vel = vel.ClampLength( 1250f );

			PropPhys.PhysicsBody.Velocity = vel;
			PropPhys.PhysicsBody.AngularVelocity = angvel;

			// Update target positions for clients
			_targetPosition = PropPhys.PhysicsBody.Position;
			_targetRotation = PropPhys.PhysicsBody.Rotation;
		}
		else
		{
			// Client-side prediction
			_predictedPosition = Vector3.Lerp( _predictedPosition, _targetPosition, 0.2f );
			_predictedRotation = Rotation.Slerp( _predictedRotation, _targetRotation, 0.2f );


			PropPhys.PhysicsBody.Position = _predictedPosition;
			PropPhys.PhysicsBody.Rotation = _predictedRotation;
		}

		// Drop / punt input
		if ( Input.Pressed( "attack1" ) ) DropObject( true );
	}
}
