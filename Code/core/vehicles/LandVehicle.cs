using Sandbox.Utility;
using System;

public class LandVehicleWheelData : Component
{
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly] public bool OnGround { get; set; } = true;
	[Property, ReadOnly] public string ForceAttachment { get; set; }
	[Property, ReadOnly] public bool WasOnGround { get; set; } = false;
	[Property, ReadOnly] public Vector3 prevPosition { get; set; }
	[Property, ReadOnly] public Vector3 prevVelocity { get; set; }
}

public class LandVehicle : BaseEntity, Component.ExecuteInEditor
{
	[DebugExpose( group: "LandVehicle" )][Property] public SkinnedModelRenderer AnimMesh { get; set; }
	[DebugExpose( group: "LandVehicle" )][Property] public Rigidbody PhysMesh { get; set; }
	protected override string GetEditorVis()
	{
		return "models/vehicles/truck.vmdl";
	}
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly, Feature( "Debug" )] public GameObject WheelFL { get; set; }
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly, Feature( "Debug" )] public GameObject WheelFR { get; set; }
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly, Feature( "Debug" )] public GameObject WheelBL { get; set; }
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly, Feature( "Debug" )] public GameObject WheelBR { get; set; }
	private bool WheelsAreSetup = false;
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly, Feature( "Debug" )] public bool WheelFriction { get; set; }
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly, Feature( "Debug" )] public bool ControlsEnabled { get; set; }
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly, Feature( "Debug" )] public float Steering { get; set; } = 0f;
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly, Feature( "Debug" )] public int Gear { get; set; } = 0;
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly, Feature( "Debug" )] public float AcclPedal { get; set; } = 0f;
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly, Feature( "Debug" )] public float BrakePedal { get; set; } = 0f;
	[DebugExpose( group: "LandVehicle" )][Property, ReadOnly, Feature( "Debug" )] public float LockWheelsTime { get; set; } = 0f;

	protected virtual Model GetWheelModel() { return Model.Load( "models/vehicles/truck_wheel_r.vmdl" ); }

	//public bool PlayerIsDriving() { return Scene.Components.GetAll<PlayerAnimatedInteraction>().First().Active && Scene.Components.GetAll<PlayerAnimatedInteraction>().First().BonemergeTarget == AnimMesh; }

	protected override void OnEnabled()
	{
		base.OnEnabled();
		AnimMesh = GameObject.Components.GetOrCreate<SkinnedModelRenderer>();
		AnimMesh.Model = Model.Load( GetEditorVis() );
		//AnimMesh.OnAnimTagEvent = HandleAnimTag;
		AnimMesh.CreateBoneObjects = true;
		//		AnimMesh.OnAnimTagEvent = HandleAnimTag;
		GameObject.Components.GetOrCreate<ModelCollider>().Model = AnimMesh.Model;

		PhysMesh = GameObject.Components.GetOrCreate<Rigidbody>();
		PhysMesh.MassOverride = 600f;
	}

	protected virtual void HandleAnimTag( SceneModel.AnimTagEvent tag )
	{
		if ( tag.Status != SceneModel.AnimTagStatus.End ) switch ( tag.Name )
			{
				case "entry_finished":
					//					ControlsEnabled = PlayerIsDriving();
					if ( ControlsEnabled )
					{
						//	Scene.Components.GetAll<PlayerAnimatedInteraction>().First().CameraLimitsPitch = new Vector2( -45f, 45f );
						//	Scene.Components.GetAll<PlayerAnimatedInteraction>().First().CameraLimitsYaw = new Vector2( -165f, 165f );
					}
					break;
			}
	}
	protected override void OnFixedUpdate()
	{
		if ( !WheelsAreSetup )
		{
			WheelsAreSetup = true;
			WheelFL = AddWheel( "wheel_fl", GetWheelModel() );
			WheelFR = AddWheel( "wheel_fr", GetWheelModel() );
			WheelBL = AddWheel( "wheel_bl", GetWheelModel() );
			WheelBR = AddWheel( "wheel_br", GetWheelModel() );
			//			GetAnimatedInteractionData().Setup( AnimMesh );
			CreateLights();
		}
		UpdateSuspension( WheelFL );
		UpdateSuspension( WheelFR );
		UpdateSuspension( WheelBL );
		UpdateSuspension( WheelBR );
		if ( ControlsEnabled )
		{
			if ( Input.Pressed( "use" ) )
			{
				//				GetAnimatedInteractionData().ExitInteraction( AnimMesh );
				ControlsEnabled = false;
			}

			UpdateSteering();
			UpdateGear();
			UpdateBrakePedal();
			//	PlayerAnimatedInteraction.StaticRef.AllowRecenter = PhysMesh.Velocity.Length > 60f;
		}
		ApplyBraking( WheelFL );
		ApplyBraking( WheelFR );
		ApplyBraking( WheelBL );
		ApplyBraking( WheelBR );
		var useskid = PhysMesh.Velocity.WithZ( 0f ).Length > 10f || AcclPedal != 0f;
		if ( !useskid )
			useskid = Time.Now <= LockWheelsTime;
		else
			LockWheelsTime = Time.Now + 0.3f;

		if ( Input.Down( "jump" ) )
			useskid = false;

		if ( useskid )
		{
			if ( WheelFriction )
			{
				WheelFL.Components.Get<ModelCollider>().Surface = Surface.FindByName( "2k_tire_low_friction" );
				WheelFR.Components.Get<ModelCollider>().Surface = Surface.FindByName( "2k_tire_low_friction" );
				WheelBL.Components.Get<ModelCollider>().Surface = Surface.FindByName( "2k_tire_low_friction" );
				WheelBR.Components.Get<ModelCollider>().Surface = Surface.FindByName( "2k_tire_low_friction" );
			}

			WheelFriction = false;
			UpdateSkid( WheelFL );
			UpdateSkid( WheelFR );
			UpdateSkid( WheelBL );
			UpdateSkid( WheelBR );
		}
		else
		{
			if ( !WheelFriction )
			{
				WheelFL.Components.Get<ModelCollider>().Surface = Surface.FindByName( "2k_tire_high_friction" );
				WheelFR.Components.Get<ModelCollider>().Surface = Surface.FindByName( "2k_tire_high_friction" );
				WheelBL.Components.Get<ModelCollider>().Surface = Surface.FindByName( "2k_tire_high_friction" );
				WheelBR.Components.Get<ModelCollider>().Surface = Surface.FindByName( "2k_tire_high_friction" );
				WheelFL.Components.Get<LandVehicleWheelData>().WasOnGround = false;
				WheelFR.Components.Get<LandVehicleWheelData>().WasOnGround = false;
				WheelBL.Components.Get<LandVehicleWheelData>().WasOnGround = false;
				WheelBR.Components.Get<LandVehicleWheelData>().WasOnGround = false;
				WheelFL.Components.Get<LandVehicleWheelData>().prevVelocity = Vector3.Zero;
				WheelFR.Components.Get<LandVehicleWheelData>().prevVelocity = Vector3.Zero;
				WheelBL.Components.Get<LandVehicleWheelData>().prevVelocity = Vector3.Zero;
				WheelBR.Components.Get<LandVehicleWheelData>().prevVelocity = Vector3.Zero;
			}

			WheelFriction = true;
		}
		if ( ControlsEnabled )
		{
			UpdateAcclPedal();
			ApplyAccl( WheelFL );
			ApplyAccl( WheelFR );
			ApplyAccl( WheelBL );
			ApplyAccl( WheelBR );
		}
	}

	protected virtual void CreateLights() { }

	public GameObject AddWheel( string attachment, Model wheelmodel )
	{
		var socket = Scene.CreateObject();
		socket.Tags.Add( "allow_to_transition" );
		socket.WorldPosition = AnimMesh.GetAttachment( attachment ).GetValueOrDefault().Position;
		socket.WorldRotation = AnimMesh.GetAttachment( attachment ).GetValueOrDefault().Rotation;
		socket.SetParent( GameObject );
		socket.Name = "socket_" + attachment;

		var wheelphys = Scene.CreateObject();
		wheelphys.Tags.Add( "allow_to_transition" );
		wheelphys.WorldPosition = AnimMesh.GetAttachment( attachment ).GetValueOrDefault().Position - AnimMesh.GetAttachment( attachment ).GetValueOrDefault().Up * 30f;
		wheelphys.WorldRotation = AnimMesh.GetAttachment( attachment ).GetValueOrDefault().Rotation;
		wheelphys.Components.Create<Prop>().Model = wheelmodel;
		wheelphys.Name = GameObject.Name + "_" + attachment;

		var sliderjoint = wheelphys.Components.Create<SliderJoint>();
		sliderjoint.Body = socket;
		sliderjoint.MinLength = -7f;
		sliderjoint.MaxLength = 3f;

		var springjoint = wheelphys.Components.Create<SpringJoint>();
		springjoint.Body = socket;
		springjoint.Frequency = 4.5f;
		springjoint.Damping = 0.6f;

		var wheeldata = wheelphys.Components.Create<LandVehicleWheelData>();
		wheeldata.ForceAttachment = attachment + "_force";

		return wheelphys;
	}

	private void UpdateSuspension( GameObject wheel )
	{
		var wheeldata = wheel.Components.Get<LandVehicleWheelData>();
		var wheelphys = wheel.Components.Get<Rigidbody>();
		SceneTraceResult tr = Scene.Trace.Body( wheelphys.PhysicsBody, wheel.WorldPosition - wheel.WorldRotation.Up * 3f ).IgnoreGameObject( GameObject ).Run();
		wheeldata.OnGround = tr.Hit;

		if ( !tr.Hit )
			wheeldata.WasOnGround = false;
	}

	private float rampIn = 0f;
	private float conflictRefVel = 0f;
	private void UpdateSteering()
	{
		float speed = 0.4f * ((Input.Down( "Left" ) ? -1f : 0f) + (Input.Down( "Right" ) ? 1f : 0f));
		//		ramp in when start turning

		if ( (Input.Down( "Left" ) ? -1f : 0f) + (Input.Down( "Right" ) ? 1f : 0f) != 0f )
		{
			rampIn = Math.Clamp( rampIn + Time.Delta / 0.3f, 0f, 1f );
			speed = speed / Math.Clamp( rampIn + Math.Abs( Steering ), 0.01f, 1f );
		}
		else
		{
			rampIn = 0f;
			//			when no keys held go back to idle but ramp down speed as we get closer to idle
			if ( Steering > 0f )
			{
				speed = -0.3f / Math.Clamp( Math.Abs( Steering ), 0.01f, 1f );
			}
			else if ( Steering < 0f )
			{
				speed = 0.3f / Math.Clamp( Math.Abs( Steering ), 0.01f, 1f );
			}
		}

		//		keep velocity but slow down when both keys held
		if ( (Input.Down( "Left" ) ? 1f : 0f) + (Input.Down( "Right" ) ? 1f : 0f) > 1.5f )
		{
			if ( conflictRefVel > 0f )
			{
				conflictRefVel += Time.Delta / 0.1f;
			}
			else if ( conflictRefVel < 0f )
			{
				conflictRefVel -= Time.Delta / 0.1f;
			}
			speed = conflictRefVel;
		}
		else
		{
			conflictRefVel = speed;
		}

		speed *= 0.9f;

		if ( speed != 0f )
		{
			Steering = Math.Clamp( Steering + Time.Delta / speed, Math.Min( Steering, Input.Down( "Left" ) ? -1f : 0f ), Math.Max( Steering, Input.Down( "Right" ) ? 1f : 0f ) );
		}

		var agsteering = Easing.SineEaseInOut( (Steering * 0.5f) + 0.5f );
		AnimMesh.Set( "f_steering", agsteering );
	}

	private float nextGearChangeTime = 0f;
	private void UpdateGear()
	{
		var localVelocity = PhysMesh.Transform.World.PointToLocal( PhysMesh.WorldPosition + PhysMesh.PhysicsBody.Velocity );
		var targetGear = Gear;
		if ( (Input.Down( "Forward" ) ? 1f : 0f) + (Input.Down( "Backward" ) ? -1f : 0f) != 0 )
			targetGear = (Input.Down( "Forward" ) ? 1 : 0) + (Input.Down( "Backward" ) ? -1 : 0);

		//		cant switch gears if we are moving the wrong direction
		if ( (targetGear > 0 && localVelocity.x > -5f) || (targetGear < 0 && localVelocity.x < 5) || (targetGear == 0 && Math.Abs( localVelocity.x ) < 200) )
		{
			if ( Time.Now > nextGearChangeTime )
				Gear = targetGear;
		}
		else
		{
			nextGearChangeTime = Time.Now + 0.3f;
		}
	}

	private void UpdateBrakePedal()
	{
		if ( (Input.Down( "Forward" ) ? 1f : 0f) + (Input.Down( "Backward" ) ? 1f : 0f) > 1.3f )
		{
			BrakePedal = 0;
			return;
		}
		if ( Math.Abs( -Math.Clamp( Gear, -1, 1 ) + (Input.Down( "Forward" ) ? 1 : 0) + (Input.Down( "Backward" ) ? -1 : 0) ) < 2 )
		{
			BrakePedal = 0;
			return;
		}
		var localVelocity = PhysMesh.Transform.World.PointToLocal( PhysMesh.WorldPosition + PhysMesh.PhysicsBody.Velocity );
		if ( localVelocity.x * Math.Clamp( Gear, -1, 1 ) <= 0 )
		{
			BrakePedal = 0;
			return;
		}
		if ( (Input.Down( "Forward" ) ? 1f : 0f) + (Input.Down( "Backward" ) ? -1f : 0f) > BrakePedal )
			BrakePedal = Math.Clamp( BrakePedal + Time.Delta / 0.3f, -1f, (Input.Down( "Forward" ) ? 1f : 0f) + (Input.Down( "Backward" ) ? -1f : 0f) );
		else if ( (Input.Down( "Forward" ) ? 1f : 0f) + (Input.Down( "Backward" ) ? -1f : 0f) < BrakePedal )
			BrakePedal = Math.Clamp( BrakePedal - Time.Delta / 0.2f, (Input.Down( "Forward" ) ? 1f : 0f) + (Input.Down( "Backward" ) ? -1f : 0f), 1f );
	}

	private void ApplyBraking( GameObject wheel )
	{
		var wheeldata = wheel.Components.Get<LandVehicleWheelData>();
		if ( !wheeldata.OnGround )
			return;
		var localVelocity = PhysMesh.Transform.World.PointToLocal( PhysMesh.WorldPosition + PhysMesh.PhysicsBody.Velocity );
		var attachment = AnimMesh.GetAttachment( wheeldata.ForceAttachment ).Value;
		PhysMesh.PhysicsBody.ApplyImpulseAt( attachment.Position, attachment.Forward.WithZ( 0f ).Normal * Math.Clamp( Math.Abs( localVelocity.x ), 0f, 500f * Time.Delta ) * PhysMesh.PhysicsBody.Mass * BrakePedal );
	}

	private void UpdateSkid( GameObject wheel )
	{
		var wheeldata = wheel.Components.Get<LandVehicleWheelData>();
		if ( !wheeldata.OnGround )
			return;
		//		i dont remember what any of this shit does lol
		var startpos = AnimMesh.GetAttachment( wheeldata.ForceAttachment ).Value.Position;
		var sidevector = AnimMesh.GetAttachment( wheeldata.ForceAttachment ).Value.Left.WithZ( 0 ).Normal;

		//		reset prev position if we werent on ground last frame
		if ( !wheeldata.WasOnGround )
			wheeldata.prevPosition = startpos;

		var velocity = (startpos - wheeldata.prevPosition) / Time.Delta;
		velocity = sidevector * velocity.Dot( sidevector );
		velocity = velocity.ClampLength( 800f );

		var prevvelocity = sidevector * wheeldata.prevVelocity;
		if ( (velocity - prevvelocity).Length > velocity.Length )
			velocity = (velocity + prevvelocity).Normal * velocity.Length;

		velocity -= 0.5f * (velocity - prevvelocity);
		wheeldata.prevVelocity = velocity.Dot( sidevector );

		//		align it partially to the cars side normal to avoid slowing the car as much on turns
		velocity = velocity.LerpTo( velocity.ProjectOnNormal( AnimMesh.WorldRotation.Left.WithZ( 0 ).Normal ), 0.5f );

		PhysMesh.PhysicsBody.ApplyImpulseAt( startpos + Vector3.Up * 15f, velocity * -40 );

		wheeldata.prevPosition = startpos;
		wheeldata.WasOnGround = true;
#if false
		Gizmo.Draw.IgnoreDepth = true;
		Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.Line(new Line(startpos + Vector3.Up * 15f, startpos + Vector3.Up * 20f + velocity));
#endif
	}

	private void UpdateAcclPedal()
	{
		if ( Math.Abs( -Math.Clamp( Gear, -1, 1 ) + (Input.Down( "Forward" ) ? 1 : 0) + (Input.Down( "Backward" ) ? -1 : 0) ) > 1 )
		{
			AcclPedal = 0;
			return;
		}
		if ( (Input.Down( "Forward" ) ? 1f : 0f) + (Input.Down( "Backward" ) ? -1f : 0f) > AcclPedal )
			AcclPedal = Math.Clamp( AcclPedal + Time.Delta, -1f, (Input.Down( "Forward" ) ? 1f : 0f) + (Input.Down( "Backward" ) ? -1f : 0f) );
		else if ( (Input.Down( "Forward" ) ? 1f : 0f) + (Input.Down( "Backward" ) ? -1f : 0f) < AcclPedal )
			AcclPedal = Math.Clamp( AcclPedal - Time.Delta / 0.2f, (Input.Down( "Forward" ) ? 1f : 0f) + (Input.Down( "Backward" ) ? -1f : 0f), 1f );
	}

	private void ApplyAccl( GameObject wheel )
	{
		var wheeldata = wheel.Components.Get<LandVehicleWheelData>();

		if ( !wheeldata.OnGround )
			return;

		var attachment = AnimMesh.GetAttachment( wheeldata.ForceAttachment ).Value;
		PhysMesh.PhysicsBody.ApplyImpulseAt( attachment.Position, attachment.Forward.WithZ( 0f ).Normal * 300f * PhysMesh.PhysicsBody.Mass * AcclPedal * Time.Delta );
	}
}
