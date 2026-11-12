namespace Core.AI;

using System;
using static Core.AI.AIController;

public sealed partial class AIGroundMotor
{
    public override bool UseLinkTraversal => Owner.Definition.UseGroundLinkTraversal;
    public override LinkMoveType SupportedTraversals => LinkMoveType.MOVE_JUMP | LinkMoveType.MOVE_CLIMB | LinkMoveType.MOVE_DROP;
    
    public override bool IsGrabbingLedge => !IsAligningLink && CurrentLinkMovement == LinkMoveType.MOVE_CLIMB && LinkProgress < 0.55f
        && Owner.Agent.AgentPosition.Distance( _linkHanging ) <= 8f;
   
    public override float LandingCompression
    {
        get
        {
            float t = (WorldTime.Now - _landedAt) / 0.36f;
            
            if ( t <= 0f || t >= 1f ) 
                return 0f;

            // Deep landing dip, followed by a small settling bounce.
            float bounce = t < 0.7f ? MathF.Sin( t / 0.7f * MathF.PI )
                : MathF.Sin( (t - 0.7f) / 0.3f * MathF.PI ) * 0.2f;
           
            return bounce * _landingStrength;
        }
    }

    private Vector3 _linkStart;
    private Vector3 _linkEnd;
    private float _linkStartedAt;
    private float _linkDuration;
    private bool? _previousAutoTraverse;
    private bool _linkCompleteRequested;
    private Vector3 _linkHanging;
    private Vector3 _previousLinkPosition;
    private float _previousLinkTime;
    private float _landedAt = float.NegativeInfinity;
    private float _landingStrength;
    private bool _isLandingBounce;
    private float _bounceStartedAt;
    private float _bounceSpeed;
    private const float BounceGravity = 800f;

    /// <summary>
    /// Whether this motor can perform the traversal.
    /// </summary>
    public override bool CanTraverseLink( LinkMoveType type, Vector3 start, Vector3 end )
    {
        if ( !base.CanTraverseLink( type, start, end ) )
            return false;

        float height = end.z - start.z;
        float distance = (end - start).WithZ( 0 ).Length;
      
        if ( distance > Math.Max( 0f, MaxLinkDistance )
            || -height > Math.Max( 0f, MaxDropHeight ) )
            return false;

        return type switch
        {
            LinkMoveType.MOVE_JUMP => height <= Math.Max( 0f, MaxJumpHeight ),
            LinkMoveType.MOVE_CLIMB => height > 1f && MaxClimbHeight > 0f && height <= MaxClimbHeight,
            LinkMoveType.MOVE_DROP => height < -1f,
            _ => false
        };
    }

    public override LinkMoveType GetLinkMovement( Vector3 start, Vector3 end, LinkMoveType allowed )
    {
        float height = end.z - start.z;
        var preferred = height < -1f ? LinkMoveType.MOVE_DROP
            : height > ClimbHeightThreshold ? LinkMoveType.MOVE_CLIMB : LinkMoveType.MOVE_JUMP;
        var fallback = preferred == LinkMoveType.MOVE_JUMP ? LinkMoveType.MOVE_CLIMB : LinkMoveType.MOVE_JUMP;

        if ( (allowed & preferred) != 0 && CanTraverseLink( preferred, start, end ) )
            return preferred;

        return (allowed & fallback) != 0 && CanTraverseLink( fallback, start, end )
            ? fallback : LinkMoveType.MOVE_NONE;
    }

    protected override bool TickLinkTraversal()
    {
        var agent = Owner.Agent;
       
        if ( !UseLinkTraversal )
        {
            if ( _previousAutoTraverse.HasValue )
            {
                if ( agent.IsTraversingLink && CurrentLinkMovement != LinkMoveType.MOVE_NONE )
                    return TickActiveLink();
                
                ResetLinkTraversal();
                
                agent.AutoTraverseLinks = _previousAutoTraverse.Value;
                _previousAutoTraverse = null;
            }
            
            return false;
        }

        _previousAutoTraverse ??= agent.AutoTraverseLinks;
        agent.AutoTraverseLinks = false;
       
        if ( agent.CurrentLinkTraversal is not NavMeshAgent.LinkTraversalData link )
        {
            ResetLinkTraversal();
            return false;
        }

        if ( CurrentLinkMovement == LinkMoveType.MOVE_NONE )
        {
            _linkStart = link.AgentInitialPosition;
            _linkEnd = link.LinkExitPosition;
            CurrentLinkMovement = Owner.Navigation.GetLinkMovement( link.LinkEnterPosition, _linkEnd, link.LinkComponent );
            
            if ( CurrentLinkMovement != LinkMoveType.MOVE_NONE && !TryFindLinkLanding( _linkStart, ref _linkEnd ) )
                CurrentLinkMovement = LinkMoveType.MOVE_NONE;
           
            if ( CurrentLinkMovement == LinkMoveType.MOVE_NONE )
            {
                // Reject the link on this side instead of completing it at it's exit.
                agent.Stop();
                agent.SetAgentPosition( _linkStart );
                Owner.Navigation.ReportFailure();
                return true;
            }

            _linkStartedAt = WorldTime.Now;
            _previousLinkTime = _linkStartedAt;
            _previousLinkPosition = _linkStart;
            LinkVelocity = Vector3.Zero;
            IsAligningLink = true;
            
            _linkDuration = CurrentLinkMovement == LinkMoveType.MOVE_CLIMB ? 1.2f
                : Math.Max( 0.45f, _linkStart.Distance( _linkEnd ) / Math.Max( 1f, agent.MaxSpeed ) );
           
            if ( CurrentLinkMovement == LinkMoveType.MOVE_DROP )
            {
                float shallow = Math.Clamp( 1f - (_linkStart.z - _linkEnd.z) / 128f, 0f, 1f );
                _linkDuration *= 1f - shallow * 0.2f;
            }

            var direction = (_linkEnd - _linkStart).WithZ( 0 ).Normal;
            var edgeStart = _linkStart.WithZ( _linkEnd.z - 8f );
            var edge = Owner.Scene.Trace.Ray( edgeStart, _linkEnd.WithZ( edgeStart.z ) )
                .IgnoreGameObjectHierarchy( Owner.GameObject ).WithoutTags( "trigger" ).Run();
            
            var ledge = edge.Hit ? edge.HitPosition : _linkEnd;
           
            _linkHanging = (ledge - direction * (agent.Radius + 2f)).WithZ( Math.Max( _linkStart.z, _linkEnd.z - agent.Height * 0.7f ) );
            float approach = (_linkHanging - _linkStart).Dot( direction );
            if ( approach < 0f )
                _linkHanging -= direction * approach;
        }

        return TickActiveLink();
    }

    private bool TickActiveLink()
    {
        var agent = Owner.Agent;
        var forward = (_linkEnd - _linkStart).WithZ( 0 ).Normal;
        if ( forward.LengthSquared > 0.01f )
            FacingYaw = Rotation.LookAt( forward ).Yaw();

        // Turn on the ground before starting the crossing.
        if ( IsAligningLink )
        {
            _linkStartedAt = WorldTime.Now;
            _previousLinkTime = WorldTime.Now;
            if ( FacingYaw is float yaw && Math.Abs( AngleDelta( yaw, Owner.WorldRotation.Yaw() ) ) > 5f )
                return true;
            IsAligningLink = false;
        }
        
        if ( _linkCompleteRequested ) 
            return true;
       
        if ( _isLandingBounce ) 
            return TickLandingBounce();

        LinkProgress = Math.Clamp( (WorldTime.Now - _linkStartedAt) / _linkDuration, 0f, 1f );

        Vector3 position;
       
        if ( CurrentLinkMovement == LinkMoveType.MOVE_CLIMB )
        {
            var hanging = _linkHanging;
           
            float phase = LinkProgress < 0.45f ? LinkProgress / 0.45f
                : LinkProgress < 0.55f ? 0f
                : LinkProgress < 0.85f ? (LinkProgress - 0.55f) / 0.3f : (LinkProgress - 0.85f) / 0.15f;
            phase = phase * phase * (3f - 2f * phase);
            
            var aboveLedge = hanging.WithZ( _linkEnd.z );
            position = LinkProgress < 0.45f ? Vector3.Lerp( _linkStart, hanging, phase )
                : LinkProgress < 0.85f ? Vector3.Lerp( hanging, aboveLedge, phase )
                : Vector3.Lerp( aboveLedge, _linkEnd, phase );
        }
        else
        {
            float smooth = LinkProgress * LinkProgress * (3f - 2f * LinkProgress);
            float travel = MathX.Lerp( LinkProgress, smooth, 0.2f );
            position = Vector3.Lerp( _linkStart, _linkEnd, travel );
            
            if ( CurrentLinkMovement == LinkMoveType.MOVE_DROP )
            {
                float shallow = Math.Clamp( 1f - (_linkStart.z - _linkEnd.z) / 128f, 0f, 1f );
                float fall = LinkProgress * LinkProgress + shallow * 0.25f * LinkProgress * (1f - LinkProgress);
                position.z = MathX.Lerp( _linkStart.z, _linkEnd.z, fall );
            }
            else
            {
                // Give upward jumps enough arc to start descending before landing.
                float arc = 24f + Math.Max( 0f, _linkEnd.z - _linkStart.z ) * 0.5f;
                position.z = MathX.Lerp( _linkStart.z, _linkEnd.z, LinkProgress )
                    + 4f * arc * LinkProgress * (1f - LinkProgress);
            }
        }

        float delta = WorldTime.Now - _previousLinkTime;
        
        if ( delta > 0f )
            LinkVelocity = (position - _previousLinkPosition) / delta;

        _previousLinkPosition = position;
        _previousLinkTime = WorldTime.Now;
        agent.SetAgentPosition( position );
        
        if ( LinkProgress >= 1f )
        {
            if ( CurrentLinkMovement is LinkMoveType.MOVE_JUMP or LinkMoveType.MOVE_DROP )
            {
                _landedAt = WorldTime.Now;
                _landingStrength = Math.Clamp( Math.Abs( LinkVelocity.z ) / 1000f, 0f, 0.32f );
                float height = Math.Clamp( Math.Abs( LinkVelocity.z ) * 0.01f, 0f, 4f );
                
                var bounds = new BBox( new Vector3( -agent.Radius, -agent.Radius, 4f ),
                    new Vector3( agent.Radius, agent.Radius, agent.Height ) );
                
                var ceiling = Owner.Scene.Trace.Box( bounds, _linkEnd, _linkEnd + Vector3.Up * height )
                    .IgnoreGameObjectHierarchy( Owner.GameObject )
                    .WithoutTags( "trigger" )
                    .Run();
               
                height = ceiling.StartedSolid ? 0f : Math.Max( 0f, height * ceiling.Fraction - 0.25f );
            
               if ( height > 0.1f )
                {
                    _bounceSpeed = MathF.Sqrt( 2f * BounceGravity * height );
                    _bounceStartedAt = WorldTime.Now;
                    _isLandingBounce = true;
                    return true;
                }
            }
            
            agent.CompleteLinkTraversal();
            _linkCompleteRequested = true;
        }
        return true;
    }

    private bool TickLandingBounce()
    {
        float time = Math.Max( 0f, WorldTime.Now - _bounceStartedAt );
        float height = _bounceSpeed * time - 0.5f * BounceGravity * time * time;
        LinkVelocity = Vector3.Up * (_bounceSpeed - BounceGravity * time);
        
        Owner.Agent.SetAgentPosition( _linkEnd + Vector3.Up * Math.Max( 0f, height ) );
        
        if ( time >= 2f * _bounceSpeed / BounceGravity )
        {
            LinkVelocity = Vector3.Zero;
            Owner.Agent.CompleteLinkTraversal();
            _linkCompleteRequested = true;
            _isLandingBounce = false;
        }

        return true;
    }

    private void ResetLinkTraversal()
    {
        CurrentLinkMovement = LinkMoveType.MOVE_NONE;
        LinkProgress = 0f;
        LinkVelocity = Vector3.Zero;
        IsAligningLink = false;
        _linkCompleteRequested = false;
        _isLandingBounce = false;
    }

    private bool TryFindLinkLanding( Vector3 start, ref Vector3 end )
    {
        var agent = Owner.Agent;
        var bounds = new BBox( new Vector3( -agent.Radius, -agent.Radius, 4f ),
            new Vector3( agent.Radius, agent.Radius, agent.Height ) );
        var forward = (end - start).WithZ( 0 ).Normal;
       
        for ( float offset = 0f; offset <= agent.Radius * 2f; offset += 8f )
        {
            var desired = end + forward * offset;
            var candidate = Owner.Scene.NavMesh.GetClosestPoint( desired );
           
            if ( !candidate.HasValue || candidate.Value.Distance( desired ) > 8f
                || Math.Abs( candidate.Value.z - end.z ) > 4f )
                continue;

            var clearance = Owner.Scene.Trace.Box( bounds, candidate.Value, candidate.Value + Vector3.Up )
                .IgnoreGameObjectHierarchy( Owner.GameObject )
                .WithoutTags( "trigger" )
                .Run();
           
            if ( clearance.StartedSolid || clearance.Hit ) 
                continue;

            end = candidate.Value;
            return true;
        }
        return false;
    }

    public override void Terminate()
    {
        base.Terminate();
        
        if ( Owner.Agent.IsValid() && _previousAutoTraverse.HasValue )
            Owner.Agent.AutoTraverseLinks = _previousAutoTraverse.Value;
        
        _previousAutoTraverse = null;
    }
}
