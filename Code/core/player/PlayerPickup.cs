using Sandbox.Physics;
using Sandbox.Diagnostics;

namespace Core;

[Title("Player Pickup")]
[Icon("Backpack")]
[Category("Core")]
public class PlayerPickup : BaseEntity
{
    [Property] public BasePlayer Owner { get; private set; }

    [Property, ReadOnly] public GameObject HeldProp { get; private set; }
    [Property, ReadOnly] public Rigidbody PropPhys { get; private set; }
    [Property, ReadOnly] public Angles PropRelativeRot { get; private set; }

    private PhysicsJoint Joint;

	/// <summary>
	/// This is used to know whether we succeeded in pickup
	/// </summary>
    private bool UseSuccess { get; set; }

    private Vector3 predictedPosition;
    private Rotation predictedRotation;

    private Vector3 targetPosition;
    private Rotation targetRotation;

    [ConVar("debug_nomass", ConVarFlags.Cheat)]
    public static bool DebugNoMass { get; set; }

    protected override void OnStart()
    {
        base.OnStart();
        if (Owner == null)
            Owner = Components.Get<BasePlayer>();
    }

    private void TryPickup(GameObject obj)
    {
        if (Owner == null || Owner.LifeState != LifeState.Alive)
            return;

        if (!obj.Components.TryGet<Rigidbody>(out var rigidbody))
            return;

        if (!DebugNoMass && rigidbody.Mass > 35)
            return;

        HeldProp = obj;
        PropPhys = rigidbody;
        PropRelativeRot = HeldProp.WorldRotation.Angles() - Owner.Controller.EyeAngles.WithPitch(0);
        HeldProp.Tags.Add("HELD_PROP");

        Owner.CurrentWeapon?.Holster();

        if (Owner.Controller.Controller.PhysicsBodyRigidbody?.PhysicsBody != null)
        {
            var point1 = new PhysicsPoint(PropPhys.PhysicsBody);
            var point2 = new PhysicsPoint(Owner.Controller.Controller.PhysicsBodyRigidbody.PhysicsBody);
            Joint = PhysicsJoint.CreateSpring(point1, point2, 0, 99999);
            Joint.Collisions = false;
        }

        UseSuccess = true;

        predictedPosition = HeldProp.WorldPosition;
        predictedRotation = HeldProp.WorldRotation;
        targetPosition = predictedPosition;
        targetRotation = predictedRotation;

        // Broadcast pickup for client visuals
        OnPickupConfirmed(HeldProp);
		
        if ( Networking.IsActive )
			OnPickupConfirmedRpc( HeldProp );
    }

    public void PickUpObject(GameObject obj)
    {
        if (Networking.IsHost || !Networking.IsActive)
        {
            TryPickup(obj);
        }
        else
        {
            PredictPickup(obj);
            RequestPickupRpc(obj, Owner);
        }
    }

    private void PredictPickup(GameObject obj)
    {
        HeldProp = obj;
        PropPhys = obj.Components.Get<Rigidbody>();

        predictedPosition = obj.WorldPosition;
        predictedRotation = obj.WorldRotation;

        targetPosition = predictedPosition;
        targetRotation = predictedRotation;

        Sound.Play("usesuccess").ListenLocal = true;
    }

    [Rpc.Broadcast] private void OnPickupConfirmedRpc(GameObject obj) => OnPickupConfirmed(obj);

    private void OnPickupConfirmed(GameObject obj)
    {
        HeldProp = obj;
        PropPhys = obj.Components.Get<Rigidbody>();

        targetPosition = obj.WorldPosition;
        targetRotation = obj.WorldRotation;
    }

    public void DropObject(bool punt = false)
    {
        if (HeldProp == null || PropPhys == null || Owner == null)
            return;

        if (Networking.IsHost || !Networking.IsActive)
        {
            // Apply real physics
            PropPhys.PhysicsBody.Velocity += Owner.Controller.Controller.Velocity;
            PropPhys.PhysicsBody.Velocity = PropPhys.PhysicsBody.Velocity.ClampLength(350f);

            if (punt)
                PropPhys.PhysicsBody.Velocity += Owner.Controller.EyeAngles.Forward * 400f;

            if (HeldProp.Components.TryGet<BaseUsable>(out var usable))
                usable.OnDropped?.Invoke(Owner);

            PropPhys.PhysicsBody.AngularVelocity *= 0.3f;

            // Update target for clients
            targetPosition = PropPhys.PhysicsBody.Position;
            targetRotation = PropPhys.PhysicsBody.Rotation;
        }
        else
        {
            // Client predicts drop visually
            predictedPosition += Owner.Controller.EyeAngles.Forward * (punt ? 400f * Time.Delta : 0f);
        }

        HeldProp.Tags.Remove("HELD_PROP");
        HeldProp = null;
        PropPhys = null;

        if (Joint.IsValid())
            Joint.Remove();

        Owner.CurrentWeapon?.Draw();
    }

    [Rpc.Host] private void RequestDropRpc(bool punt) { DropObject(punt); OnDropConfirmedRpc(); }
    [Rpc.Broadcast] private void OnDropConfirmedRpc() => HeldProp = null;
    [Rpc.Host] private void RequestPickupRpc(GameObject obj, BasePlayer requestingPlayer) { Owner = requestingPlayer; TryPickup(obj); }


    protected override void OnFixedUpdate()
    {
        if (Owner == null)
            return;

        UpdatePickup();

        if (HeldProp == null || PropPhys == null)
            return;

        // Drop if dead or standing on held prop
        if (Owner.LifeState != LifeState.Alive || Owner.Controller.Controller.GroundObject == HeldProp)
        {
            DropObject();
            return;
        }

        // Drop if too far
        if (Vector3.DistanceBetween(PropPhys.PhysicsBody.MassCenter, Owner.Controller.Head.WorldPosition) > 128f)
        {
            DropObject();
            return;
        }

        if (!Networking.IsHost)
        {
            // Client-side prediction: interpolate to target positions
            predictedPosition = Vector3.Lerp(predictedPosition, targetPosition, 0.2f);
            predictedRotation = Rotation.Slerp(predictedRotation, targetRotation, 0.2f);

            PropPhys.PhysicsBody.Position = predictedPosition;
            PropPhys.PhysicsBody.Rotation = predictedRotation;
        }
        else
        {
            // Host authoritative movement
            var wantedRotation = (PropRelativeRot + Owner.Controller.EyeAngles.WithPitch(0)).ToRotation();
            var wantedPosition = Owner.Controller.Head.WorldPosition + Owner.Controller.EyeAngles.Forward * 80f;
			
            wantedPosition += HeldProp.WorldPosition - PropPhys.PhysicsBody.MassCenter;

            var vel = PropPhys.PhysicsBody.Velocity;
            var angvel = PropPhys.PhysicsBody.AngularVelocity;

            Vector3.SmoothDamp(PropPhys.PhysicsBody.Position, wantedPosition, ref vel, 0.05f, Time.Delta);
            Rotation.SmoothDamp(PropPhys.PhysicsBody.Rotation, wantedRotation, ref angvel, 0.05f, Time.Delta);

            vel = vel.ClampLength(1250f);

            PropPhys.PhysicsBody.Velocity = vel;
            PropPhys.PhysicsBody.AngularVelocity = angvel;

            // Update target positions for clients
            targetPosition = PropPhys.PhysicsBody.Position;
            targetRotation = PropPhys.PhysicsBody.Rotation;
        }

        // Drop / punt input
        if (Input.Pressed("attack1"))
        {
            if (Networking.IsActive)
                RequestDropRpc(true);
            else
                DropObject(true);
        }
    }

    protected override void OnUpdate()
    {
        if (HeldProp == null)
            return;

        base.OnUpdate();

        if (HeldProp.Components.TryGet<BaseUsable>(out var usable))
            usable.OnHoldUpdate?.Invoke(Owner);
    }

    public void UpdatePickup()
    {
        if (Owner == null || !Input.Pressed("use"))
            return;

        if (HeldProp != null && HeldProp.IsValid())
        {
            if (Networking.IsHost || !Networking.IsActive)
                DropObject();
            else
                RequestDropRpc(false);
            return;
        }

        var tr = Scene.Trace.Ray(Owner.Controller?.AimRay ?? default, 100f)
            .IgnoreGameObjectHierarchy(this.GameObject)
            .WithoutTags("trigger")
            .HitTriggers()
            .Run();

        if (tr.Hit && tr.GameObject.IsValid())
        {
            PickUpObject(tr.GameObject);
        }
        else
        {
            Sound.Play("usedeny").ListenLocal = true;
        }
    }
}
