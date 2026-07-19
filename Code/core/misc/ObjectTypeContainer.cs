namespace Core;

using System;
using Sandbox;

/// <summary>
/// Container that groups and manages a specific type of object entries.
/// Automatically finds and uses a solver to generate default data.
/// </summary>
[AssetType( Name = "Object Container", Extension = "objcon" )]
public class ObjectTypeContainer : GameResource
{
	[Space( 12 )]
	[Property, CategorySelector, WideMode] public ObjectCategoryListing SelectedCategory { get; set; }

	[Space( 24 )]

	[Property, InlineEditor, WideMode] public List<ObjectEntry> Entries { get; set; } = new();

	[Space( 12 )]

	[Property] public int OrderIndex { get; set; }

	public const string IconName = "package";
	protected override Bitmap CreateAssetTypeIcon( int width, int height )
		=> CreateSimpleAssetTypeIcon( IconName, width, height, "#2E2E2EFF", "#FFD966FF" );
}


public class ObjectEntry
{
	[Property] public string Name { get; set; }
	[Property] public int OrderIndex { get; set; }

	// Optional resource associated with this entry
	[Property, InlineEditor] public GameResource Resource { get; set; }
	[Property, InlineEditor] public Type EntityClass { get; set; }


	public override string ToString() => Name ?? $"Entry {OrderIndex}";
}
