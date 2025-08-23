using Sandbox;
public class AISquad : Component
{
	public BaseNpc Npc;

	public const int MAX_SQUAD_MEMBERS = 16;
	public const int MAX_SQUAD_DATA_SLOTS = 4;

	protected override void OnStart()
	{
		Npc.Brain.OnThink += Think;
		base.OnStart();
	}
	void Think()
	{
		if ( Npc == null || !Npc.Agent.IsValid )
			return;
	}

	protected override void OnFixedUpdate()
	{

	}



}
