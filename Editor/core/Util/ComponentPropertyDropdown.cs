namespace Editor;

using System;
using System.Reflection;
using Core;
using System.Collections.Generic;
using System.Linq;

public class ComponentPropertyDropdown : DropdownControlWidget<string>
{
	private readonly SerializedProperty _property;

	public ComponentPropertyDropdown( SerializedProperty property ) : base( property )
	{
		_property = property;
	}

	protected override IEnumerable<object> GetDropdownValues()
	{
		// Get the attribute
		if ( !_property.TryGetAttribute<TargetPropertySelectorAttribute>( out var attr ) )
			yield break;

		// Determine type to inspect
		Type typeToInspect = attr.TargetType;

		if ( typeToInspect is null )
			yield break;

		// Get writable properties
		var props = typeToInspect.GetProperties( BindingFlags.Public | BindingFlags.Instance )
			.Where( p => p.CanWrite );

		foreach ( var p in props )
		{
			yield return new Entry
			{
				Label = p.Name,
				Value = p.Name
			};
		}
	}
}

/// <summary>
/// Custom editor to show ComponentPropertyDropdown when [TargetPropertySelector] is applied.
/// </summary>
[CustomEditor( typeof( object ), WithAllAttributes = new[] { typeof( TargetPropertySelectorAttribute ) } )]
public class TargetPropertySelectorCW : ControlWidget
{
	public TargetPropertySelectorCW( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;

		// Find the component type property in the serialized object
		var compTypeProp = property.Parent.GetProperty( "ComponentType" );

		var dropdown = new ComponentPropertyDropdown( compTypeProp );
		Layout.Add( dropdown );
	}
}
