namespace Core;

#if FMOD
using FMODSbox;
#endif
using Sandbox.Physics;
using System;

public partial class BasePlayer
{
	public readonly record struct GrabState( GameObject GameObject, Rigidbody Body, Vector3 LocalOffset, Angles GrabOffset, float GrabDistance )
	{
		public bool IsValid => GameObject.IsValid() && Body.IsValid() && Body.PhysicsBody.IsValid();

		public Vector3 EndPoint => !IsValid ? default : Body.PhysicsBody.Transform.PointToWorld( LocalOffset );
	}


	[Property, ReadOnly, Feature( "PickUp" ), Sync]
	public GrabState PickupState
	{
		get;
		private set
		{
			if ( field == value ) return;
			OnPickupStateChanged( field, value );
			field = value;
		}
	}

	private ControlJoint _joint;
	private PhysicsBody _controlBody;
	private bool _useSuccess { get; set; } = false;

	[ConVar( "debug_nomass", ConVarFlags.Cheat )]
	public static bool DebugNoMass { get; set; }

	public void DropObject( bool punt = false )
	{
		if ( !PickupState.IsValid ) return;

		var playerVelocity = Controller.Controller.Velocity;
		var puntDirection = Controller.EyeAngles.Forward;

		ApplyDropPhysics( PickupState, punt, playerVelocity, puntDirection );

		if ( GameManagerSystem.Rules.IsOnline && !Networking.IsHost ) DropObjectHost( PickupState, punt, playerVelocity, puntDirection );
		else if ( Networking.IsHost ) ReleasePickupClaim( PickupState );

		CleanupHeldProp();
	}

	private bool IsHeldByAnyone( GameObject obj )
	{
		if ( obj.Tags.Has( "held_prop" ) ) return true;

		var root = GetNetworkRoot( obj );
		return root.IsValid() && root.Tags.Has( "held_prop" );
	}

	private GameObject GetHeldObject( GameObject obj )
	{
		var root = GetNetworkRoot( obj );
		return root.IsValid() ? root : obj;
	}

	private GameObject GetNetworkRoot( GameObject obj )
	{
		GameObject root = obj.Network.Active ? obj : null;
		var current = obj.Parent;

		while ( current.IsValid() && !current.IsRoot )
		{
			if ( current.Network.Active )
				root = current;

			current = current.Parent;
		}

		return root;
	}

	private void TryTakePickupOwnership( GrabState state )
	{
		if ( GameManagerSystem.Rules.IsOnline || !state.GameObject.Network.Active ) return;

		if ( state.GameObject.Network.IsOwner ) return;

		state.GameObject.Network.TakeOwnership();
	}

	private void DropPickupOwnership( GrabState state )
	{
		if ( !GameManagerSystem.Rules.IsOnline || !state.GameObject.Network.Active ) return;

		if ( !state.GameObject.Network.IsOwner ) return;

		state.GameObject.Network.DropOwnership();
	}

	private bool ValidatePickupState( GrabState state )
	{
		state = ResolvePickupState( state );

		if ( !state.IsValid || !state.Body.MotionEnabled )
			return false;

		if ( state.GameObject.Tags.Has( "held_prop" ) )
			return false;

		if ( !DebugNoMass && state.Body.Mass > 35 )
			return false;

		if ( state.GameObject.Components.TryGet<BaseUsable>( out var usable ) && !usable.CanBeHeld )
			return false;

		return true;
	}

	private void ClaimPickupOnHost( GrabState state, Connection owner )
	{
		state = ResolvePickupState( state );

		if ( !ValidatePickupState( state ) )
		{
			RejectPickup( state );
			return;
		}

		PickupState = state;
		SetHeldPropTag( state.GameObject, true );
		SetHeldPropTagBroadcast( state.GameObject, true );

		if ( owner is not null && state.GameObject.Network.Active )
			state.GameObject.Network.AssignOwnership( owner );
	}

	[Rpc.Host]
	private void ClaimPickupHost( GrabState state ) => ClaimPickupOnHost( state, Rpc.Caller );

	private GrabState ResolvePickupState( GrabState state )
	{
		if ( !state.GameObject.IsValid() ) return state;

		if ( state.Body.IsValid() ) return state;

		var body = state.GameObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndChildren );

		if ( !body.IsValid() ) body = state.GameObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndParent );

		if ( !body.IsValid() ) return state;

		return state with { Body = body };
	}

	[Rpc.Owner]
	private void RejectPickup( GrabState state )
	{
		if ( PickupState.GameObject != state.GameObject ) return;

		DropPickupOwnership( state );
		CleanupHeldProp();
	}

	[Rpc.Broadcast] private void SetHeldPropTagBroadcast( GameObject obj, bool held ) => SetHeldPropTag( obj, held );

	private void SetHeldPropTag( GameObject obj, bool held )
	{
		if ( !obj.IsValid() ) return;

		obj.Tags.Set( "held_prop", held );
	}

	public void UpdatePickup()
	{
		if ( !Input.Pressed( "use" ) || LifeState != LifeState.Alive )
			return;

		_useSuccess = false;

		if ( PickupState.IsValid )
		{
			DropObject();
			return;
		}

		var tr = Scene.Trace.Ray( Controller?.AimRay ?? default, 100f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger", "water", "held_prop", "player" )
			.HitTriggers()
			.Run();

		if ( tr.Hit && tr.GameObject.IsValid() ) TryPickup( tr );

#if FMOD
		if ( _useSuccess ) FMODSound.Play( "event:/Player/HUD/UseSuccess" );
		else FMODSound.Play( "event:/Player/HUD/UseDeny" );
#else
		if ( _useSuccess ) Sound.Play( "usesuccess" ).Volume = 0.25f;
		else Sound.Play( "usedeny" ).Volume = 0.25f;
#endif
	}


	private void TryPickup( SceneTraceResult tr )
	{
		var obj = tr.GameObject;
		var heldObject = GetHeldObject( obj );

		if ( LifeState != LifeState.Alive || IsHeldByAnyone( obj ) ) return; // important to filter out already held objects

		var all = obj.Components.GetAll<BaseUsable>();

		foreach ( var usable in all )
		{
			if ( !usable.IsValid() ) continue;

			if ( usable.CanInteract ) _useSuccess = true;
			if ( !usable.CanBeHeld && all.Count() > 1 ) continue;
			if ( !usable.CanBeHeld ) return;
		}

		if ( !TryFindPickupRigidbody( tr, out var rigidbody ) ) return;

		if ( !rigidbody.MotionEnabled ) return;

		if ( !DebugNoMass && rigidbody.Mass > 35 ) return;

		var bodyTransform = rigidbody.PhysicsBody.Transform.WithScale( obj.WorldScale );
		var grabOffset = obj.WorldRotation.Angles() - Controller.EyeAngles.WithPitch( 0 );
		var state = new GrabState( heldObject, rigidbody, bodyTransform.PointToLocal( tr.HitPosition ), grabOffset, 80f );

		if ( Networking.IsHost )
		{
			ClaimPickupOnHost( state, Owner?.Connection ?? Connection.Local );
		}
		else
		{
			PickupState = state;
			TryTakePickupOwnership( state );

			if ( GameManagerSystem.Rules.IsOnline ) ClaimPickupHost( state );
		}

		CurrentWeapon?.Holster( HolsterType.Pickup );
	}

	private bool TryFindPickupRigidbody( SceneTraceResult tr, out Rigidbody rigidbody )
	{
		rigidbody = tr.Body?.Component as Rigidbody;

		if ( rigidbody.IsValid() )
			return true;

		rigidbody = tr.Component?.GameObject?.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndParent );

		if ( rigidbody.IsValid() )
			return true;

		rigidbody = tr.GameObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndParent );

		return rigidbody.IsValid();
	}

	private void OnPickupStateChanged( GrabState oldState, GrabState newState )
	{
		var shouldPredictHeldTag = IsControlledLocally && !Networking.IsHost;

		if ( shouldPredictHeldTag ) SetHeldPropTag( oldState.GameObject, false );

		if ( newState.GameObject.IsValid() )
		{
			if ( shouldPredictHeldTag ) SetHeldPropTag( newState.GameObject, true );

			CurrentWeapon?.Holster( HolsterType.Pickup );
			return;
		}

		ClearPickupJoint();
		CurrentWeapon?.Draw( HolsterType.Pickup );
	}

	private bool CanMove( GrabState state )
	{
		if ( !state.IsValid ) return false;

		if ( state.Body.IsProxy ) return false;

		if ( !state.Body.MotionEnabled ) return false;

		return state.Body.PhysicsBody.IsValid();
	}

	[Rpc.Host]
	private void DropObjectHost( GrabState state, bool punt, Vector3 playerVelocity, Vector3 puntDirection )
	{
		state = ResolvePickupState( state );
		ApplyDropPhysics( state, punt, playerVelocity, puntDirection );

		if ( PickupState.GameObject == state.GameObject )
		{
			ReleasePickupClaim( state );
			CleanupHeldProp();
		}
	}

	private void ReleasePickupClaim( GrabState state )
	{
		SetHeldPropTag( state.GameObject, false );
		SetHeldPropTagBroadcast( state.GameObject, false );

		if ( state.GameObject.Network.Active && !state.GameObject.Network.IsProxy )
			state.GameObject.Network.DropOwnership();
	}

	private void ApplyDropPhysics( GrabState state, bool punt, Vector3 playerVelocity, Vector3 puntDirection )
	{
		if ( !CanMove( state ) ) return;

		var velocity = state.Body.PhysicsBody.Velocity + playerVelocity;
		velocity = velocity.ClampLength( 350f );

		if ( punt ) velocity += puntDirection * 400f;

		state.Body.PhysicsBody.Velocity = velocity;

		if ( state.GameObject.Components.TryGet<BaseUsable>( out var usable ) )
			usable.OnDropped?.Invoke( this );

		state.Body.PhysicsBody.AngularVelocity *= 0.3f;
	}

	private void CleanupHeldProp()
	{
		SetHeldPropTag( PickupState.GameObject, false );
		PickupState = default;

		ClearPickupJoint();
	}

	private void ClearPickupJoint()
	{
		if ( _joint.IsValid() )
			_joint.Remove();

		_joint = null;
		_controlBody?.Remove();
		_controlBody = null;
	}


	/// <summary>
	/// Helper method for updating pickup physics 
	/// </summary>
	public void UpdatePickupPhysics()
	{
		if ( !Controller.Controller.IsValid() ) return;

		if ( IsControlledLocally ) UpdatePickup();

		if ( !PickupState.IsValid )
		{
			ClearPickupJoint();
			return;
		}

		// Drop if dead or standing on held prop
		if ( LifeState != LifeState.Alive || Controller.Controller.GroundObject == PickupState.GameObject )
		{
			DropObject();
			return;
		}

		// Drop if too far
		if ( Vector3.DistanceBetween( PickupState.Body.PhysicsBody.MassCenter, Controller.Head.WorldPosition ) > 128f )
		{
			DropObject();
			return;
		}

		UpdatePickupJoint();

		// Drop / punt input
		if ( IsControlledLocally && Input.Pressed( "attack1" ) ) DropObject( true );
	}

	private void UpdatePickupJoint()
	{
		var wantedPosition = Controller.Head.WorldPosition + Controller.EyeAngles.Forward * PickupState.GrabDistance;
		wantedPosition += PickupState.GameObject.WorldPosition - PickupState.Body.PhysicsBody.MassCenter;
		var wantedRotation = (PickupState.GrabOffset + Controller.EyeAngles.WithPitch( 0 )).ToRotation();

		if ( !CanMove( PickupState ) )
		{
			if ( IsControlledLocally && GameManagerSystem.Rules.IsOnline && !Networking.IsHost ) UpdatePickupTargetHost( PickupState, wantedPosition, wantedRotation );
			else ClearPickupJoint();

			return;
		}

		UpdatePickupJoint( PickupState, wantedPosition, wantedRotation );
	}

	[Rpc.Host]
	private void UpdatePickupTargetHost( GrabState state, Vector3 wantedPosition, Rotation wantedRotation )
	{
		if ( PickupState.GameObject != state.GameObject || !CanMove( state ) ) return;

		UpdatePickupJoint( state, wantedPosition, wantedRotation );
	}

	private void UpdatePickupJoint( GrabState state, Vector3 wantedPosition, Rotation wantedRotation )
	{
		_controlBody ??= new PhysicsBody( Scene.PhysicsWorld )
		{
			BodyType = PhysicsBodyType.Keyframed,
			AutoSleep = false
		};

		_controlBody.Transform = new Transform( wantedPosition, wantedRotation );

		if ( !_joint.IsValid() )
		{
			var maxForce = MathF.Max( state.Body.PhysicsBody.Mass, 1f ) * Scene.PhysicsWorld.Gravity.LengthSquared;

			_joint = PhysicsJoint.CreateControl( new PhysicsPoint( _controlBody ), new PhysicsPoint( state.Body.PhysicsBody ) );
			_joint.LinearSpring = new PhysicsSpring( 32, 4, maxForce );
			_joint.AngularSpring = new PhysicsSpring( 64, 4, maxForce * 3 );
			_joint.Collisions = false;
		}
	}
}
