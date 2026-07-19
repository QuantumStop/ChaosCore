using System;

namespace Core;

[AttributeUsage( AttributeTargets.Property )]
public class ResourceSelectorAttribute : Attribute
{
	public Type ResourceType { get; }
	public string TitleMember { get; }
	public string ModelMember { get; }

	/// <summary>
	/// Define what resource type to display, and optionally
	/// which members represent the display name and model.
	/// </summary>
	public ResourceSelectorAttribute( Type resourceType, string titleMember = "Title", string modelMember = "Model" )
	{
		ResourceType = resourceType;
		TitleMember = titleMember;
		ModelMember = modelMember;
	}
}
