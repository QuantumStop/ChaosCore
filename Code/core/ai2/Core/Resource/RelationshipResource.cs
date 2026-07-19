namespace Core.AI;

/// <summary>
/// Defines the relationships between factions.
/// </summary>
[AssetType( Name = "Relationship Definition File", Extension = "rdef", Category = "NPC" )]

public class RelationshipResource : GameResource
{
	public enum RelationshipType
	{
		REL_NEUTRAL = 0,
		REL_LIKE,
		REL_HATE,
		REL_FEAR
	}

	public RelationshipType relationshipType;


}
