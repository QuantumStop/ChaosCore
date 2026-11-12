namespace Core.AI;

using System;

public partial class AINavigation
{
    private Vector3[] _peerDetour;
    private int _peerDetourIndex;
    private Vector3? _peerDestination;
    private float _nextPeerDetourAt;
    private float _peerDetourExpires;
    public bool IsPassingNPC => _peerDetour is not null;

    private void UpdatePeerDetour()
    {
        if ( _peerDetour is null ) 
            return;

        var agent = Owner.Agent;
        
        if ( WorldTime.Now >= _peerDetourExpires )
            _peerDetourIndex = _peerDetour.Length;
        else if ( agent.AgentPosition.Distance( _peerDetour[_peerDetourIndex] ) <= Math.Max( 16f, agent.Radius * 0.75f ) )
            _peerDetourIndex++;
        else return;

        if ( _peerDetourIndex < _peerDetour.Length )
            agent.MoveTo( _peerDetour[_peerDetourIndex] );
        else
        {
            var destination = _peerDestination;
            ClearPeerDetour();
            if ( destination.HasValue ) 
                agent.MoveTo( destination.Value );
        }
    }

    private bool TryPassNPC( AIController blocker, Vector3 forward )
    {
        if ( IsPassingNPC || WorldTime.Now < _nextPeerDetourAt || Owner.Agent.TargetPosition is not Vector3 destination )
            return false;
        
        _nextPeerDetourAt = WorldTime.Now + 1f;
        var agent = Owner.Agent;
        var start = agent.AgentPosition;
        var side = Vector3.Up.Cross( forward ).Normal;
        float minimumClearance = agent.Radius + blocker.Agent.Radius + 24f;
        float preferred = (destination - start).Dot( side ) >= 0f ? 1f : -1f;
       
        for ( int candidate = 0; candidate < 6; candidate++ )
        {
            float clearance = minimumClearance * (1f + candidate / 2 * 0.5f);
            float sign = candidate % 2 == 0 ? preferred : -preferred;
            float blockerSide = (blocker.Agent.AgentPosition - start).Dot( side );
            var desiredSide = start + side * (blockerSide + clearance * sign);
            var desiredAhead = blocker.Agent.AgentPosition + forward * clearance + side * (clearance * sign);
            var beside = Owner.Scene.NavMesh.GetClosestPoint( desiredSide );
            var ahead = Owner.Scene.NavMesh.GetClosestPoint( desiredAhead );
           
            if ( !beside.HasValue || !ahead.HasValue || beside.Value.Distance( desiredSide ) > 8f
            || ahead.Value.Distance( desiredAhead ) > 8f || Math.Abs( ahead.Value.z - start.z ) > 8f ) 
                continue;
            
            if ( !PassingSegmentClear( start, beside.Value ) || !PassingSegmentClear( beside.Value, ahead.Value ) ) 
                continue;

            _peerDestination = destination;
            _peerDetour = [beside.Value, ahead.Value];
            _peerDetourIndex = 0;
            _peerDetourExpires = WorldTime.Now + 4f;
            agent.MoveTo( beside.Value );
            BlockingNPC = null;
            _peerWaiting = false;
            return true;
        }
        return false;
    }

    private bool PassingSegmentClear( Vector3 start, Vector3 end )
    {
        var agent = Owner.Agent;
        var bounds = new BBox( new Vector3( -agent.Radius, -agent.Radius, 4f ),
            new Vector3( agent.Radius, agent.Radius, agent.Height ) );
        
        var trace = Owner.Scene.Trace.Box( bounds, start, end )
            .IgnoreGameObjectHierarchy( Owner.GameObject )
            .WithoutTags( "trigger", "npc", "skinned_collider" )
            .Run();
        
        if ( trace.Hit || trace.StartedSolid ) 
            return false;

        var segment = (end - start).WithZ( 0 );
        
        foreach ( var other in Owner.Scene.GetAllComponents<AIController>() )
        {
            if ( other == Owner || !other.IsAlive || !other.Enabled || !other.Agent.IsValid() ) 
                continue;
            
            var offset = other.Agent.AgentPosition - start;

            if ( Math.Abs( offset.z ) > Math.Min( agent.Height, other.Agent.Height ) * 0.5f ) 
                continue;
            
            float t = Math.Clamp( offset.WithZ( 0 ).Dot( segment ) / Math.Max( 1f, segment.LengthSquared ), 0f, 1f );
           
            if ( (offset.WithZ( 0 ) - segment * t).Length < agent.Radius + other.Agent.Radius + 4f )
            {
                // Allow leaving an existing overlap, but never moving further into it.
                if ( t <= 0.01f && (end - other.Agent.AgentPosition).WithZ( 0 ).Length > offset.WithZ( 0 ).Length + 4f ) 
                    continue;
                
                return false;
            }
        }
        return true;
    }

    private void ClearPeerDetour()
    {
        _peerDetour = null;
        _peerDestination = null;
        _peerDetourIndex = 0;
    }
}
