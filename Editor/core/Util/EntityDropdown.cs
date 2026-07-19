#if IGNIS
namespace Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core;

/// <summary>
/// Dropdown widget for selecting concrete BaseEntity types.
/// </summary>
public sealed class EntityDropdown( SerializedProperty property ) : DropdownControlWidget<object>( property )
{
	private readonly Type _baseType = typeof( BaseEntity );

	protected override IEnumerable<object> GetDropdownValues()
	{
		var types = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany( a =>
			{
				try { return a.GetTypes(); }
				catch ( ReflectionTypeLoadException e ) { return e.Types.Where( t => t is not null )!; }
			} )
			.Where( t => t is not null && t.IsClass && !t.IsAbstract && _baseType.IsAssignableFrom( t ) )
			.OrderBy( t => t.Namespace )
			.ThenBy( t => t.Name );

		foreach ( var type in types )
		{
			var displayName = string.IsNullOrEmpty( type.Namespace )
				? type.Name
				: $"{type.Namespace.Replace( "HL2K.Entities.", "" )}/{type.Name}";

			yield return new Entry
			{
				Label = displayName,
				Value = type
			};
		}
	}
}

/// <summary>
/// Custom control widget that uses EntityDropdown when [EntitySelector] is present.
/// </summary>
[CustomEditor( typeof( object ), WithAllAttributes = new[] { typeof( EntitySelectorAttribute ) } )]
public class EntitySelectorCW : ControlWidget
{
	public override bool SupportsMultiEdit => true;

	public EntitySelectorCW( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;

		var dropdown = new EntityDropdown( property );
		Layout.Add( dropdown );
	}
}
#endif
