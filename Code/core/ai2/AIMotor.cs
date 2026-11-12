namespace Core.AI;

using System;
using static Core.AI.AIController;

/// <summary>
/// Base class for motors that implement and fine-tune a specific locomotion style.
/// </summary>
public abstract class AIMotor : AIModule
{
	public float? RequestedSpeed { get; set; }
    public float? BrakingDeceleration { get; set; }
    public float SpeedLimit { get; protected set; }
    public float? FacingYaw { get; protected set; }
    public virtual float EffectiveAcceleration => Math.Max( 1f, Owner.Definition.AgentAccel )
        * Math.Max( 0f, Owner.BehaviorModule?.MotorAccelerationScale ?? 1f );
    public virtual float TurnRate => 150f;
    public virtual float CruiseSpeed => Math.Max( 0f, RequestedSpeed ?? Owner.Definition.AgentMaxSpeed );

    public LinkMoveType CurrentLinkMovement { get; protected set; }
    public float LinkProgress { get; protected set; }
    public Vector3 LinkVelocity { get; protected set; }
    public bool IsAligningLink { get; protected set; }
    public virtual bool IsGrabbingLedge => false;
    public virtual float LandingCompression => 0f;
    public virtual bool UseLinkTraversal => false;
    public virtual LinkMoveType SupportedTraversals => LinkMoveType.MOVE_NONE;
    public virtual float MaxJumpHeight => Owner.Definition.MaxJumpHeight;
    public virtual float ClimbHeightThreshold => Owner.Definition.ClimbHeightThreshold;
    public virtual float MaxClimbHeight => Owner.Definition.MaxClimbHeight;
    public virtual float MaxLinkDistance => Owner.Definition.MaxLinkDistance;
    public virtual float MaxDropHeight => Owner.Definition.MaxDropHeight;
    public override void Tick()
    {
        var agent = Owner.Agent;
       
        if ( !agent.IsValid() ) 
            return;
            
        agent.UpdateRotation = false;
        float cruise = PrepareMovement();
        
        if ( TickLinkTraversal() )
            return;

        float speed = Math.Min( cruise, Owner.Navigation?.MovementSpeedLimit ?? cruise );
        SpeedLimit = Math.Min( SpeedLimit, speed );
        
        if ( speed <= 0f ) 
            agent.Velocity = Vector3.Zero;
        
        ConfigureAgent( speed );
        TickMovement();
    }

    /// <summary>
    /// Handles link traversal before normal movement.
    /// </summary>
    protected virtual bool TickLinkTraversal() => false;
   
    /// <summary>
    /// Updates movement for this motor's locomotion style.
    /// </summary>
    protected virtual void TickMovement() { }
    public virtual bool CanTraverseLink( LinkMoveType type, Vector3 start, Vector3 end )
        => type != LinkMoveType.MOVE_NONE && (((int)type & ((int)type - 1)) == 0) && (SupportedTraversals & type) != 0;

    public virtual LinkMoveType GetLinkMovement( Vector3 start, Vector3 end, LinkMoveType allowed )
        => LinkMoveType.MOVE_NONE;

    /// <summary>
    /// Applies speed and acceleration to the agent.
    /// </summary>
    protected virtual void ConfigureAgent( float speed )
    {
        var agent = Owner.Agent;
        
        if ( Math.Abs( agent.Acceleration - EffectiveAcceleration ) > 0.01f )
            agent.Acceleration = EffectiveAcceleration;
       
        if ( Math.Abs( agent.MaxSpeed - speed ) > 0.01f )
            agent.MaxSpeed = speed;
    }

    protected virtual float PrepareMovement()
    {
        float cruise = CruiseSpeed;
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

    public virtual void MoveTo( Vector3 position ) => Owner.Agent.MoveTo( position );
    public virtual void Stop() => Owner.Agent.Stop();

    public static float AngleDelta( float target, float current ) => ((target - current) % 360f + 540f) % 360f - 180f;

    public static float ApproachYaw( float current, float target, float deltaTime, float turnRate = 150f )
        => current + Math.Clamp( AngleDelta( target, current ), -turnRate * deltaTime, turnRate * deltaTime );

    public static float BrakingSpeed( float endSpeed, float deceleration, float distance )
        => MathF.Sqrt( endSpeed * endSpeed + 2f * Math.Max( 0f, deceleration ) * Math.Max( 0f, distance ) );
}
