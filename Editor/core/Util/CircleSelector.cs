using System;

public class CircleSelector : Widget
{
	public string[] Options { get; set; }
	public int SelectedIndex { get; set; } = 0;

	public int Value
	{
		get => SelectedIndex;
		set
		{
			if ( Options is null || Options.Length == 0 ) return;
			SelectedIndex = Math.Clamp( value, 0, Options.Length - 1 );
			OnSelected?.Invoke( SelectedIndex );
		}
	}

	public float CircleSize { get; set; } = 16;
	public float InnerCircleSize { get; set; } = 8;
	public float Spacing { get; set; } = 8f;

	public Color OuterColor { get; set; } = Color.Parse( "#4C4C4C" )!.Value;
	public Color InnerColor { get; set; } = Color.Parse( "#cacacaff" )!.Value;

	public Color DisabledColor { get; set; } = Color.Parse( "#1C1C1C" )!.Value;

	public Action<int> OnSelected { get; set; } // Callback when selection changes

	public CircleSelector( Widget parent ) : base( parent )
	{
		MinimumHeight = (int)CircleSize;
		MinimumWidth = (int)(CircleSize * (Options?.Length ?? 1) + Spacing * ((Options?.Length ?? 1) - 1));
	}

	protected override void OnPaint()
	{
		base.OnPaint();

		if ( Options is null || Options.Length == 0 ) return;

		var rect = LocalRect;
		float x = rect.Left;

		for ( int i = 0; i < Options.Length; i++ )
		{
			// Measure label size
			var labelRect = Paint.MeasureText( new Rect( 0, 0, 0, 0 ), Options[i] );
			float blockWidth = CircleSize + 4 + labelRect.Width; // circle + spacing + text
			float blockHeight = MathF.Max( CircleSize, labelRect.Height );

			// Encapsulate option in rect
			var optionRect = new Rect( x, rect.Center.y - blockHeight / 2, blockWidth, blockHeight );

			// --- Draw outer circle (polygon outline) ---
			var outerRect = new Rect( optionRect.Left, optionRect.Center.y - CircleSize / 2, CircleSize, CircleSize );

			Paint.SetBrush( Color.Transparent );
			Paint.SetPen( OuterColor, 2 );
			Paint.Antialiasing = true;

			int segments = 32;
			var points = new List<Vector2>();
			for ( int s = 0; s < segments; s++ )
			{
				float angle = s / (float)segments * MathF.PI * 2;
				float cx = outerRect.Center.x + MathF.Cos( angle ) * (outerRect.Width / 2);
				float cy = outerRect.Center.y + MathF.Sin( angle ) * (outerRect.Height / 2);
				points.Add( new Vector2( cx, cy ) );
			}

			Paint.DrawPolygon( points );

			// --- Draw inner circle if selected ---
			if ( i == SelectedIndex )
			{
				var innerRect = new Rect(
					outerRect.Center.x - InnerCircleSize / 2,
					outerRect.Center.y - InnerCircleSize / 2,
					InnerCircleSize,
					InnerCircleSize
				);

				Paint.SetBrush( InnerColor );
				Paint.ClearPen();
				Paint.Antialiasing = true;
				Paint.DrawCircle( innerRect );
			}

			// --- Draw text to the right of the circle ---
			var labelPos = new Vector2(
				outerRect.Right + 4, // small spacing
				optionRect.Center.y - labelRect.Height / 2
			);

			Paint.SetPen( InnerColor );
			Paint.DrawText( labelPos, Options[i] );

			// Move x for next option
			x += optionRect.Width + Spacing;
		}
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );
		if ( e.Accepted ) return;
		if ( e.Button != MouseButtons.Left ) return;

		var rect = LocalRect;
		float x = rect.Left;

		for ( int i = 0; i < Options.Length; i++ )
		{
			var labelRect = Editor.Paint.MeasureText( new Rect( 0, 0, 0, 0 ), Options[i] );
			float blockWidth = CircleSize + 4 + labelRect.Width;
			float blockHeight = MathF.Max( CircleSize, labelRect.Height );

			// Option's full rect
			var optionRectLeft = x;
			var optionRectTop = rect.Center.y - blockHeight / 2;
			var optionRectRight = optionRectLeft + blockWidth;
			var optionRectBottom = optionRectTop + blockHeight;

			var mousePos = e.LocalPosition;

			if ( mousePos.x >= optionRectLeft && mousePos.x <= optionRectRight &&
				mousePos.y >= optionRectTop && mousePos.y <= optionRectBottom )
			{
				SelectedIndex = i;
				OnSelected?.Invoke( i );
				e.Accepted = true;
				break;
			}

			x += blockWidth + Spacing;
		}
	}

}
