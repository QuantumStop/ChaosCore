namespace Core.AI;

/// <summary>
/// Defines a desired list of facts an NPC should aim for.
/// </summary>
[AssetType( Name = "AI Goal Definition", Extension = "aigoal", Category = "NPC" )]
public class GoalState : GameResource
{
	[Category( "Atoms" )] public GoalAtom Goal { get; set; }
}

public class GoalAtom
{

	[Category( "Parameters" ), AIFactSelector] public string goalName { get; set; }
	[Category( "Parameters" )] public bool goalState { get; set; }
	[Category( "Parameters" )] public float goalWeight { get; set; }

}
