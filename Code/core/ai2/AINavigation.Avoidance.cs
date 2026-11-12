namespace Core.AI;

using System;

public partial class AINavigation
{
    public AIController BlockingNPC { get; private set; }

    public bool IsYielding => BlockingNPC.IsValid() && _peerWaiting;
    private bool _peerWaiting;
    private float _peerClearAt;

    private float UpdatePeerYield( float cruise )
    {
        var agent = Owner.Agent;
        bool wasWaiting = IsYielding;
        var previousBlocker = BlockingNPC;
        _peerWaiting = false;
        
        if ( !agent.IsNavigating || agent.IsTraversingLink )
        {
            BlockingNPC = null;
            return cruise;
        }

        var direction = wasWaiting ? Vector3.Zero : agent.WishVelocity.WithZ( 0 );
        
        if ( direction.Length < 5f )
            direction = (agent.GetLookAhead( 64f ) - agent.AgentPosition).WithZ( 0 );
        
        if ( direction.LengthSquared < 1f && agent.TargetPosition is Vector3 target )
            direction = (target - agent.AgentPosition).WithZ( 0 );
        
        if ( direction.LengthSquared < 1f )
        {
            BlockingNPC = null;
            return cruise;
        }
       
        direction = direction.Normal;
        AIController blocker = null;
        float nearest = float.MaxValue;
        float clearance = 0f;
        float braking = Math.Max( 1f, Motor.BrakingDeceleration ?? Motor.EffectiveAcceleration );
        float lookAhead = Math.Max( 32f, agent.Velocity.WithZ( 0 ).LengthSquared / (2f * braking) );
        
        foreach ( var other in Owner.Scene.GetAllComponents<AIController>() )
        {
            if ( other == Owner || !other.IsAlive || !other.Enabled || !other.Agent.IsValid()
            || other == Owner.Blackboard.activeEnemy || other.Agent.IsTraversingLink ) 
                continue;
            
            var delta = other.Agent.AgentPosition - agent.AgentPosition;

            if ( Math.Abs( delta.z ) > Math.Min( agent.Height, other.Agent.Height ) * 0.5f ) 
                continue;
            
            float along = delta.WithZ( 0 ).Dot( direction );
            float radius = agent.Radius + other.Agent.Radius + 2f;
            float lateral = (delta.WithZ( 0 ) - direction * along).Length;
          
            if ( along <= 0f || along > radius + lookAhead || lateral >= radius * 0.85f ) 
                continue;

            // Let one NPC pass first when both are approaching head on!
            var otherDirection = (other.Agent.GetLookAhead( 64f ) - other.Agent.AgentPosition).WithZ( 0 );
           
            if ( otherDirection.LengthSquared < 1f && other.Agent.TargetPosition is Vector3 otherTarget )
                otherDirection = (otherTarget - other.Agent.AgentPosition).WithZ( 0 );
          
            otherDirection = otherDirection.Normal;
            
            if ( other.Agent.IsNavigating && otherDirection.Dot( direction ) < -0.5f && Owner.Id.CompareTo( other.Id ) < 0 ) 
                continue;
          
            if ( other.Agent.Velocity.WithZ( 0 ).Dot( direction ) > 24f && along > radius ) 
                continue;

            if ( along < nearest )
            {
                nearest = along;
                blocker = other;
                clearance = along - MathF.Sqrt( radius * radius - lateral * lateral );
            }
        }

        if ( blocker is null )
        {
            if ( wasWaiting && previousBlocker.IsValid() && WorldTime.Now < _peerClearAt )
            {
                BlockingNPC = previousBlocker;
                _peerWaiting = true;
                return 0f;
            }
            BlockingNPC = null;
            
            return cruise;
        }

        if ( clearance > (wasWaiting && previousBlocker == blocker ? 9f : 3f) )
        {
            if ( wasWaiting && WorldTime.Now < _peerClearAt )
            {
                BlockingNPC = blocker;
                _peerWaiting = true;
                return 0f;
            }
            
            BlockingNPC = null;
            float frontSpeed = Math.Max( 0f, blocker.Agent.Velocity.WithZ( 0 ).Dot( direction ) );
            
            return Math.Min( cruise, AIMotor.BrakingSpeed( frontSpeed, braking, clearance - 3f ) );
        }

        if ( TryPassNPC( blocker, direction ) ) 
            return cruise;

        _peerWaiting = true;
        BlockingNPC = blocker;
        _peerClearAt = WorldTime.Now + 0.2f;
        
        return 0f;
    }
}
