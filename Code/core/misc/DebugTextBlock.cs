namespace Core;

using System;

/// <summary>
/// A block of debug text with labels and values.
/// </summary>
public sealed class DebugTextBlock
{
    /// <summary>
    /// The width of a debug column in pixels.
    /// </summary>
    public const float Width = 240f;

    private readonly record struct Row( string Label, string Value, Color Color, float Y );
    private readonly List<Row> _rows = [];

    /// <summary>
    /// The total height of the block in pixels.
    /// </summary>
    public float Height { get; private set; }

    /// <summary>
    /// Adds a section header.
    /// </summary>
    public void Header( string text )
    {
        _rows.Add( new( text, null, Color.White, Height ) );
        Height += 21f;
    }

    /// <summary>
    /// Adds a label and value row.
    /// </summary>
    public void Line( string label, string value, Color color )
    {
        _rows.Add( new( label, value, color, Height ) );
        Height += 14f;
    }

    /// <summary>
    /// Adds vertical space in pixels.
    /// </summary>
    public void Space( float height = 14f ) => Height += height;

    /// <summary>
    /// Adds another block below this one.
    /// </summary>
    public void Append( DebugTextBlock block )
    {
        foreach ( var row in block._rows )
        {
            _rows.Add( row with { Y = Height + row.Y } );
        }

        Height += block.Height;
    }

    /// <summary>
    /// Draws the block using a right screen margin and top position.
    /// </summary>
    public void Draw( Vector2 position, TextRendering.Scope scope )
    {
        float right = MathF.Round( position.x );
        float left = MathF.Round( Screen.Width - right - Width );
        scope.FontVariantNumeric = Sandbox.UI.FontVariantNumeric.TabularNums;

        foreach ( var row in _rows )
        {
            float y = MathF.Round( position.y + row.Y );
           
            scope.Text = row.Label;
            scope.TextColor = row.Color;
            
            DebugOverlaySystem.Current.ScreenText( new Vector2( left, y ), scope, TextFlag.Left, 0f );

            if ( row.Value is null ) 
                continue;
                
            scope.Text = row.Value;
            DebugOverlaySystem.Current.ScreenText( new Vector2( -right, y ), scope, TextFlag.Right, 0f );
        }
    }
}
