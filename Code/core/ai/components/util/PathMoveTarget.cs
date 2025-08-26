namespace Core;

public class PathMoveTarget : BaseEntity
{
	/// <summary>
	/// The next path corner to move to
	/// </summary>
	[Property, MakeDirty] public PathTrack nextMoveTarget { get; private set; }
	public List<GameObject> pathPoints { get; private set; } = new List<GameObject>();

	private List<GameObject> pathsObj { get; set; } = new();

	/// <summary>
	/// Time to wait at this path corner
	/// </summary>
	[Property] public float waitHereFor { get; private set; }

	/// <summary>
	/// Is this path corner currently being used?
	/// </summary>
	[Property, ReadOnly] public bool isActive = false;

	/// <summary>
	/// Current BaseNPC User
	/// </summary>
	[Property, ReadOnly] public BaseNpc currentUser { get; set; }
	
	/// <summary>
	/// Has our current BaseNPC User reached us?
	/// </summary>
	[Property, ReadOnly] public bool hasUserReached { get; set; } = false;

	//	protected override string GetEditorVis() { return null; }

	public bool HasNextMoveTarget() { return nextMoveTarget != null; }
	public bool HasActiveUser() { return currentUser._currentPathCorner.Active; } // FIXME: supposed to be currentUser._currentPathCorner == this
	public void OnReachedPathTarget() // Called in the task to be handled here
	{
		hasUserReached = true;

		// Move to the next path target if one exists
		if ( HasNextMoveTarget() )
		{

		}

		// Mark this corner as not active
		isActive = false;
	}

	//	private int currentPathIndex = 0;

	protected override void OnUpdate()
	{

		if ( currentUser != null )
		{
			// If the NPC is at this path corner, make them move toward it
			if ( HasActiveUser() )
			{
				isActive = true;

				// If the NPC has reached the move target, move them to the corner's position
				if ( currentUser.OnReachedMoveTarget() )
				{
					// Move NPC to this corner's position
					currentUser.DoMovement( WorldPosition, BaseNpc.GoalType.GOALTYPE_PATHCORNER );
				}
			}
		}
	}
}
