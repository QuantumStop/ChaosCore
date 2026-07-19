using Core.AI;
using System;
using System.Collections.Generic;
using System.Text;
using static Core.AI.NpcRelations;

namespace Core;

public class Bullseye : BaseEntity
{
	[Property] public int Health;
	[Property] public bool DestroyOnDeath { get; set; } = true;

	public bool IsActive => GameObject.Active && Health > 0;


	protected override void OnStart()
	{
		// Register as a targetable entity
		var sig = Components.GetOrCreate<BullseyeTargetSignature>();
	}

	public void OnDamage( in DamageInfo dmginfo )
	{
		if ( (Health - dmginfo.Damage.CeilToInt()) <= 0 )
			EventKilled();

		Health -= dmginfo.Damage.CeilToInt();
	}

	public void EventKilled()
	{
		OnKilled?.Invoke( this );

		if ( DestroyOnDeath )
			GameObject.Destroy();
		else
			GameObject.Enabled = false; // disable but keep in scene for I/O
	}
}
