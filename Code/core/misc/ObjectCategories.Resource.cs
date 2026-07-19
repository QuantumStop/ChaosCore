namespace Core;

using System;
using Sandbox;

[AssetType( Name = "Object Category List", Extension = "objcat" )]
public class ObjectTypeCategories : GameResource
{
	[Property, InlineEditor, WideMode] public List<ObjectCategoryListing> Categories { get; set; }

	public static string IconName = "package";
	protected override Bitmap CreateAssetTypeIcon( int width, int height )
		=> CreateSimpleAssetTypeIcon( IconName, width, height, "#2E2E2EFF", "#FFD966FF" );


	protected override void PostReload()
	{
		base.PostReload();
		string fileName = ResourceName ?? "unknown";

		if ( Categories is null ) return;

		foreach ( var category in Categories )
		{
			category.EnsureId( fileName );
		}
	}
}


[Serializable]
public class ObjectCategoryListing
{
	[Property] public string EntryName { get; set; }
	[Property] public string EntryIcon { get; set; }

	[Property, Title( "Generated ID" ), ReadOnly] public string Id { get; private set; }

	[Property] public int OrderIndex { get; set; }

	/// <summary>
	/// Ensures this category has a stable ID. Returns true if the ID was set or updated.
	/// </summary>
	public string EnsureId( string resourceName )
	{
		if ( string.IsNullOrWhiteSpace( Id ) )
			Id = GenerateDeterministicId( resourceName, EntryName );
		return Id;
	}


	/// <summary>
	/// Returns true if this category has a valid ID, for safatey checks purposes
	/// </summary>
	public bool HasValidId => !string.IsNullOrWhiteSpace( Id );

	private static string GenerateDeterministicId( string resourceName, string categoryName )
	{
		string input = $"{resourceName}:{categoryName}";
		uint hash = Fnv1aHash( input );

		return $"objcat_{hash:X8}";
	}

	/// <summary>
	/// FNV-1a 32-bit hash
	/// </summary>
	private static uint Fnv1aHash( string text )
	{
		const uint fnvPrime = 0x01000193;
		const uint fnvOffsetBasis = 0x811C9DC5;
		uint hash = fnvOffsetBasis;

		foreach ( char c in text )
		{
			hash ^= c;
			hash *= fnvPrime;
		}
		return hash;
	}

	public override string ToString() => $"{EntryName ?? "Unnamed"} ({Id ?? "no-id"})";

}


[AttributeUsage( AttributeTargets.Property )]
public class CategorySelectorAttribute : Attribute { }
