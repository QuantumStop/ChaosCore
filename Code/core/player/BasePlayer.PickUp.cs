namespace Core;

#if FMOD
using FMODSbox;
#endif
using Sandbox.Physics;
public partial class BasePlayer
{
	[Property, Feature( "PickUp" )] public BasePlayer PickUpOwner { get; private set; }
	[Property, ReadOnly, Feature( "PickUp" )] public GameObject HeldProp { get; private set; }
	[Property, ReadOnly, Feature( "PickUp" )] public Rigidbody PropPhys { get; private set; }
	[Property, ReadOnly, Feature( "PickUp" )] public Angles PropRelativeRot { get; private set; }

	private PhysicsJoint Joint;
	private bool UseSuccess { get; set; } = false;

	private Vector3 predictedPosition;
	private Rotation predictedRotation;

	private Vector3 targetPosition;
	private Rotation targetRotation;

	[ConVar( "debug_nomass", ConVarFlags.Cheat )]
	public static bool DebugNoMass { get; set; }

	public void PickUpObject( GameObject obj )
	{
		if ( Networking.IsHost || !Networking.IsActive )
		{
			TryPickup( obj );
		}
		else
		{
			PredictPickup( obj );
			RequestPickupRpc( obj, PickUpOwner );
		}
	}

	public void DropObject( bool punt = false )
	{
		if ( !HeldProp.IsValid() || !PropPhys.IsValid() || !PickUpOwner.IsValid() )
			return;

		if ( Networking.IsHost || !Networking.IsActive )
		{
			ApplyDropPhysics( punt );
		}
		else
		{
			predictedPosition += PickUpOwner.Controller.EyeAngles.Forward * (punt ? 400f * Time.Delta : 0f);
		}

		CleanupHeldProp();
	}

	public void UpdatePickup()
	{
		if ( !PickUpOwner.IsValid() || !Input.Pressed( "use" ) || PickUpOwner.LifeState != LifeState.Alive )
			return;

		UseSuccess = false;

		if ( HeldProp.IsValid() )
		{
			if ( Networking.IsHost || !Networking.IsActive )
				DropObject();
			else
				RequestDropRpc( false );
			return;
		}

		var tr = Scene.Trace.Ray( PickUpOwner.Controller?.AimRay ?? default, 100f )
			.IgnoreGameObjectHierarchy( this.GameObject )
			.WithoutTags( "trigger", "water" )
			.HitTriggers()
			.Run();

		if ( tr.Hit && tr.GameObject.IsValid() )
		{
			PickUpObject( tr.GameObject );
		}

#if FMOD
		if ( UseSuccess ) FMODSound.Play( "event:/Player/HUD/UseSuccess" );
		else FMODSound.Play( "event:/Player/HUD/UseDeny" );
#else
		if ( UseSuccess ) Sound.Play( "usesuccess" ).Volume = 0.25f;
		else Sound.Play( "usedeny" ).Volume = 0.25f;
#endif
	}


	private void TryPickup( GameObject obj )
	{
		if ( !PickUpOwner.IsValid() || PickUpOwner.LifeState != LifeState.Alive )
			return;

		// no rigid body (static) or motion disabled
		if ( obj.Components.TryGet<Rigidbody>( out var rigidbody ) ) { if ( !rigidbody.MotionEnabled ) return; }
		else
			return;

		if ( obj.Components.TryGet<BaseWeaponItem>( out var weapon ) )
		{
			UseSuccess = true; // for weapon holding we want successful sound, to know its actually happening
			return;
		}

		if ( obj.Components.TryGet<BaseUsable>( out var usable ) )
			if ( !usable.CanBeHeld ) return;

		if ( !DebugNoMass && rigidbody.Mass > 35 )
			return;

		HeldProp = obj;
		PropPhys = rigidbody;
		PropRelativeRot = HeldProp.WorldRotation.Angles() - PickUpOwner.Controller.EyeAngles.WithPitch( 0 );
		HeldProp.Tags.Add( "HELD_PROP" );

		PickUpOwner.CurrentWeapon?.Holster();

		if ( PickUpOwner.Controller.Controller.PhysicsBodyRigidbody?.PhysicsBody is not null )
		{
			var point1 = new PhysicsPoint( PropPhys.PhysicsBody );
			var point2 = new PhysicsPoint( PickUpOwner.Controller.Controller.PhysicsBodyRigidbody.PhysicsBody );
			Joint = PhysicsJoint.CreateSpring( point1, point2, 0, 99999 );
			Joint.Collisions = false;
		}

		predictedPosition = HeldProp.WorldPosition;
		predictedRotation = HeldProp.WorldRotation;
		targetPosition = predictedPosition;
		targetRotation = predictedRotation;

		OnPickupConfirmed( HeldProp );

		if ( Networking.IsActive )
			OnPickupConfirmedRpc( HeldProp );

		UseSuccess = true;
	}

	private void PredictPickup( GameObject obj )
	{
		HeldProp = obj;
		PropPhys = obj.Components.Get<Rigidbody>();

		predictedPosition = obj.WorldPosition;
		predictedRotation = obj.WorldRotation;

		targetPosition = predictedPosition;
		targetRotation = predictedRotation;

		HeldProp.Tags.Add( "HELD_PROP" ); // ensure client knows it's held
	}

	private void ApplyDropPhysics( bool punt )
	{
		PropPhys.PhysicsBody.Velocity += PickUpOwner.Controller.Controller.Velocity;
		PropPhys.PhysicsBody.Velocity = PropPhys.PhysicsBody.Velocity.ClampLength( 350f );

		if ( punt )
			PropPhys.PhysicsBody.Velocity += PickUpOwner.Controller.EyeAngles.Forward * 400f;

		if ( HeldProp.Components.TryGet<BaseUsable>( out var usable ) )
			usable.OnDropped?.Invoke( PickUpOwner );

		PropPhys.PhysicsBody.AngularVelocity *= 0.3f;

		targetPosition = PropPhys.PhysicsBody.Position;
		targetRotation = PropPhys.PhysicsBody.Rotation;
	}

	private void CleanupHeldProp()
	{
		HeldProp?.Tags.Remove( "HELD_PROP" );
		HeldProp = null;
		PropPhys = null;

		if ( Joint.IsValid() )
			Joint.Remove();

		PickUpOwner.CurrentWeapon?.Draw();
	}

	[Rpc.Broadcast] private void OnPickupConfirmedRpc( GameObject obj ) => OnPickupConfirmed( obj );
	private void OnPickupConfirmed( GameObject obj )
	{
		HeldProp = obj;
		PropPhys = obj.Components.Get<Rigidbody>();

		targetPosition = obj.WorldPosition;
		targetRotation = obj.WorldRotation;
	}

	[Rpc.Host] private void RequestDropRpc( bool punt ) { DropObject( punt ); OnDropConfirmedRpc(); }
	[Rpc.Broadcast] private void OnDropConfirmedRpc() => HeldProp = null;
	[Rpc.Host] private void RequestPickupRpc( GameObject obj, BasePlayer requestingPlayer ) { PickUpOwner = requestingPlayer; TryPickup( obj ); }


	/// <summary>
	/// Helper method for updating pickup physics 
	/// </summary>
	public void UpdatePickupPhysics()
	{
		if ( !PickUpOwner.IsValid() || !PickUpOwner.Controller.Controller.IsValid() )
			return;

		// Handle new pickups / drop requests first
		UpdatePickup();

		if ( !HeldProp.IsValid() || !PropPhys.PhysicsBody.IsValid() )
			return;

		// Drop if dead or standing on held prop
		if ( PickUpOwner.LifeState != LifeState.Alive || PickUpOwner.Controller.Controller.GroundObject == HeldProp )
		{
			DropObject();
			return;
		}

		// Drop if too far
		if ( Vector3.DistanceBetween( PropPhys.PhysicsBody.MassCenter, PickUpOwner.Controller.Head.WorldPosition ) > 128f )
		{
			DropObject();
			return;
		}

		if ( !Networking.IsHost )
		{
			// Client-side prediction
			predictedPosition = Vector3.Lerp( predictedPosition, targetPosition, 0.2f );
			predictedRotation = Rotation.Slerp( predictedRotation, targetRotation, 0.2f );

			PropPhys.PhysicsBody.Position = predictedPosition;
			PropPhys.PhysicsBody.Rotation = predictedRotation;
		}
		else
		{
			// Host authoritative movement
			var wantedRotation = (PropRelativeRot + PickUpOwner.Controller.EyeAngles.WithPitch( 0 )).ToRotation();
			var wantedPosition = PickUpOwner.Controller.Head.WorldPosition + PickUpOwner.Controller.EyeAngles.Forward * 80f;
			wantedPosition += HeldProp.WorldPosition - PropPhys.PhysicsBody.MassCenter;

			var vel = PropPhys.PhysicsBody.Velocity;
			var angvel = PropPhys.PhysicsBody.AngularVelocity;

			Vector3.SmoothDamp( PropPhys.PhysicsBody.Position, wantedPosition, ref vel, 0.05f, Time.Delta );
			Rotation.SmoothDamp( PropPhys.PhysicsBody.Rotation, wantedRotation, ref angvel, 0.05f, Time.Delta );

			vel = vel.ClampLength( 1250f );

			PropPhys.PhysicsBody.Velocity = vel;
			PropPhys.PhysicsBody.AngularVelocity = angvel;

			// Update target positions for clients
			targetPosition = PropPhys.PhysicsBody.Position;
			targetRotation = PropPhys.PhysicsBody.Rotation;
		}

		// Drop / punt input
		if ( Input.Pressed( "attack1" ) )
		{
			if ( Networking.IsActive )
				RequestDropRpc( true );
			else
				DropObject( true );
		}
	}
}
