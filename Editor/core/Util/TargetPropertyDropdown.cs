#if IGNIS
namespace Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sandbox;

[CustomEditor( typeof( TargetPropertyInfo ) )]
public sealed class TargetPropertyDropdown : DropdownControlWidget<string>
{
	private readonly SerializedProperty _targetProperty;
	private readonly List<Entry> _entries = new();
	new PopupWidget _menu;

	public TargetPropertyDropdown( SerializedProperty property )
		: base( property )
	{
		_targetProperty = property;

		// Ensure TargetPropertyInfo exists
		var targetInfo = _targetProperty.GetValue<TargetPropertyInfo>();
		if ( targetInfo is null )
		{
			targetInfo = new TargetPropertyInfo( "EntityType" );
			_targetProperty.SetValue( targetInfo );
		}
	}

	protected override IEnumerable<object> GetDropdownValues()
	{
		_entries.Clear();

		var targetInfo = _targetProperty.GetValue<TargetPropertyInfo>();
		if ( targetInfo is null ) yield break;

		// Get parent object
		var parent = _targetProperty.Parent;
		if ( parent is null ) yield break;

		// Get the Type to pull properties from
		var sourceProp = parent.GetProperty( targetInfo.SourceTypeProperty );
		if ( sourceProp is null ) yield break;

		var entityType = sourceProp.GetValue<Type>();
		if ( entityType is null ) yield break;

		// Gather all writable properties
		var props = entityType.GetProperties( BindingFlags.Public | BindingFlags.Instance )
							  .Where( p => p.CanWrite );

		foreach ( var p in props )
		{
			var entry = new Entry
			{
				Label = p.Name,
				Value = p.Name
			};
			_entries.Add( entry );
		}

		foreach ( var e in _entries )
			yield return e;
	}

	protected override void PaintControl()
	{
		var rect = LocalRect.Shrink( 8, 0 );
		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;

		var targetInfo = _targetProperty.GetValue<TargetPropertyInfo>();

		var selectedName = targetInfo?.PropertyName ?? "";
		var entry = _entries.FirstOrDefault( e => (string)e.Value == selectedName );

		var label = entry.Label ?? (string.IsNullOrEmpty( selectedName ) ? "None" : selectedName);

		Paint.SetPen( color );
		Paint.SetDefaultFont();
		Paint.DrawText( rect, label, TextFlag.LeftCenter );

		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}

	public override void StartEditing()
	{
		if ( !_menu.IsValid )
			OpenMenu();
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( e.LeftMouseButton && !_menu.IsValid() )
			OpenMenu();
	}

	void OpenMenu()
	{
		_menu = new PopupWidget( null )
		{
			Layout = Layout.Column(),
			Width = ScreenRect.Width
		};

		var scroller = _menu.Layout.Add( new ScrollArea( this ), 1 );
		scroller.Canvas = new Widget( scroller )
		{
			Layout = Layout.Column(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand
		};

		var entries = GetDropdownValues().ToArray();

		foreach ( var o in entries )
		{
			var b = scroller.Canvas.Layout.Add( new MenuOption<string>( o, _targetProperty ) );
			b.MouseLeftPress = () =>
			{
				if ( o is Entry e )
				{
					var info = _targetProperty.GetValue<TargetPropertyInfo>();
					if ( info is not null )
					{
						info.PropertyName = (string)e.Value;
						_targetProperty.SetValue( info );
					}
				}

				_menu.Update();
				_menu.Close();
			};
		}

		_menu.Position = ScreenRect.BottomLeft;
		_menu.Visible = true;
		_menu.AdjustSize();
		_menu.ConstrainToScreen();
		_menu.OnPaintOverride = PaintMenuBackground;
	}

	bool PaintMenuBackground()
	{
		Paint.SetBrushAndPen( Theme.ControlBackground );
		Paint.DrawRect( Paint.LocalRect, 0 );
		return true;
	}
}

public class MenuOption<T> : Widget
{
	object info;
	SerializedProperty property;

	public MenuOption( object e, SerializedProperty p ) : base( null )
	{
		info = e;
		property = p;

		Layout = Layout.Row();
		Layout.Margin = 8;

		if ( e is DropdownControlWidget<T>.Entry entry )
		{
			if ( !string.IsNullOrWhiteSpace( entry.Icon ) )
			{
				Layout.Add( new IconButton( entry.Icon ) { Background = Color.Transparent, TransparentForMouseEvents = true, IconSize = 18 } );
			}

			Layout.AddSpacingCell( 8 );
			var c = Layout.AddColumn();
			var title = c.Add( new Label( entry.Label ) );
			title.SetStyles( "font-size: 12px; font-weight: bold; font-family: Poppins; color: white;" );

			if ( !string.IsNullOrWhiteSpace( entry.Description ) )
			{
				var desc = c.Add( new Label( entry.Description.Trim( '\n', '\r', '\t', ' ' ) ) );
				desc.WordWrap = true;
				desc.MinimumHeight = 1;
				desc.MinimumWidth = 400;
			}
		}
		else
		{
			Layout.AddSpacingCell( 8 );
			var c = Layout.AddColumn();
			var title = c.Add( new Label( e.ToString() ) );
			title.SetStyles( "font-size: 12px; font-weight: bold; font-family: Poppins; color: white;" );
		}
	}

	bool HasValue()
	{
		if ( property.IsMultipleDifferentValues ) return false;

		var value = property.GetValue<object>( default );
		return value == info;
	}

	protected override void OnPaint()
	{
		if ( Paint.HasMouseOver || HasValue() )
		{
			Paint.SetBrushAndPen( Theme.Blue.WithAlpha( HasValue() ? 0.3f : 0.1f ) );
			Paint.DrawRect( LocalRect.Shrink( 2 ), 2 );
		}
	}
}
#endif
