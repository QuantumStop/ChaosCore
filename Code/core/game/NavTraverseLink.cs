namespace Core;

using Core.AI;

[Title( "NavMesh - Traversal Link" )]
[Category( "Navigation" )]
[Description( """
    Navigation Traversal links do everything regular links can, but are extended to support LinkMoveType 
    so it can be defined what type of traversal is supported for this link.
""" )]
public class NavMeshTraverseLink : NavMeshLink
{
    /// <summary>
    /// Allowed from start to end. Select one to require it.
    /// </summary>
    [Property, Title( "Forward Traversals" )]
    public AIController.LinkMoveType AllowedTraversals { get; set; } = AIController.LinkMoveType.MOVE_JUMP
    | AIController.LinkMoveType.MOVE_CLIMB
    | AIController.LinkMoveType.MOVE_VAULT
    | AIController.LinkMoveType.MOVE_DROP
    | AIController.LinkMoveType.MOVE_DROP;

    /// <summary>
    /// Allowed from end to start. Select one to require it.
    /// </summary>
    [Property, Title( "Reverse Traversals" ), ShowIf( nameof( IsBiDirectional ), true )]
    public AIController.LinkMoveType InTraversals { get; set; } = AIController.LinkMoveType.MOVE_JUMP
    | AIController.LinkMoveType.MOVE_CLIMB
    | AIController.LinkMoveType.MOVE_VAULT
    | AIController.LinkMoveType.MOVE_DROP
    | AIController.LinkMoveType.MOVE_DROP;

    public AIController.LinkMoveType GetAllowedTraversals( Vector3 start, Vector3 end )
    {
        bool reverse = (end - start).Dot( WorldEndPosition - WorldStartPosition ) < 0f;
        return reverse ? IsBiDirectional ? InTraversals : AIController.LinkMoveType.MOVE_NONE : AllowedTraversals;
    }

    protected override void OnLinkEntered( NavMeshAgent agent )
    {
        base.OnLinkEntered( agent );
        var npc = agent.GetComponentInParent<AIController>();

        if ( !npc.IsValid() || npc.Navigation?.Motor?.UseLinkTraversal == true )
            return;

        agent.Stop();
        npc.Navigation?.ReportFailure();
    }

    private int _selectedEndpoint;

    protected override void DrawGizmos()
    {
        using var scope = Gizmo.Scope( "Traversal link" );
        Gizmo.Draw.IgnoreDepth = false;

        bool selected = Gizmo.IsSelected;
        DrawEndpoint( "Start", LocalStartPosition, 0, selected );
        DrawEndpoint( "End", LocalEndPosition, 1, selected );

        var start = LocalStartPosition;
        var end = LocalEndPosition;
        var delta = end - start;

        if ( delta.Length < 0.001f )
            return;

        Gizmo.Draw.Color = Gizmo.IsSelected ? Color.Magenta : Color.Magenta.WithAlpha(0.65f);
        var side = Vector3.Cross( delta.Normal, Vector3.Up );

        if ( side.Length < 0.001f )
            side = Vector3.Cross( delta.Normal, Vector3.Forward );

        side = side.Normal * 2f;

        const int segments = 32;

        var points = new Vector3[segments + 1];
        var distances = new float[segments + 1];
        float height = System.Math.Max( 32f, delta.Length * 0.25f );

        for ( int i = 0; i <= segments; i++ )
        {
            float t = (float)i / segments;
            points[i] = Vector3.Lerp( start, end, t ) + Vector3.Up * (4f * height * t * (1f - t));
           
            if ( i > 0 )
                distances[i] = distances[i - 1] + points[i - 1].Distance( points[i] );
        }

        float length = distances[segments];
        float headLength = System.Math.Min( 12f, length * (IsBiDirectional ? 0.5f : 0.9f) );
        float from = IsBiDirectional ? headLength : 0f;
        float to = length - headLength;

        for ( int i = 1; i <= segments; i++ )
        {
            float a = System.Math.Max( from, distances[i - 1] );
            float b = System.Math.Min( to, distances[i] );
            if ( b <= a )
                continue;

            var first = PointAt( a );
            var last = PointAt( b );
            Gizmo.Draw.SolidTriangle( first + side, last + side, last - side );
            Gizmo.Draw.SolidTriangle( first + side, last - side, first - side );
        }

        DrawHead( end, PointAt( to ) );

        if ( IsBiDirectional )
            DrawHead( start, PointAt( from ) );

        Vector3 PointAt( float distance )
        {
            for ( int i = 1; i <= segments; i++ )
            {
                if ( distance > distances[i] )
                    continue;
                float span = distances[i] - distances[i - 1];
                return Vector3.Lerp( points[i - 1], points[i], span > 0f ? (distance - distances[i - 1]) / span : 0f );
            }
            return end;
        }

        void DrawHead( Vector3 tip, Vector3 root )
        {
            Gizmo.Draw.SolidTriangle( root + side * 2f, tip, root - side * 2f );
        }
    }

    private void DrawEndpoint( string name, Vector3 position, int index, bool selected )
    {
        using var scope = Gizmo.Scope( name );
        var bounds = new BBox( position - 5f, position + 5f );
        Gizmo.Hitbox.BBox( bounds );

        if ( Gizmo.WasClicked )
        {
            _selectedEndpoint = index;
            Gizmo.Select( allowUnselect: false );
        }

        Gizmo.Draw.Color = selected || Gizmo.IsHovered ? Color.Yellow : Color.White.WithAlpha( 0.8f );

        Gizmo.Draw.Model( "models/editor/ground_node_hint.vmdl", new Transform( position, Rotation.Identity, selected || Gizmo.IsHovered ? 1f : 0.95f ) );

        if ( !Gizmo.IsSelected || _selectedEndpoint != index )
            return;

        if ( Gizmo.Control.Position( name, position, out var moved, axisRotation: Rotation.Identity ) )
        {
            if ( index == 0 )
                LocalStartPosition = moved;
            else
                LocalEndPosition = moved;
        }
    }
}
