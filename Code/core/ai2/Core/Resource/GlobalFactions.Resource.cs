namespace Core.AI;

/// <summary>
/// Defines the usable global factions.
/// </summary>
[AssetType( Name = "Global Faction Definition", Extension = "gfac", Category = "NPC" )]
public class GlobalFactions : GameResource
{
	/// <summary>
	/// This defines a faction that is used in the world. 
	/// The KVP is a string holding the faction name, and then a boolean 
	/// determining if the faction should 
	/// have child relationships to be setup in the relationship matrix.
	/// Leave as false for default (No child relationships to be defined)
	/// </summary>
	[Category( "Global Definitions" )] public Dictionary<string, bool> FactionSet { get; set; }
}
