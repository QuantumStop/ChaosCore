using System;

namespace Core.AI;

public abstract partial class AIMotor
{
    [ConVar( "ai_debug_navigation", Help = "Show navigation debug for NPCs that have Debug Navigation enabled. 0 = off, 1 = basic, 2 = detailed." )]
    public static int DebugLevel { get; set; }

    private static readonly Color _ringColor = Color.Parse( "#146BA3" ).Value;
    private static readonly Color _routeColor = Color.Parse( "#6682C7" ).Value;
    private static readonly Color _trailColor = Color.Parse( "#576682" ).Value;
    private static readonly Color _facingColor = Color.Parse( "#0D9E75" ).Value;
    private static readonly Color _targetColor = Color.Parse( "#7a932f" ).Value;

    private readonly Queue<Vector3> _trail = new();
    private Vector3[] _points = [];
    private Vector3[] _trailPoints = [];

    private Vector3? _destination;
    private float _nextSample;
    private string _pathStatus = "None";
    private bool _validPath;

    private bool _wasEnabled;

    public override void DrawDebug()
    {
        if ( DebugLevel <= 0 )
        {
            if ( _wasEnabled )
            {
                _trail.Clear();
                _points = [];
                _trailPoints = [];
                _destination = null;
                _pathStatus = "None";
                _validPath = false;
            }
            _wasEnabled = false;
            return;
        }

        var agent = Owner.Agent;

        if ( !_wasEnabled )
            _nextSample = 0;

        _wasEnabled = true;

        // We only refresh the path and it's trail every 0.2s
        if ( WorldTime.Now >= _nextSample )
        {
            _nextSample = WorldTime.Now + 0.2f;

            var path = agent.GetPath();
            _points = path.Points?.Select( p => p.Position ).ToArray() ?? [];
            _validPath = path.IsValid;
            _pathStatus = agent.TargetPosition.HasValue ? path.Status.ToString() : "None";

            // Keep the last destination when our agent stops
            if ( agent.TargetPosition is Vector3 destination )
                _destination = destination;

            if ( DebugLevel >= 2 )
            {
                _trail.Enqueue( Owner.WorldPosition );

                while ( _trail.Count > 40 )
                    _trail.Dequeue();

                _trailPoints = [.. _trail];
            }
        }

        using ( Gizmo.Scope( "AI navigation", new Transform( Vector3.Zero ) ) )
        {
            Gizmo.Draw.IgnoreDepth = true;
            Gizmo.Draw.LineThickness = 2;

            DrawGroundOverlay( Owner );

            if ( DebugLevel >= 2 )
            {
                DrawPath( _trailPoints, isTrail: true );

                if ( agent.IsNavigating )
                    DrawPath( _points );
            }
        }
    }

    private void DrawGroundOverlay( AIController npc )
    {
        var agent = npc.Agent;
        var center = npc.WorldPosition + Vector3.Up * 2f;
        float radius = Math.Max( 24f, agent.Radius * 1.5f );

        Gizmo.Draw.Color = _ringColor;
        DrawPositionRing( center, radius, 4f );

        var facing = npc.WorldRotation.Forward.WithZ( 0 ).Normal;
        var origin = center + Vector3.Up * 0.4f;
        var side = Vector3.Cross( facing, Vector3.Up ) * 4f;
        var shoulder = origin + facing * radius * 0.95f * 0.78f;
        var tip = origin + facing * radius * 0.95f;

        Gizmo.Draw.Color = _facingColor;

        Gizmo.Draw.SolidTriangle( origin - side, shoulder - side, shoulder + side );
        Gizmo.Draw.SolidTriangle( origin - side, shoulder + side, origin + side );
        Gizmo.Draw.SolidTriangle( shoulder - side, tip, shoulder + side );

        if ( agent.IsNavigating && FacingYaw is float yaw )
            DrawTurn( npc, center, radius, yaw );

        if ( _destination is Vector3 destination )
        {
            Gizmo.Draw.Color = _targetColor;
            DrawGoalMarker( destination + Vector3.Up * 2f );
        }
    }

    private static void DrawTurn( AIController npc, Vector3 center, float radius, float yaw )
    {
        float startYaw = npc.WorldRotation.Yaw();
        float delta = AngleDelta( yaw, startYaw );
        Vector3 direction = Rotation.FromYaw( yaw ).Forward;
        float turnRadius = radius * 0.76f;
        Vector3 origin = center + Vector3.Up * 0.6f;

        Gizmo.Draw.Color = _targetColor;

        var marker = origin + direction * turnRadius;

        for ( int i = 0; i < 20; i++ )
        {
            Gizmo.Draw.SolidTriangle( marker, marker + Rotation.FromYaw( i * 18f ).Forward * 3f,
                marker + Rotation.FromYaw( (i + 1) * 18f ).Forward * 3f );
        }

        if ( Math.Abs( delta ) <= 3f ) return;

        int steps = Math.Max( 2, (int)(Math.Abs( delta ) / 5f) );

        for ( int i = 0; i < steps; i++ )
        {
            var a = origin + Rotation.FromYaw( startYaw + delta * i / steps ).Forward * turnRadius;
            var b = origin + Rotation.FromYaw( startYaw + delta * (i + 1) / steps ).Forward * turnRadius;
            DrawRotationStrip( a, b, 1.5f );
        }

        var tip = origin + direction * turnRadius;
        var tangent = Vector3.Cross( Vector3.Up, direction ) * Math.Sign( delta );      // Point the arrow along the arc, in the direction of where Motor is turning us.
        var neck = tip - tangent * 3.2f;

        DrawRotationStrip( tip - tangent * 8f, neck, 2f );

        var side = Vector3.Cross( tangent, Vector3.Up ) * 5f;
        Gizmo.Draw.SolidTriangle( neck - side, tip, neck + side );
    }

    private void DrawPath( IReadOnlyList<Vector3> points, bool isTrail = false )
    {
        var color = isTrail ? _trailColor : _validPath ? _routeColor : Color.Red;
        var lineColor = color.WithAlpha( isTrail ? 0.25f : 0.65f );
        var pointColor = isTrail ? color.WithAlpha( 0.55f ) : color;

        Vector3? previous = null;

        for ( int pointIndex = 0; pointIndex < points.Count; pointIndex++ )
        {
            var p = points[pointIndex] + Vector3.Up * 1.5f;
            bool isLastPoint = pointIndex == points.Count - 1;

            if ( previous.HasValue )
            {
                var start = previous.Value;
                var distance = start.Distance( p );
                var steps = Math.Max( 1, (int)MathF.Ceiling( distance / 64f ) );

                for ( int i = 1; i <= steps; i++ )
                {
                    var a = Vector3.Lerp( start, p, (i - 1f) / steps );
                    var b = Vector3.Lerp( start, p, (float)i / steps );

                    Gizmo.Draw.Color = lineColor;

                    var side = Vector3.Cross( (b - a).WithZ( 0 ).Normal, Vector3.Up ) * 5f;

                    Gizmo.Draw.SolidTriangle( a - side, b, a + side );

                    // We dont want the actual goal as a point, we show a marker there anyways
                    if ( isLastPoint && i == steps )
                        continue;

                    Gizmo.Draw.Color = pointColor;
                    Gizmo.Draw.SolidCylinder( b, b + Vector3.Up * 1.2f, 5f, 24 );
                }
            }
            else if ( !isLastPoint )
            {
                Gizmo.Draw.Color = pointColor;
                Gizmo.Draw.SolidCylinder( p, p + Vector3.Up * 1.2f, 5f, 24 );
            }

            previous = p;
        }
    }

    public DebugTextBlock GetLocomotionDebugText()
    {
        var block = new DebugTextBlock();
        var npc = Owner;

        if ( DebugLevel < 2 )
            return null;

        var agent = npc.Agent;
        var target = agent.TargetPosition;
        float length = 0f;

        for ( int i = 1; i < _points.Length; i++ )
        {
            length += _points[i - 1].Distance( _points[i] );
        }

        float arrival = npc.CurrentAction is MoveToAction move ? move.ArrivalDistance : agent.Radius;

        block.Line( "NPC", npc.GameObject.Name, Color.White );
        block.Line( "Motor", GetType().Name, Color.White );
        block.Line( "Status", GetMovementStatus( npc, arrival ), Color.White );
        block.Line( "Path result", _pathStatus, Color.Orange );
        block.Line( "Speed / Wish", $"{agent.Velocity.Length:0} / {agent.WishVelocity.Length:0}", Color.Green );
        block.Line( "Cruise / Cap", $"{RequestedSpeed ?? npc.Definition.AgentMaxSpeed:0} / {SpeedLimit:0}", Color.Green );
        block.Line( "Accel / Brake", $"{EffectiveAcceleration:0} / {Math.Max( 1f, BrakingDeceleration ?? EffectiveAcceleration ):0}", Color.Green );
        block.Line( "Path pts / length", $"{_points.Length} / {length:0}u", Color.Orange );
        block.Line( "Goal distance", target.HasValue ? $"{npc.WorldPosition.Distance( target.Value ):0}u" : "-", Color.Orange );
        block.Line( "Arrival radius", target.HasValue ? $"{arrival:0}u" : "-", Color.Cyan );

        var corner = this is AIGroundMotor ground && ground.NextCorner is Vector3 cornerPosition
            ? $"{ground.CornerSpeed:0} / {npc.WorldPosition.Distance( cornerPosition ):0}u" : "-";
        block.Line( "Corner spd / dist", corner, Color.Cyan );
        block.Line( "Facing error", FacingYaw is float yaw ? $"{AngleDelta( yaw, npc.WorldRotation.Yaw() ):0} deg" : "-", Color.Cyan );
        block.Line( "Turn rate", $"{TurnRate:0} deg/s", Color.Cyan );
        return block;
    }

    private string GetMovementStatus( AIController npc, float arrival )
    {
        var agent = npc.Agent;

        if ( !npc.CanMove )
            return "Movement disabled";

        var destination = agent.TargetPosition ?? _destination;
        bool withinArrival = destination.HasValue && npc.WorldPosition.Distance( destination.Value ) <= arrival;
        bool moving = agent.Velocity.Length > 1f;

        if ( !agent.IsNavigating )
        {
            if ( moving )
                return "Slowing down";

            if ( withinArrival )
                return "Arrived";

            return destination.HasValue ? "Stopped" : "Idle";
        }

        if ( withinArrival )
            return "Arriving";

        if ( !_validPath )
            return "No valid path";

        return moving ? "Moving" : "Waiting";
    }

    private static void DrawGoalMarker( Vector3 center )
    {
        for ( int quadrant = 0; quadrant < 4; quadrant++ )
        {
            float midpoint = quadrant * 90f;
            var offset = Rotation.FromYaw( quadrant * 90f ).Forward;

            for ( int i = 0; i < 12; i++ )
            {
                float angle = midpoint - 18f + i * 3f;
                var a = Rotation.FromYaw( angle ).Forward * 6f;
                var b = Rotation.FromYaw( angle + 3f ).Forward * 6f;
                Gizmo.Draw.SolidTriangle( center + offset * 2f, center + offset + a * 1.1f, center + offset + b * 1.1f );
            }
        }

        DrawPositionRing( center, 6f, -1f, sections: 32 );
    }

    private static void DrawRotationStrip( Vector3 a, Vector3 b, float halfWidth )
    {
        var side = Vector3.Cross( (b - a).WithZ( 0 ).Normal, Vector3.Up ) * halfWidth;
        Gizmo.Draw.SolidTriangle( a - side, b - side, b + side );
        Gizmo.Draw.SolidTriangle( a - side, b + side, a + side );
    }

    private static void DrawPositionRing( Vector3 center, float radius, float width, int sections = 64 )
    {
        for ( int i = 0; i < sections; i++ )
        {
            var a = Rotation.FromYaw( i * 360f / sections ).Forward;
            var b = Rotation.FromYaw( (i + 1) * 360f / sections ).Forward;

            Gizmo.Draw.SolidTriangle( center + a * radius, center + b * radius, center + b * (radius - width) );
            Gizmo.Draw.SolidTriangle( center + a * radius, center + b * (radius - width), center + a * (radius - width) );
        }
    }
}
