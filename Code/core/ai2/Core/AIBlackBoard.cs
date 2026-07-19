namespace Core.AI;

public sealed class CombatMemory
{
	public BaseEntity Enemy;
	public Vector3 LastKnownPosition;
	public Vector3 LastKnownVelocity;
	public float LastSeenTime;
	public float Confidence; // 0..1

	public bool IsValid =>
		Enemy is not null &&
		Enemy.IsValid() &&
		Confidence > 0f;

	public float TimeSinceSeen => Time.Now - LastSeenTime;
}

public class AIBlackBoard : AIModule
{
	public BaseEntity activeEnemy = null;
	public float lastSeenEnemyTime;
	public BasePlayer playerReference = null;
	public PlayerTargetSignature playerTargetReference = null;
	public HintNode activeHintNode; // the node in which we want to move to. (cover, special actions, etc )

	public Vector3? _currentMovePos = new Vector3( 0, 0, 0 ); // where we should go
	public List<HintNode> nodePool;
	public CombatMemory combatMemory = null;
	public float enemyDistance; // how close our active enemy is
	public TimeSince? lastCombatSoundHeard;

	public override void Init( AIController controller )
	{
		Owner = controller;
		nodePool = [];
	}

	public HintNode ClaimCoverNode( AIController user )
	{
		if ( nodePool.Count == 0 )
			return null;

		var node = GetAvailableNode( user );
		if ( !node.IsValid() )
			return null;

		node.SetActiveHintNode( user );
		return node;
	}

	public bool HasAnyCoverNode()
	{
		return nodePool is not null && nodePool.Count > 0;
	}


	public HintNode GetAvailableNode( AIController user )
	{
		List<HintNode> potentialHints = [];

		foreach ( var node in nodePool )
		{
			if ( node.CanUseThisNode( user ) )
				potentialHints.Add( node );
			//	else
			//	Log.Info( $"Node {node} not available!" );
		}

		return potentialHints.OrderBy( n => (n.WorldPosition - Owner.WorldPosition).Length ).FirstOrDefault();
	}
}
