namespace Core.Editor;

public sealed partial class ResourceEditor
{
	private sealed class InspectorSection : Widget
	{
		public InspectorSection( Widget parent, bool footer = false ) : base( parent )
		{
			// Make both footer areas the same height.
			if ( footer )
				FixedHeight = Theme.RowHeight + 26;

			Layout = Layout.Row();
			Layout.Margin = 12;
			Layout.Spacing = 8;
		}

		protected override void OnPaint()
		{
			Paint.ClearPen();
			Paint.SetBrush( Theme.WidgetBackground );
			Paint.DrawRect( LocalRect );
		}
	}
}
