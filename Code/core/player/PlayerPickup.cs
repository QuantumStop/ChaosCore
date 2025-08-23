using Sandbox.Physics;

[Title( "Player Pickup" )]
[Icon( "Backpack" )]
[Category( "Core" )]
public class PlayerPickup : BaseEntity
{
	BasePlayer Player => BasePlayer.Local;

	[Property, ReadOnly] public GameObject HeldProp { get; set; }
	[Property, ReadOnly] public Rigidbody PropPhys { get; set; }
	[Property, ReadOnly] public Angles PropRelativeRot { get; set; }

	/// <summary>
	/// Ignore mass requirement for the player pickup
	/// </summary>
	[ConVar( "debug_nomass", ConVarFlags.Cheat )]
	public static bool DebugNoMass { get; set; }

	/// <summary>
	/// This is used to know whether we succeeded in pickup
	/// </summary>
	private bool UseSuccess { get; set; } = false;

	/// <summary>
	/// Pickup a GameObject when successful
	/// </summary>
	/// <param name="prop">The gameobject in question</param>
	public void PickUpObject( GameObject prop )
	{
		if ( prop.Components.TryGet<Rigidbody>( out var rigidbody ) )
		{
			if ( !DebugNoMass )
				if ( rigidbody.Mass > 35 ) return;  // cant pickup more than 35 kg

			HeldProp = prop;
			PropPhys = rigidbody;
		}
		else { return; }


		BasePlayer.Local?.CurrentWeapon?.Holster();

		PropRelativeRot = HeldProp.WorldRotation.Angles() - Player.Controller.EyeAngles.WithPitch( 0 );
		HeldProp.Tags.Add( "HELD_PROP" );

		if ( HeldProp.Components.TryGet<BaseUsable>( out var usable ) )
			usable.OnHoldStart?.Invoke( Player );

		if ( Player.Controller.Controller.PhysicsBodyRigidbody.PhysicsBody != null )
		{
			var point1 = new PhysicsPoint( PropPhys.PhysicsBody );
			var point2 = new PhysicsPoint( Player.Controller.Controller.PhysicsBodyRigidbody.PhysicsBody );
			Joint = PhysicsJoint.CreateSpring( point1, point2, 0, 99999 );
			Joint.Collisions = false;
		}
	}

	protected override void OnFixedUpdate()
	{
		UpdatePickup();

		if ( !HeldProp.IsValid() )
			return;

		if ( Player.Controller.Controller.GroundObject == HeldProp || Player.LifeState == LifeState.Dead )
		{
			DropObject();
			return;
		}

		// if where the prop is too far away from where it's supposed to be, drop it
		if ( Vector3.DistanceBetween( PropPhys.PhysicsBody.MassCenter, Player.Controller.Head.WorldPosition ) > 128f ) { DropObject(); return; }

		var WantedRotation = (PropRelativeRot + Player.Controller.EyeAngles.WithPitch( 0 )).ToRotation();
		var WantedPosition = Player.Controller.Head.WorldPosition + Player.Controller.EyeAngles.Forward * 80f;
		WantedPosition += HeldProp.WorldPosition - PropPhys.PhysicsBody.MassCenter;

		var vel = PropPhys.PhysicsBody.Velocity;
		var angvel = PropPhys.PhysicsBody.AngularVelocity;

		Vector3.SmoothDamp( PropPhys.PhysicsBody.Position, WantedPosition, ref vel, 0.05f, Time.Delta );
		Rotation.SmoothDamp( PropPhys.PhysicsBody.Rotation, WantedRotation, ref angvel, 0.05f, Time.Delta );

		// clamping the velocity to prevent it from going too fast
		vel = vel.ClampLength( 1250f );

		PropPhys.PhysicsBody.Velocity = vel;
		PropPhys.PhysicsBody.AngularVelocity = angvel;

		//Log.Info( Vector3.DistanceBetween( PropPhys.PhysicsBody.MassCenter, Player.Controller.Head.WorldPosition ) );

		if ( HeldProp.Components.TryGet<BaseUsable>( out var usable ) )
			usable.OnHoldFixedUpdate?.Invoke( Player );

		if ( Input.Pressed( "attack1" ) )
			DropObject( true );
	}

	protected override void OnUpdate()
	{
		if ( !HeldProp.IsValid() )
			return;

		base.OnUpdate();

		if ( HeldProp.Components.TryGet<BaseUsable>( out var usable ) )
			usable.OnHoldUpdate?.Invoke( Player );
	}


	PhysicsJoint Joint;

	/// <summary>
	/// Run checks if we can pickup the GameObject we got
	/// </summary>
	/// <param name="obj"></param>
	protected void TryPickup( GameObject obj )
	{
		UseSuccess = false;

		// Extra check, just in case. We are dead, don't forget that...
		if ( Player.LifeState == LifeState.Dead ) return;

		bool allowHold = true;

		// no rigid body (static) or motion disabled
		if ( obj.Components.TryGet<Rigidbody>( out var rigid ) ) { if ( !rigid.MotionEnabled ) allowHold = false; }
		else
			allowHold = false;

		if ( obj.Components.TryGet<BaseUsable>( out var usable ) )
			if ( !usable.CanBeHeld ) allowHold = false;

		//		see if we can pick the prop up 
		if ( allowHold )
		{
			PickUpObject( obj );
			UseSuccess = true;
			Sound.Play( "usesuccess" ).ListenLocal = true;
		}
		else
		{
			UseSuccess = false;
			Sound.Play( "usedeny" ).ListenLocal = true;
		}
	}
	/// <summary>
	/// The trace, input checks, sounds
	/// </summary>
	public void UpdatePickup()
	{
		if ( Input.Pressed( "use" )  )
		{

			if ( HeldProp.IsValid() )
			{
				DropObject();
				return;
			}

			var tr = Scene.Trace.Ray( Player.Controller.AimRay, 100f )
				.IgnoreGameObjectHierarchy( this.GameObject )
				.WithoutTags( "trigger" )
				.HitTriggers()
				.Run();

			if ( tr.Hit && tr.GameObject.IsValid() )
			{
				TryPickup( tr.GameObject );
			}
			else
			{
				Sound.Play( "usedeny" ).ListenLocal = true;
			}
		}

	}
	/// <summary>
	/// Drop the object
	/// </summary>
	/// <param name="punt">Do we throw it</param>
	public void DropObject( bool punt = false )
	{
		PropPhys.PhysicsBody.Velocity += Player.Controller.Controller.Velocity;
		PropPhys.PhysicsBody.Velocity = PropPhys.PhysicsBody.Velocity.ClampLength( 350f );

		if ( punt )
			PropPhys.PhysicsBody.Velocity += Player.Controller.EyeAngles.Forward * 400f;

		if ( HeldProp.Components.TryGet<BaseUsable>( out var usable ) )
			usable.OnDropped?.Invoke( Player );

		PropPhys.PhysicsBody.AngularVelocity *= 0.3f;
		HeldProp.Tags.Remove( "HELD_PROP" );
		HeldProp = null;
		PropPhys = null;
		if ( Joint.IsValid() ) Joint.Remove();


		BasePlayer.Local.CurrentWeapon?.Draw();
	}
}
