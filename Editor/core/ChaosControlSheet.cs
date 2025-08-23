using System;
using Microsoft.CodeAnalysis;
using Sandbox.Internal;

namespace Editor;
using Core;

public static class ChaosUtility
{

	/// <summary>
	/// Show a popup control sheet for this. You should set parent to the control from
	/// this this sheet is created. If you do that properly, when that control is deleted,
	/// this popup will get deleted too. If you set it to null then the control sheet
	/// will stay open until it's closed.
	/// </summary>
    public static Widget OpenControlSheet(SerializedObject so, Widget parent)
    {

		Type type = so.Targets.FirstOrDefault()?.GetType();
		
		while ( type != null )
		{
			// We check for an IPopupEditor for the type
			var genericType = typeof( IPopupEditor<> ).MakeGenericType( type );
			var typeDescription = GlobalToolsNamespace.EditorTypeLibrary
				.GetTypes<Widget>()
				.FirstOrDefault( x => x.Interfaces.Contains( genericType ) );

			if ( typeDescription != null )
			{
				try
				{
					return typeDescription.Create<CrosshairPreviewWidget>( new object[] { so, parent } );
				}
				catch ( Exception exception )
				{
					GlobalGameNamespace.Log.Error( exception, $"Exception when creating {typeDescription.FullName}" );
				}
			}

			type = type.BaseType;
		}




		var container = new Widget( parent );
		container.Layout = Layout.Column();
		container.Layout.Spacing = 6;

		// Create the ControlSheet and add it to the layout
		var sheet = new ControlSheet();
		sheet.AddObject( so );
		container.Layout.Add( sheet ); // Add ControlSheet like a widget here

		// Your custom preview
		var segment = so.Targets.FirstOrDefault() as CrosshairCircleSegment;
		if ( segment != null )
		{
			var preview = new CrosshairPreviewWidget( container, segment );
			preview.Size = 200f;
			container.Layout.Add( preview );
		}

		// Return the container that includes both the sheet and preview
		return container;

		// // Default fallback editor UI
		// var container = new Widget(parent);
		// container.Layout = Layout.Column();
		// container.Layout.Spacing = 6;

		// // Main property editor
		// var sheet = new ControlSheet();
		// sheet.AddObject(so);
		// container.Layout.Add(sheet);

		// // Declare preview outside so it can be referenced later
		// CrosshairPreviewWidget preview = null;

		// if (so.Targets.FirstOrDefault() is CrosshairCircleSegment segment)
		// {
		//     preview = new CrosshairPreviewWidget(container, segment);
		//     preview.Size = 200f;
		//     container.Layout.Add(preview);

		//     Log.Info("CrosshairPreviewWidget added to layout.");
		// }

		// if (preview != null)
		// {
		//     so.OnPropertyChanged += (SerializedProperty p) => preview.Invalidate();
		// }

		// // Add preview widget if applicable
		// so.OnPropertyChanged += (SerializedProperty p) =>
		// {
		//     if (p.Name == nameof(CrosshairCircleSegment.CrosshairCircleThickness) ||
		//         p.Name == nameof(CrosshairCircleSegment.CrosshairCircleColor))
		//     {
		//         preview.Invalidate();
		//     }
		// };

    }

}


public class CrosshairPreviewWidget : Widget
{
	public CrosshairCircleSegment Data { get; set; }

	public CrosshairPreviewWidget( Widget parent, CrosshairCircleSegment data ) : base( parent )
	{
		Data = data;
		Size = new Vector2( 200, 200 ); // Explicit width & height
		Log.Info( "Widget Created" );

	}


	protected override void OnPaint()
	{
		Log.Info( "CrosshairPreviewWidget OnPaint called." );

		var rect = LocalRect;
		Paint.SetPen( Color.Transparent ); // No border
		Paint.SetBrush( Color.Red );       // Fill color
		Paint.DrawRect( rect );            // Draw filled rect
		Log.Info( "OnPaint called" );
		Paint.DrawRect( LocalRect, 5 );
	}

}



public class CrosshairPreviewWindow : Window
{
	
	
    public CrosshairPreviewWindow() : base( null )
	{
		Title = "Crosshair Preview";
		Size = new Vector2( 300, 300 );

		var preview = new CrosshairPreviewWidget( this, new CrosshairCircleSegment() );
		preview.Size = new Vector2( 280, 280 );
		Layout = Layout.Column();
		Layout.Add( preview );

		Show();
	}

    [Menu("Tools", "Crosshair/Preview")]
    public static void Open()
    {
        new CrosshairPreviewWindow();
    }
}

public class DebugPreviewWidget : Widget
{
    public DebugPreviewWidget(Widget parent) : base(parent, false)
    {
        Log.Info("DebugPreviewWidget created");
        Size = new Vector2(200, 200);
    }

    protected override void OnPaint()
    {
        Log.Info("DebugPreviewWidget OnPaint called");

        Paint.SetPen(Color.Transparent);
        Paint.SetBrush(Color.Red);
        Paint.DrawRect(LocalRect);  // Fill widget with red
    }
}
