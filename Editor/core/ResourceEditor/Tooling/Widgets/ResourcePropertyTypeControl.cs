namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Linq;

[AttributeUsage( AttributeTargets.Property )]
public sealed class ResourcePropertyTypeAttribute : Attribute { }

[CustomEditor( typeof( string ), WithAllAttributes = [typeof( ResourcePropertyTypeAttribute )] )]
public sealed class ResourcePropertyTypeControl : ControlWidget
{
	private AdvancedDropdownPopup _popup;

	public ResourcePropertyTypeControl( SerializedProperty property ) : base( property )
	{
		PaintBackground = false;
		Cursor = CursorShape.Finger;
		MouseTracking = true;
	}

	public override bool IsControlButton => true;

	public override bool IsControlActive => base.IsControlActive || _popup?.IsValid == true;

	protected override void PaintControl()
	{
		var type = ResourceCodegen.ResolveType( SerializedProperty.GetValue<string>() );
		Theme.DrawDropdown( LocalRect, type is null ? "Select type..." : ResourceCodegen.TypeName( type ), "data_object", IsControlActive, IsControlDisabled );
	}

	public override void StartEditing()
	{
		if ( IsControlDisabled || _popup?.IsValid == true )
			return;

		_popup = OpenPicker( this, ResourceCodegen.PropertyTypes(), type =>
		{
			PropertyStartEdit();
			SerializedProperty.SetValue( type.FullName );
			SignalValuesChanged();
			PropertyFinishEdit();
		} );
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( !e.LeftMouseButton || IsControlDisabled )
			return;

		e.Accepted = true;
		StartEditing();
	}

	protected override void OnDoubleClick( MouseEvent e ) { }

	public static AdvancedDropdownPopup OpenPicker( Widget anchor, IEnumerable<Type> types, Action<Type> selected )
	{
		var values = types.Distinct().OrderBy( ResourceCodegen.TypeName ).ToArray();

		var popup = new AdvancedDropdownPopup( anchor );
		popup.Dropdown.RootTitle = "Type";
		popup.Dropdown.SearchPlaceholderText = "Find a type...";
		popup.Dropdown.OnBuildItems = root =>
		{
			foreach ( var type in values )
			{
				root.Add( new AdvancedDropdownItem( ResourceCodegen.TypeName( type ), type.IsEnum ? "list" : "data_object", type ) { Description = type.Namespace, Tooltip = type.FullName } );
			}
		};
		popup.OnSelect = value =>
		{
			if ( value is Type type )
				selected( type );
		};
		popup.Dropdown.Rebuild();
		popup.OpenAt( anchor.ScreenRect.BottomLeft, animateOffset: new Vector2( 0, -4 ) );

		return popup;
	}
}
