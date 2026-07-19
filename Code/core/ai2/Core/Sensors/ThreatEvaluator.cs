namespace Core.AI;

using System;

public sealed class ThreatEvaluator
{
	public float ThreatScore { get; private set; }

	public bool ThreatHigh => ThreatScore >= 0.4f;
	public bool ThreatLow => ThreatScore <= 0.2f;

	public void Update( AIController agent )
	{
		var enemy = agent.Blackboard.activeEnemy;

		if ( !enemy.IsValid() || !enemy.IsValid )
		{
			agent.Blackboard.activeEnemy = null;
			ThreatScore = 0f;
			return;
		}

		float dist = agent.WorldPosition.Distance( enemy.WorldPosition );

		float healthRatio = 0f;
		if ( agent.Blackboard.activeEnemy is BasePlayer player )
		{
			healthRatio = player.Health / 100;
		}
		else if ( agent.Blackboard.activeEnemy is AIController AI )
		{
			healthRatio = AI.curHealth / 100;
		}

		float distanceFactor = 1f - Math.Clamp( dist / 600f, 0f, 1f );
		float healthFactor = 1f - healthRatio;

		ThreatScore =
			(distanceFactor * 0.5f) +
			(healthFactor * 0.5f);
	}
}
