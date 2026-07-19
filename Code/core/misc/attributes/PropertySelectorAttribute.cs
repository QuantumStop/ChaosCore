namespace Core;

using System;

/// <summary>
/// Marks a property to use the ComponentPropertyDropdown in the editor.
/// Allows specifying the type to filter properties from.
/// </summary>
[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
public class TargetPropertySelectorAttribute : Attribute
{
	/// <summary>
	/// The type to inspect for assignable properties. Can be null and resolved dynamically.
	/// </summary>
	public Type TargetType { get; set; }

	public TargetPropertySelectorAttribute() { }

	public TargetPropertySelectorAttribute( Type fromType )
	{
		TargetType = fromType;
	}
}
