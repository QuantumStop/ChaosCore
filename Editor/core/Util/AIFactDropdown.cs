namespace Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using Core;

public sealed class AIFactDropdown : ControlWidget
{
	private PopupWidget _menu;

	public AIFactDropdown( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;
		Layout = Layout.Row();
		Layout.Spacing = 2;
	}

	public override bool IsControlActive => base.IsControlActive || _menu.IsValid();
	public override bool IsControlHovered => base.IsControlHovered || _menu.IsValid();
	public override bool IsControlButton => true;
	public override bool SupportsMultiEdit => true;

	protected override void PaintControl()
	{
		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;
		if ( IsControlDisabled ) color = color.WithAlpha( 0.5f );

		var rect = LocalRect.Shrink( 8, 0 );

		Paint.SetPen( SerializedProperty.IsMultipleDifferentValues ? Theme.MultipleValues : color );
		Paint.DrawText( rect, GetCurrentLabel(), TextFlag.LeftCenter );
		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( IsControlDisabled || !e.LeftMouseButton || _menu.IsValid() ) return;
#if IGNIS || STANDALONE
		OpenMenu();
#endif
	}

	public override void StartEditing()
	{
		if ( IsControlDisabled || _menu.IsValid() ) return;
#if IGNIS || STANDALONE
		OpenMenu();
#endif
	}

	private string GetCurrentLabel()
	{
		if ( SerializedProperty.IsMultipleDifferentValues ) return "Multiple Values"; // i don really know how much we need multiedit?
		var value = SerializedProperty.GetValue<string>( string.Empty ) ?? string.Empty;
		return string.IsNullOrWhiteSpace( value ) ? "None" : value;
	}
#if IGNIS || STANDALONE
	private void OpenMenu()
	{
		PropertyStartEdit();

		var entries = Core.AI.AIFacts.All().OrderBy( f => f ).ToArray();
		var menuWidth = ScreenRect.Width;

		_menu = new PopupWidget( null );
		_menu.Layout = Layout.Column();
		_menu.MinimumWidth = menuWidth;
		_menu.MaximumWidth = menuWidth;
		_menu.VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand;
		_menu.OnLostFocus += PropertyFinishEdit;

		var scroller = _menu.Layout.Add( new ScrollArea( this ), 1 );
		scroller.NoSystemBackground = true;
		scroller.TranslucentBackground = true;
		scroller.Canvas = new Widget( scroller )
		{
			Layout = Layout.Column(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand,
			MaximumWidth = menuWidth
		};

		float contentHeight = 0;

		var noneOption = scroller.Canvas.Layout.Add( new FactMenuOption( "None", string.Empty, SerializedProperty ) );
		noneOption.MouseLeftPress = () =>
		{
			SerializedProperty.SetValue( string.Empty );
			_menu.Close();
		};


		contentHeight += noneOption.FixedHeight;

		foreach ( var fact in entries )
		{
			var option = scroller.Canvas.Layout.Add( new FactMenuOption( fact, fact, SerializedProperty ) );
			option.MouseLeftPress = () =>
			{
				SerializedProperty.SetValue( fact );
				_menu.Close();
			};
			contentHeight += option.FixedHeight;
		}

		scroller.Canvas.AdjustSize();
		_menu.Position = ScreenRect.BottomLeft;
		_menu.Visible = true;
		_menu.AdjustSize();
		_menu.ConstrainToScreen();
		_menu.OnPaintOverride = PaintMenuBackground;

		if ( contentHeight < 200 )
		{
			scroller.FixedHeight = contentHeight;
			_menu.FixedHeight = contentHeight;
		}

		if ( scroller.VerticalScrollbar.Minimum != scroller.VerticalScrollbar.Maximum )
			scroller.Canvas.MaximumWidth -= 8;
	}
#endif

	private bool PaintMenuBackground()
	{
		Paint.SetBrushAndPen( Theme.ControlBackground, Theme.WidgetBackground, 1 );
		Paint.DrawRect( Paint.LocalRect.Shrink( 1 ), 4 );
		return true;
	}
}

internal class FactMenuOption : Widget // internal so others can use this
{
	private readonly string _label;
	private readonly string _value;
	private readonly SerializedProperty _property;

	public FactMenuOption( string label, string value, SerializedProperty property ) : base( null )
	{
		_label = label;
		_value = value;
		_property = property;

		Layout = Layout.Row();
		Layout.Margin = 0;
		VerticalSizeMode = SizeMode.Default;
		FixedHeight = Theme.RowHeight;
		Cursor = CursorShape.Finger;

		var col = Layout.AddColumn();
		col.Margin = new Sandbox.UI.Margin( 8, 4 );
		col.Add( new Label( _label ) ).Color = Theme.Text;
	}

	private bool IsSelected()
	{
		var value = _property.GetValue<string>( string.Empty ) ?? string.Empty;
		return string.Equals( value, _value, StringComparison.Ordinal );
	}

	protected override void OnPaint()
	{
		if ( Paint.HasMouseOver || IsSelected() )
		{
			Paint.SetBrushAndPen( Theme.Blue.WithAlpha( IsSelected() ? 0.5f : 0.1f ) );
			Paint.DrawRect( LocalRect );
		}
	}
}

[CustomEditor( typeof( string ), WithAllAttributes = new[] { typeof( AIFactSelectorAttribute ) } )]
public class AIFactSelectorCW : ControlWidget
{
	public override bool SupportsMultiEdit => true;

	public AIFactSelectorCW( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;
		Layout.Add( new AIFactDropdown( property ) );
	}
}

