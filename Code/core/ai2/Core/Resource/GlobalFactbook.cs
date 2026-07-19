namespace Core.AI;

public struct WorldFact
{
	public WorldFact( string name, bool value ) { Name = name; Value = value; }

	/// <summary>
	/// Name of the fact.
	/// </summary>
	[Property, AIFactSelector] public string Name { get; set; }
	/// <summary>
	/// Boolean state of the fact.
	/// </summary>
	[Property] public bool Value { get; set; }

}

/// <summary>
/// Fact registry!
/// </summary>
public class AIFacts : GameObjectSystem
{
	public AIFacts( Scene scene ) : base( scene )
	{
	}

	// Combat
	public const string HasEnemy = "hasEnemy";
	public const string ThreatEliminated = "threatEliminated";
	public const string EnemyVisible = "enemyVisible";
	public const string EnemyInRangeAttack1 = "enemyInRangeAttack1";
	public const string EnemyInRangeAttack2 = "enemyInRangeAttack2";
	public const string EnemyInMeleeAttack1 = "enemyInMeleeAttack1";
	public const string EnemyInMeleeAttack2 = "enemyInMeleeAttack2";
	public const string EnemyTooClose = "enemyTooClose";
	public const string EnemyThreatHigh = "enemyThreatHigh";
	public const string EnemyThreatLow = "enemyThreatLow";
	public const string Alert = "alert";
	public const string SearchingForEnemy = "searchingForEnemy";
	public const string HasEnemyLKP = "hasEnemyLKP";
	public const string EngageEnemy = "engageEnemy";

	// Health/Damage
	public const string LowHealth = "lowHealth";
	public const string CriticalHealth = "criticalHealth";
	public const string LightDamage = "lightDamage";
	public const string HeavyDamage = "heavyDamage";
	public const string EnemyHurt = "enemyHurt";
	public const string ShouldRetreat = "shouldRetreat";

	// Movement
	public const string InCover = "inCover";
	public const string CanRest = "canRest";
	public const string ShouldRest = "shouldRest";
	public const string IsRested = "Rested";

	// Squad
	public const string SquadCohesionOK = "squadCohesionOK";
	public const string SquadIsBroken = "squadIsBroken";
	public const string SquadLeaderAlive = "squadLeaderAlive";
	public const string SquadHasEnemyContact = "squadHasEnemyContact";
	public const string SquadAsleep = "squadAsleep";
	public const string IsSquadLeader = "isSquadLeader";
	public const string LeaderDistanceOk = "leaderDistanceOk";
	public const string WithPack = "withPack";

	// Houndeye specific
	public const string Bored = "bored";
	public const string IsBored = "isBored";
	public const string VeryBored = "veryBored";
	public const string ChaseFriend = "chaseFriend";
	public const string FleeFriend = "fleeFriend";
	public const string BeingCommunicatedWith = "beingCommunicatedWith";
	public const string StartedConversation = "startedConversation";
	public const string CanTalk = "canTalk";
	public const string Encircling = "encircling";

	// Attack Cooldowns
	public const string InRange1Cooldown = "inRange1Cooldown";
	public const string InRange2Cooldown = "inRange2Cooldown";
	public const string InMelee1Cooldown = "inMelee1Cooldown";
	public const string InMelee2Cooldown = "inMelee2Cooldown";

	// Sensing
	public const string HeardSound = "heardSound";
	public const string HeardEnemySound = "heardEnemySound";
	public const string HeardAllySound = "heardAllySound";
	public const string HeardPhysicsSound = "heardPhysicsSound";
	public const string HeardPlayerSound = "heardPlayerSound";
	public const string SoundInvestigated = "soundInvestigated";
	public const string HeardSuspiciousSound = "heardSuspiciousSound";
	public const string LowPain = "lowPain";
	public const string MediumPain = "mediumPain";
	public const string HighPain = "highPain";
	public const string FriendDied = "friendDead";
	public const string TouchingEnemy = "touchingEnemy";
	public const string TouchingPlayer = "touchingPlayer";
	public const string TouchingFriend = "touchingFriend";
	public const string EnemyIsPlayer = "enemyIsPlayer";
	public const string EnemyIsNPC = "enemyIsNPC";

	public const string ScentDetected = "scentDetected";
	public const string ScentInvestigated = "scentInvestigated";
#if IGNIS || STANDALONE
	public static IEnumerable<string> All()
	{
		return typeof( AIFacts )
			.GetFields( System.Reflection.BindingFlags.Public |
						System.Reflection.BindingFlags.Static |
						System.Reflection.BindingFlags.FlattenHierarchy )
			.Where( f => f.IsLiteral && f.FieldType == typeof( string ) )
			.Select( f => (string)f.GetValue( null ) );
	}
#endif
}
