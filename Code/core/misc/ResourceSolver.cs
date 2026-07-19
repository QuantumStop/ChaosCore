#if IGNIS
namespace Core;

using Sandbox;
using System;
using System.Collections.Generic;
using System.Reflection;

[AssetType( Name = "Type Resource Resolver", Extension = "tres" )]
public class TypeResourceResolver : GameResource
{
	[Space( 12 )]

	[InfoBox( "To create entries correctly inside the object placer editor, this can be used to map the specific type class of an entity to a desired resource type.", tint: EditorTint.Green )]

	[Property, InlineEditor, WideMode] public List<TypeResourceMapping> Mappings { get; set; } = new();

	public Type GetResourceTypeFor( Type entityType )
	{
		if ( entityType is null ) return null;

		var t = entityType;
		while ( t is not null )
		{
			var mapping = Mappings.Find( m => m.EntityType == t );
			if ( mapping is not null ) return mapping.ResourceType; // could be null
			t = t.BaseType;
		}

		return null;
	}

	public bool HasResource( Type entityType ) => GetResourceTypeFor( entityType ) is not null;

	public List<GameResource> GetResourcesFor( Type entityType )
	{
		var resourceType = GetResourceTypeFor( entityType );
		if ( resourceType is null ) return [];

		// Call generic dynamically using reflection
		return ResourceLibraryHelper.GetAllDynamic( resourceType, recursive: true );
	}
}


/// <summary>
/// EntityType to ResourceType
/// </summary>
[Serializable]
public class TypeResourceMapping
{
	[Property] public Type EntityType { get; set; }
	[Property] public Type ResourceType { get; set; }

	// Specify where to pull the Type from
	[Property, InlineEditor]
	public TargetPropertyInfo TargetProperty { get; set; } = new( "EntityType" );
}

[Serializable]
public class TargetPropertyInfo
{
	// Name of the property on the parent object that provides the Type
	[Property, Hide]
	public string SourceTypeProperty { get; set; }

	[Property, ReadOnly]
	public string PropertyName { get; set; }

	[Property, Hide]
	public Type PropertyType { get; set; }

	public TargetPropertyInfo() { }

	public TargetPropertyInfo( string sourceTypeProperty )
	{
		SourceTypeProperty = sourceTypeProperty;
	}
}

public static class ResourceLibraryHelper
{
	public static List<GameResource> GetAllDynamic( Type resourceType, string folder = null, bool recursive = true )
	{
		if ( resourceType is null )
			return [];

		// Find the generic GetAll<T>(string, bool) method
		var method = typeof( ResourceLibrary )
			.GetMethods( BindingFlags.Public | BindingFlags.Static )
			.FirstOrDefault( m =>
			{
				if ( !m.IsGenericMethod ) return false;
				if ( m.Name != "GetAll" ) return false;
				var parameters = m.GetParameters();
				return parameters.Length == 2
					   && parameters[0].ParameterType == typeof( string )
					   && parameters[1].ParameterType == typeof( bool );
			} ) ?? throw new Exception( "Cannot find ResourceLibrary.GetAll<T>(string,bool)" );

		var safePath = string.IsNullOrEmpty( folder ) ? "" : folder;

		var genericMethod = method.MakeGenericMethod( resourceType );
		var result = genericMethod.Invoke( null, [safePath, recursive] );

		return (result as IEnumerable<GameResource>)?.ToList() ?? [];
	}
}
#endif
