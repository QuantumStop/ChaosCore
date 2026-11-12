namespace Core.AI;

using System;

/// <summary>
/// Motor for ground movement.
/// 
/// Plans speed and facing by looking ahead along the path.
/// Meanwhile NavMeshAgent follows the current path and handles avoidance and position.
/// 
/// Inspired by blended movement motor from Source.
/// </summary>
public sealed partial class AIGroundMotor : AIMotor
{
    internal const float MinSegmentLength = 1f;

    public override void Stop()
    {
        ResetLinkTraversal();
        base.Stop();
    }

    public Vector3? NextCorner { get; private set; }
    public float CornerSpeed { get; private set; }

    public static float CornerSpeedScale( Vector3 incoming, Vector3 outgoing )
        => Math.Clamp( incoming.WithZ( 0 ).Normal.Dot( outgoing.WithZ( 0 ).Normal ) + 0.2f, 0f, 1f );

    public static float CalculateCornerSpeed( Vector3 incoming, Vector3 outgoing, float cruise, float acceleration, float radius )
    {
        var a = incoming.WithZ( 0 ).Normal;
        var b = outgoing.WithZ( 0 ).Normal;

        float dot = Math.Clamp( a.Dot( b ), -1f, 1f );
        float halfTurn = MathF.Sqrt( (1f - dot ) * 0.5f );

        if ( halfTurn < 0.01f ) 
            return cruise;

        // Keep corner speed within the acceleration available for turning.
        float speed = MathF.Sqrt( acceleration * radius / halfTurn );
        return Math.Min( cruise, speed );
    }

    protected override float PrepareMovement()
    {
        NextCorner = null;
        CornerSpeed = 0f;
        return base.PrepareMovement();
    }

    protected override void TickMovement()
    {
        var npc = Owner;
        var agent = npc.Agent;
        float cruise = CruiseSpeed;
        float acceleration = EffectiveAcceleration;
        float deceleration = Math.Max( 1f, BrakingDeceleration ?? acceleration );

        var wish = agent.WishVelocity.WithZ( 0 );

        if ( wish.LengthSquared > 1f )
            FacingYaw = Rotation.LookAt( wish ).Yaw();

        if ( !agent.IsNavigating ) 
            return;

        var points = agent.GetPath().Points?.Select( p => p.Position ).ToArray();
       
        if ( points is not { Length: >= 2 } ) 
            return;

        float distance = 0f;
        var previous = agent.AgentPosition;
        var velocity = agent.Velocity.WithZ( 0 );

        for ( int i = 1; i < points.Length; i++ )
        {
            var point = points[i];
            var incoming = (point - previous).WithZ( 0 );
            distance += incoming.Length;

            if ( incoming.LengthSquared < MinSegmentLength * MinSegmentLength )
            {
                previous = point;
                continue;
            }

            bool isTerminal = i == points.Length - 1;
            float cornerSpeed = isTerminal && npc.CurrentAction is MoveToAction { StopAtDestination: false }
                ? cruise : 0f;

            if ( !isTerminal )
            {
                var outgoing = (points[i + 1] - point).WithZ( 0 );

                if ( outgoing.LengthSquared < MinSegmentLength * MinSegmentLength )
                {
                    previous = point;
                    continue;
                }

                cornerSpeed = CalculateCornerSpeed( incoming, outgoing, cruise, acceleration, agent.Radius );

                if ( !NextCorner.HasValue && cornerSpeed < cruise * 0.99f )
                {
                    NextCorner = point;
                    CornerSpeed = cornerSpeed;

                    float inYaw = Rotation.LookAt( incoming ).Yaw();
                    float turn = AngleDelta( Rotation.LookAt( outgoing ).Yaw(), inYaw );

                    // Start facing into the turn before reaching the corner.
                    float turnTime = Math.Abs( turn ) * 0.8f / TurnRate;
                    float anticipation = Math.Max( agent.Radius * 2f, velocity.Length * turnTime );
                    float blend = Math.Clamp( 1f - distance / anticipation, 0f, 1f );

                    if ( blend > 0f )
                        FacingYaw = inYaw + turn * 0.8f * blend;
                }
            }

            float brakingDistance = distance;

            // Allow for agent response time and begin braking before the corner.
            float buffer = Math.Max( agent.Radius * 0.5f, velocity.Length * 0.05f );
            if ( isTerminal )
            {
                float arrival = npc.Navigation.IsPassingNPC ? 8f : npc.CurrentAction is MoveToAction move
                    ? move.ArrivalDistance : agent.Radius;
                // Reach zero inside the completion region. Stopping at its outer edge
                // leaves the agent held in a running pose before the action completes.
                brakingDistance -= Math.Max( 0f, arrival - 4f );
                buffer = 0f;
            }
            brakingDistance = Math.Max( 0f, brakingDistance - buffer );

            // Every future corner imposes a constraint on current speed.
            float allowedSpeed = BrakingSpeed( cornerSpeed, deceleration, brakingDistance );
            SpeedLimit = Math.Min( SpeedLimit, allowedSpeed );

            previous = point;
        }

        // Reduce excess speed without overriding steering direction.
        if ( velocity.Length > SpeedLimit )
        {
            var corrected = velocity.Normal * SpeedLimit;
            agent.Velocity = corrected.WithZ( agent.Velocity.z );
        }
    }
}
