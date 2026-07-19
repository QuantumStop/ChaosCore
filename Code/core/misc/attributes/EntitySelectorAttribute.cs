namespace Core;

using System;

/// <summary>
/// Marks a property to use the BaseUtilBoxMechanic dropdown in the editor.
/// </summary>
[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
public class EntitySelectorAttribute : Attribute
{
	public Type TargetType { get; set; } = typeof( BaseEntity );
}
