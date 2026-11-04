namespace Core.AI;

using System;

/// <summary>
/// Base class for motors that implement and fine-tune a specific locomotion style.
/// </summary>
public abstract partial class AIMotor : AIModule
{
	public float? RequestedSpeed { get; set; }
    public float? BrakingDeceleration { get; set; }
    public float SpeedLimit { get; protected set; }
    public float? FacingYaw { get; protected set; }
    public float EffectiveAcceleration { get; protected set; }
    public virtual float TurnRate => 150f;

    protected float PrepareMovement()
    {
        var cruise = Math.Max( 0f, RequestedSpeed ?? Owner.Definition.AgentMaxSpeed );
        var accelerationScale = Math.Max( 0f, Owner.BehaviorModule?.MotorAccelerationScale ?? 1f );
     
        EffectiveAcceleration = Math.Max( 1f, Owner.Definition.AgentAccel ) * accelerationScale;
        SpeedLimit = cruise;
        FacingYaw = null;
       
        return cruise;
    }

    public override void Terminate()
    {
        if ( Owner.Agent.IsValid() )
            Stop();

        FacingYaw = null;
        SpeedLimit = 0f;
        base.Terminate();
    }

    public abstract void MoveTo( Vector3 position );
    public abstract void Stop();

    public static float AngleDelta( float target, float current ) => ((target - current) % 360f + 540f) % 360f - 180f;

    public static float ApproachYaw( float current, float target, float deltaTime, float turnRate = 150f )
        => current + Math.Clamp( AngleDelta( target, current ), -turnRate * deltaTime, turnRate * deltaTime );

    public static float BrakingSpeed( float endSpeed, float deceleration, float distance )
        => MathF.Sqrt( endSpeed * endSpeed + 2f * Math.Max( 0f, deceleration ) * Math.Max( 0f, distance ) );
}
