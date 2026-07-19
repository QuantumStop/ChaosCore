namespace Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core;
using Core.AI;

/// <summary>
/// Dropdown widget for selecting AIBehavior types.
/// </summary>
public sealed class AIBehaviorDropdown : ControlWidget
{
	private readonly record struct BehaviorEntry( string Label, string Value );
	private readonly Type _baseType = typeof( AIBehavior );
	private PopupWidget _menu;

	public AIBehaviorDropdown( SerializedProperty property ) : base( property )
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
		if ( IsControlDisabled ) return;
		if ( !e.LeftMouseButton ) return;
		if ( _menu.IsValid() ) return;

		OpenMenu();
	}

	public override void StartEditing()
	{
		if ( IsControlDisabled ) return;
		if ( _menu.IsValid() ) return;
		OpenMenu();
	}

	private string GetCurrentLabel()
	{
		if ( SerializedProperty.IsMultipleDifferentValues )
			return "Multiple Values";

		var value = SerializedProperty.GetValue( string.Empty ) ?? string.Empty;
		if ( string.IsNullOrWhiteSpace( value ) )
			return "None";

		return value;
	}

	private IEnumerable<BehaviorEntry> GetEntries()
	{
		yield return new BehaviorEntry( "None", string.Empty );

		var types = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany( assembly =>
			{
				try { return assembly.GetTypes(); }
				catch ( ReflectionTypeLoadException e ) { return e.Types.Where( t => t is not null )!; }
			} )
			.Where( t => t is not null && t.IsClass && !t.IsAbstract && _baseType.IsAssignableFrom( t ) && t != _baseType )
			.OrderBy( t => t.Name );

		foreach ( var type in types )
		{
			yield return new BehaviorEntry( type.Name, type.Name );
		}
	}

	private void OpenMenu()
	{
		PropertyStartEdit();

		var entries = GetEntries().ToArray();
		var menuWidth = ScreenRect.Width;

		_menu = new PopupWidget( null )
		{
			Layout = Layout.Column(),
			MinimumWidth = menuWidth,
			MaximumWidth = menuWidth,
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand
		};
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
		foreach ( var entry in entries )
		{
			var option = scroller.Canvas.Layout.Add( new BehaviorMenuOption( entry.Label, entry.Value, SerializedProperty ) );
			option.MouseLeftPress = () =>
			{
				SerializedProperty.SetValue( entry.Value );
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
		{
			scroller.Canvas.MaximumWidth -= 8;
		}
	}

	private bool PaintMenuBackground()
	{
		Paint.SetBrushAndPen( Theme.ControlBackground, Theme.WidgetBackground, 1 );
		Paint.DrawRect( Paint.LocalRect.Shrink( 1 ), 4 );
		return true;
	}

}

file class BehaviorMenuOption : Widget
{
	private readonly string _label;
	private readonly string _value;
	private readonly SerializedProperty _property;

	public BehaviorMenuOption( string label, string value, SerializedProperty property ) : base( null )
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
		var title = col.Add( new Label( _label ) );
		title.Color = Theme.Text;
	}

	private bool IsSelected()
	{
		var value = _property.GetValue( string.Empty ) ?? string.Empty;
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

/// <summary>
/// Custom control widget that uses AIBehaviorDropdown when [AIBehaviorSelector] is present.
/// </summary>
[CustomEditor( typeof( string ), WithAllAttributes = new[] { typeof( AIBehaviorSelectorAttribute ) } )]
public class AIBehaviorSelectorCW : ControlWidget
{
	public override bool SupportsMultiEdit => true;

	public AIBehaviorSelectorCW( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;

		var dropdown = new AIBehaviorDropdown( property );
		Layout.Add( dropdown );
	}
}
