using System;
using System.Collections.Generic;
using System.Text;

namespace Core.AI;

public interface IAIEvents : ISceneEvent<IAIEvents>
{
	/// <summary>
	/// This NPC has died
	/// </summary>
	/// <param name="npc">Player in question</param>
	void OnDeath( AIController npc ) { }
	/// <summary>
	/// This npc has spawned
	/// </summary>
	/// <param name="npc">NPC in question</param>
	void OnSpawn( AIController npc ) { }
	/// <summary>
	/// This player has taken damage
	/// </summary>
	/// <param name="npc">NPC in question</param>
	/// <param name="damageInfo">Damage in question</param>
	void OnTookDamage( AIController npc, DamageInfo damageInfo ) { }
}
