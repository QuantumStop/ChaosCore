namespace Editor;

using System;
using Sandbox;

public class PippedSlider : FloatSlider
{
	public int Steps => Math.Max( 1, (int)(Maximum - Minimum) + 1 );
	public string[] StepIcons { get; set; }

	// --- Independent(!) vertical offsets ---
	public float HandleYOffset { get; set; } = -2f;   // relative to widget center
	public float GrooveYOffset { get; set; } = -6f;
	public float PipYOffset { get; set; } = 12f;


	// --- Handle dimensions ---
	public float HandleHeight { get; set; } = 15f;
	public float HandleWidth { get; set; } = 10f;


	// --- Theme ---
	public Color GrooveBase { get; set; } = Color.Parse( "#1C1C1C" )!.Value;
	public Color GrooveHighlight { get; set; } = Color.Parse( "#4C4C4C" )!.Value;
	public Color GrooveShadow { get; set; } = Color.Parse( "#0D0D0D" )!.Value;
	public Color HandleColor { get; set; } = Color.Parse( "#616161ff" )!.Value;
	public Color PipColor { get; set; } = Color.Gray.WithAlpha( 0.75f );


	public PippedSlider( Widget parent ) : base( parent )
	{
		MinimumHeight = 65;
		MaximumHeight = 80;
		MinimumWidth = 60;
		Step = 1;
	}

	protected override void OnPaint()
	{
		Rect rect = LocalRect;
		float centerY = rect.Center.y;

		// --- Groove positioning ---
		float grooveLeft = rect.Left + 5;
		float grooveWidth = rect.Width - 16;
		float grooveHeight = 3f;
		float grooveY = centerY + GrooveYOffset;

		// --- Groove (track under handle) ---
		Rect grooveRect = new( grooveLeft, grooveY - grooveHeight / 2, grooveWidth, grooveHeight );
		Paint.SetBrush( GrooveBase );
		Paint.ClearPen();
		Paint.DrawRect( grooveRect );

		Paint.SetPen( GrooveHighlight, 2 );
		Paint.DrawLine( grooveRect.BottomLeft, grooveRect.BottomRight );
		Paint.SetPen( GrooveShadow, 1 );
		Paint.DrawLine( grooveRect.TopLeft, grooveRect.TopRight );

		// --- Pips & Icons ---
		if ( Steps > 1 )
		{
			float spacing = grooveRect.Width / (Steps - 1);
			float pipBaseY = grooveRect.Bottom + PipYOffset;

			for ( int i = 0; i < Steps; i++ )
			{
				float x = grooveRect.Left + i * spacing;
				float pipHeight = 5;
				Rect pipRect = new( x - 0.5f, pipBaseY - pipHeight / 2, 1, pipHeight );

				Paint.SetBrushAndPen( PipColor, PipColor );
				Paint.DrawRect( pipRect );

				// --- Draw icon below pip ---
				if ( StepIcons is not null && i < StepIcons.Length )
				{
					string icon = StepIcons[i];
					float iconSize = 15;
					float padding = 7f;   // space between pip and icon

					Rect iconRect = new(
						x - iconSize / 2,
						pipRect.Bottom + padding,
						iconSize,
						iconSize
					);

					Paint.DrawIcon( iconRect, icon, (int)iconSize, TextFlag.Center );
				}
			}
		}

		// --- Handle ---
		float handleX = grooveLeft + grooveWidth * ((Value - Minimum) / (Maximum - Minimum));
		float handleTop = centerY + HandleYOffset - HandleHeight; // top of handle
		float handleBottom = handleTop + HandleHeight + 5;        // bottom tip of triangle
		float halfWidth = HandleWidth / 2;

		Vector2[] handlePolygon =
		[
			new Vector2(handleX - halfWidth, handleTop),            // top-left
 		   	new Vector2(handleX + halfWidth, handleTop),            // top-right
    		new Vector2(handleX + halfWidth, handleBottom - 6),     // bottom-right rectangle portion
    		new Vector2(handleX, handleBottom),                     // tip of triangle
    		new Vector2(handleX - halfWidth, handleBottom - 5.5f)   // bottom-left rectangle portion
		];

		Paint.SetBrush( HandleColor );
		Paint.ClearPen();
		Paint.DrawPolygon( handlePolygon );

		// --- Bevel / shading ---

		Paint.SetPen( HandleColor.Lighten( 0.25f ), 1 );

		Paint.DrawLine( handlePolygon[0], handlePolygon[1] ); // top-left -> top-right
		Paint.DrawLine( handlePolygon[0], handlePolygon[4] ); // top-left -> bottom-left rectangle

		Paint.Antialiasing = true;

		Paint.SetPen( HandleColor.Darken( 0.65f ), 1 );

		Paint.Antialiasing = false;

		Paint.DrawLine( handlePolygon[1], handlePolygon[2] ); // top-right -> bottom-right rectangle
		Paint.DrawLine( handlePolygon[2], handlePolygon[3] ); // bottom-right -> triangle tip
		Paint.DrawLine( handlePolygon[3], handlePolygon[4] ); // triangle tip -> bottom-left rectangle
	}
}
