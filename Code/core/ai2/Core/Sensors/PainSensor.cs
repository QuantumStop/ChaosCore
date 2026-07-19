namespace Core.AI;

public class PainSensor : BaseSensor<PainPacket>
{
	public PainSensor( AIController agent )
			: base( agent )
	{
	}

	float PainScore; // 0 -> 1 

	bool PainIsLow; // look around, maybe move a bit
	bool PainIsMedium; // stop what we're doing, run around a bit
	bool PainIsHigh; // take cover

	public float PainTime; // how long we feel pain and thus will react for

	public bool ShouldUpdateWorldState;

	TimeSince TimeSinceLastInjury;

	public float DeterminePainScore( DamageInfo dmgInfo )
	{
		var damageAmount = dmgInfo.Damage;
		var currentHealth = Agent.curHealth;
		var maxHealth = Agent.maxHealth;

		// Higher damage + lower health = more pain
		float healthRatio = 1f - (currentHealth / maxHealth); // 0 = full health, 1 = near death
		float painScore = (damageAmount / maxHealth) + healthRatio * 0.5f; // weight health state

		float painFinal = MathX.Clamp( painScore, 0, 1 );

		return painFinal;
	}

	public void InflictPain( DamageInfo dmgInfo )
	{

		PainScore = DeterminePainScore( dmgInfo );

		if ( PainScore <= 0.3 )
		{
			PainIsLow = true;
		}
		else if ( PainScore <= 0.6 )
		{
			PainIsMedium = true;
		}
		else
		{
			PainIsHigh = true;
		}

		TimeSinceLastInjury = 0;
		ShouldUpdateWorldState = true;
	}

	void CollectPacketData()
	{
		if ( TimeSinceLastInjury > (Time.Now + PainTime) )
		{
			ShouldUpdateWorldState = false;
			return;
		}

		PainTime = PainScore * 2 * (1 + PainScore);

		Packet.painTime = PainTime;
		Packet.painScore = PainScore;

		Packet.painIsHigh = PainIsHigh && TimeSinceLastInjury < PainTime; // fact
		Packet.painIsMedium = PainIsMedium && TimeSinceLastInjury < PainTime; // fact
		Packet.painIsLow = PainIsLow && TimeSinceLastInjury < PainTime; // fact

		Packet.shouldUpdateWorldState = ShouldUpdateWorldState;
		Packet.timeSinceLastInjury = TimeSinceLastInjury;
		//Log.Info($"Paintime: {PainTime} | TimeSinceLastInjury: {TimeSinceLastInjury}");

		/*Agent.WorldState.Set( AIFacts.HighPain, painHigh );
		Agent.WorldState.Set( AIFacts.MediumPain, painMedium );
		Agent.WorldState.Set( AIFacts.LowPain, painLow );*/
	}

	public PainPacket GetOutputPacketData()
	{
		return Packet;
	}


	public override void UpdatePacket()
	{
		if ( ShouldUpdateWorldState )
			CollectPacketData();
	}

}
