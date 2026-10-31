namespace Core.Editor;

using System;
using System.Collections.Generic;

/// <summary>
/// Type discovery shared by browsing, pickers and schema validation. 
/// Invalidated when the tool opens or hotloads while open.
/// </summary>
internal static class ResourceCatalog
{
	private static ResourceEditorType[] _resources;
	private static Type[] _properties;
	internal static int Revision { get; private set; }
	private static readonly Dictionary<string, ResourceEditorType> ByExtension = new( StringComparer.OrdinalIgnoreCase );
	private static readonly Dictionary<string, Type> ByName = new( StringComparer.Ordinal );
	private static readonly Dictionary<string, Type> Bases = new( StringComparer.Ordinal );

	/// <summary>
	/// Clears cached types so the next lookup rebuilds them from the current TypeLibrary.
	/// </summary>
	internal static void Invalidate()
	{
		Revision++;
		ResourceValues.Invalidate();
		_resources = null;
		_properties = null;
	}

	/// <summary>
	/// Allowed resource types used by the outliner and resource picker.
	/// </summary>
	internal static ResourceEditorType[] Resources
	{
		get
		{
			if ( _resources is not null )
				return _resources;

			var resources = new List<ResourceEditorType>();
			ByExtension.Clear();
			Bases.Clear();
			
			foreach ( var asset in AssetType.All )
			{
				var type = asset.ResourceType;
				if ( !asset.IsGameResource || type is null || !typeof( GameResource ).IsAssignableFrom( type ) )
					continue;

				Bases[type.FullName] = type;
				if ( type.IsAbstract || !ResourceAccess.Allows( type ) )
					continue;

				var icon = DisplayInfo.ForType( type ).Icon;
				var definition = new ResourceEditorType( asset.FriendlyName, asset.FileExtension, string.IsNullOrWhiteSpace( icon ) ? "description" : icon,
					() => ResourceEditor.CreateDefault( asset ), type, asset );
				resources.Add( definition );
				ByExtension[definition.Extension] = definition;
			}

			resources.Sort( ( left, right ) => string.Compare( left.Name, right.Name, StringComparison.CurrentCulture ) );
			return _resources = resources.ToArray();
		}
	}

	/// <summary>
	/// Finds an allowed resource type by extension. Returns null when it is not available.
	/// </summary>
	internal static ResourceEditorType FindResource( string extension )
	{
		_ = Resources;
		return extension is not null && ByExtension.TryGetValue( extension, out var type ) ? type : null;
	}

	/// <summary>
	/// Resolves a registered base class, including abstract resource types.
	/// </summary>
	internal static Type FindBase( string name )
	{
		if ( name == typeof( GameResource ).FullName )
			return typeof( GameResource );

		_ = Resources;
		return name is not null && Bases.TryGetValue( name, out var type ) ? type : null;
	}

	/// <summary>
	/// Supported property types shared by the type picker and code generation.
	/// </summary>
	internal static Type[] Properties
	{
		get
		{
			if ( _properties is not null )
				return _properties;

			// Always expected to be around
			var types = new HashSet<Type>
			{
				typeof( string ), 
				typeof( bool ), 
				typeof( int ), 
				typeof( float ), 
				typeof( double ), 
				typeof( long ),
				typeof( Vector2 ), 
				typeof( Vector3 ), 
				typeof( Vector4 ), 
				typeof( Color ), 
				typeof( Angles ), 
				typeof( Rotation ),
				typeof( Transform ),
				typeof( Curve ), 
				typeof( Model ), 
				typeof( Texture ), 
				typeof( Material ), 
				typeof( SoundEvent )
			};
			
			foreach ( var descriptor in Game.TypeLibrary.GetTypes() )
			{
				var type = descriptor.TargetType;
				if ( type.IsVisible && !type.ContainsGenericParameters && !type.IsByRefLike && (type.IsEnum || typeof( Resource ).IsAssignableFrom( type )
				|| (type.IsDefined( typeof( SerializableAttribute ), false ) && !typeof( Component ).IsAssignableFrom( type ) 
				&& type != typeof( GameObject ) && type != typeof( Scene ) && !type.IsAbstract)) )
					types.Add( type );
			}
			
			var sorted = new List<Type>( types );
			sorted.Sort( ( left, right ) => string.Compare( left.FullName, right.FullName, StringComparison.CurrentCulture ) );
			ByName.Clear();

			foreach ( var type in sorted )
			{
				ByName[type.FullName] = type;
			}

			return _properties = [.. sorted];
		}
	}

	/// <summary>
	/// Resolves a supported property type by it's full name.
	/// </summary>
	internal static Type FindProperty( string name )
	{
		_ = Properties;
		return name is not null && ByName.TryGetValue( name, out var type ) ? type : null;
	}
}
