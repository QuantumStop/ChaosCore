namespace Core.AI;

using System;
using static Core.AI.AIController;

public partial class AINavigation
{
    public float MovementSpeedLimit { get; private set; } = float.PositiveInfinity;
    public Vector3? Destination => _nextLinkTarget ?? _peerDestination ?? Owner.Agent.TargetPosition;

    private Vector3? _nextLinkTarget;
    private bool _stopAfterLink;

    public override void Tick()
    {
        if ( Motor is null || !Owner.Agent.IsValid() ) return;

        ApplyLinkRequest();
        MovementSpeedLimit = Motor.CruiseSpeed;
        if ( !Owner.Agent.IsTraversingLink && ControllerMoveType is MoveType.MOVE_GROUND or MoveType.MOVE_CRAWL )
        {
            UpdatePeerDetour();
            MovementSpeedLimit = UpdatePeerYield( MovementSpeedLimit );
        }
        else
        {
            BlockingNPC = null;
            _peerWaiting = false;
        }
        Motor.Tick();
    }

    /// <summary>
    /// Moves toward a destination while preserving temporary routes and active traversals.
    /// </summary>
    public void MoveTo( Vector3 position )
    {
        if ( Motor is null ) 
            return;

        if ( Motor.UseLinkTraversal && Owner.Agent.IsTraversingLink )
        {
            _nextLinkTarget = position;
            _stopAfterLink = false;
            return;
        }

        if ( IsPassingNPC )
        {
            _peerDestination = position;
            return;
        }

        Motor.MoveTo( position );
    }

    /// <summary>
    /// Stops navigation after any active traversal finishes.
    /// </summary>
    public void Stop()
    {
        ClearPeerDetour();
        BlockingNPC = null;
        _peerWaiting = false;
      
        if ( Motor?.UseLinkTraversal == true && Owner.Agent.IsTraversingLink )
        {
            _nextLinkTarget = null;
            _stopAfterLink = true;
            return;
        }

        _nextLinkTarget = null;
        _stopAfterLink = false;
        Motor?.Stop();
    }

    private void ApplyLinkRequest()
    {
        if ( Owner.Agent.IsTraversingLink ) 
            return;

        var target = _nextLinkTarget;
        bool stop = _stopAfterLink;
        _nextLinkTarget = null;
        _stopAfterLink = false;

        if ( stop ) 
            Stop();
        else if ( target.HasValue ) 
            MoveTo( target.Value );
    }

    private void ClearLocalMovement()
    {
        ClearPeerDetour();
        BlockingNPC = null;
        _peerWaiting = false;
        _nextLinkTarget = null;
        _stopAfterLink = false;
        MovementSpeedLimit = float.PositiveInfinity;
    }

    /// <summary>
    /// Chooses a permitted traversal that the motor can perform.
    /// </summary>
    public LinkMoveType GetLinkMovement( Vector3 start, Vector3 end, NavMeshLink link = null )
    {
        if ( Motor is null ) 
            return LinkMoveType.MOVE_NONE;

        var allowed = Owner.Definition.AllowedLinkTraversals & Motor.SupportedTraversals;
      
        if ( link is NavMeshTraverseLink traversal )
            allowed &= traversal.GetAllowedTraversals( start, end );

        return Motor.GetLinkMovement( start, end, allowed );
    }
}
