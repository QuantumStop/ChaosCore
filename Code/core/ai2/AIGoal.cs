namespace Core.AI;

public class Goal
{
	public string Name { get; }
	public List<WorldFact> DesiredState { get; }
	public float Priority { get; set; }

	public Goal( string name, List<WorldFact> desiredState, float priority )
	{
		Name = name;
		DesiredState = desiredState;
		Priority = priority;
	}
}


