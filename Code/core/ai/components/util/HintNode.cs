using Sandbox;

public class HintNode : BaseEntity
{
	public enum AI_Hint 
	{
	HINT_NONE = 0,
	HINT_GENERIC_COVER,
	HINT_XEN_FOOD_BURIED,
	HINT_XEN_FOOD,

	};

	[Property] public AI_Hint hintType { get; set; }
	
}
