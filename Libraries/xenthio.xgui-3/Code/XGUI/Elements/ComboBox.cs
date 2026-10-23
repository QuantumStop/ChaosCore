using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;
using System;
using System.Collections.Generic;
using System.Linq;
namespace XGUI;

/// <summary>
/// A UI control which provides multiple options via a dropdown box.
/// </summary>
[Library( "combobox" ), Alias( "selector" )]
public class ComboBox : Button
{

	XGUIPopup _pane;
	/// <summary>
	/// The icon of an arrow pointing down on the right of the element.
	/// </summary>
	protected IconPanel _dropdownIndicator;

	/// <summary>
	/// Called when the value has been changed,
	/// </summary>
	public System.Action<string> ValueChanged { get; set; }

	/// <summary>
	/// Called just before opening, allows options to be dynamic
	/// </summary>
	public System.Func<List<Option>> BuildOptions { get; set; }

	/// <summary>
	/// The options to show on click. You can edit these directly via this property.
	/// </summary>
	public List<Option> Options { get; set; } = [];

	Option _selected;

	object _value;
	int _valueHash;

	/// <summary>
	/// The current string value. This is useful to have if Selected is null.
	/// </summary>
	public override object Value
	{
		get => _value;
		set
		{
			if ( _valueHash == HashCode.Combine( value ) )
				return;

			if ( $"{_value}" == $"{value}" )
				return;

			_valueHash = HashCode.Combine( value );
			_value = value;

			if ( BuildOptions != null )
			{
				Options = BuildOptions.Invoke();
			}

			if ( _value != null && Options.Count == 0 )
			{
				PopulateOptionsFromType( _value.GetType() );
			}

			Select( _value?.ToString(), false );
		}
	}

	/// <summary>
	/// The currently selected option.
	/// </summary>
	public Option Selected
	{
		get => _selected;
		set
		{
			if ( _selected == value ) return;

			_selected = value;

			if ( _selected != null )
			{
				Value = $"{_selected.Value}";
				Icon = _selected.Icon;
				Text = _selected.Title;

				ValueChanged?.Invoke( Value.ToString() );
				CreateEvent( "onchange" );
				CreateValueEvent( "value", _selected?.Value );
			}
		}
	}

	public ComboBox()
	{
		AddClass( "selector" );
		_dropdownIndicator = Add.Icon( "u", "selector_indicator" );
	}

	public ComboBox( Panel parent ) : this()
	{
		Parent = parent;
	}

	/// <summary>
	/// Given the type, populate options. This is useful if you're an enum type.
	/// </summary>
	private void PopulateOptionsFromType( Type type )
	{
		if ( type == typeof( bool ) )
		{
			Options.Add( new Option( "True", true ) );
			Options.Add( new Option( "False", false ) );
			return;
		}

		if ( type.IsEnum )
		{
			var names = type.GetEnumNames();
			var values = type.GetEnumValues();

			for ( int i = 0; i < names.Length; i++ )
			{
				Options.Add( new Option( names[i], values.GetValue( i ) ) );
			}

			return;
		}

		//Log.Info( $"Dropdown Type: {type}" );
	}


	public bool IsOpen = false;
	protected override void OnMouseDown( MousePanelEvent e )
	{
		base.OnMouseDown( e );
		if ( !IsOpen )
		{
			Open();
		}
		else
		{
			Close();
		}
	}
	/// <summary>
	/// Open the dropdown.
	/// </summary>
	public void Open()
	{
		IsOpen = true;
		_pane = new XGUIPopup( this, XGUIPopup.PositionMode.BelowStretch, 0.0f );
		_pane.Focus();

		_pane.AddClass( "flat-top" );
		if ( BuildOptions != null )
		{
			Options = BuildOptions.Invoke();
		}

		foreach ( var option in Options )
		{
			var o = _pane.AddOption( option.Title, option.Icon, () => Select( option ) );
			if ( Selected != null && option.Value == Selected.Value )
			{
				o.AddClass( "active" );
			}
		}
	}

	public void Close()
	{
		if ( _pane != null )
		{
			_pane.Delete();
			_pane = null;
		}
		Focus();
		IsOpen = false;
	}

	/// <summary>
	/// Select an option.
	/// </summary>
	protected virtual void Select( Option option, bool triggerChange = true )
	{
		if ( !triggerChange )
		{
			_selected = option;

			if ( option != null )
			{
				Value = option.Value;
				Icon = option.Icon;
				Text = option.Title;
			}
		}
		else
		{
			Selected = option;
		}
		Close();
	}

	/// <summary>
	/// Select an option by value string.
	/// </summary>
	protected virtual void Select( string value, bool triggerChange = true )
	{
		Select( Options.FirstOrDefault( x => string.Equals( x.Value.ToString(), value, StringComparison.OrdinalIgnoreCase ) ), triggerChange );
	}
	string _override = null;
	public override void Tick()
	{
		base.Tick();

		SetClass( "open", _pane != null && !_pane.IsDeleting );
		SetClass( "active", _pane != null && !_pane.IsDeleting );

		_pane?.Style.Width = Box.Rect.Width * ScaleFromScreen;
	}
	public override void SetProperty( string name, string value )
	{
		if ( name == "default" )
		{
			_override = value;
			return;
		}
		base.SetProperty( name, value );
	}
	protected override void OnParametersSet()
	{
		// Only clear if we have some options to populate
		if ( Children.Any( x => x.ElementName.Equals( "option", StringComparison.OrdinalIgnoreCase ) ) ) Options.Clear();

		foreach ( var child in Children.ToList() )
		{
			if ( child.ElementName.Equals( "option", StringComparison.OrdinalIgnoreCase ) )
			{
				var o = new Option();
				o.Title = string.Join( "", child.Descendants.OfType<Label>().Select( x => x.Text ) );
				o.Value = child.GetAttribute( "value", o.Title );
				o.Icon = child.GetAttribute( "icon", null );

				Options.Add( o );
				if ( _override == child.GetAttribute( "value", o.Title ) )
				{
					Select( o, true );
				}
			}
		}
	}
}
