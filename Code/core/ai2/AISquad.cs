namespace Core.AI;

public sealed class AISquadManager( Scene scene ) : GameObjectSystem( scene )
{
	private readonly Dictionary<string, AISquad> squads = new();

	public AISquad GetOrCreateSquad( string name, AIController requester )
	{
		if ( string.IsNullOrEmpty( name ) )
			return null;

		if ( !squads.TryGetValue( name, out var squad ) )
		{
			squad = new AISquad( name );
			squads.Add( name, squad );

			Log.Info( $"[AISquadManager] Created squad '{name}'" );
		}

		squad.AddMember( requester );
		return squad;
	}

	public void RemoveEmptySquad( AISquad squad )
	{
		if ( squad is not null && squad.MemberCount == 0 )
			squads.Remove( squad.Name );
	}
}
public class AISquad
{
	public string Name { get; }
	public AIController Leader { get; private set; }

	public readonly List<AIController> members = [];

	public int MemberCount => members.Count;

	public bool chooseNewSquadLeaderOnDeath = true; // TODO: move this into AI definition. This can be left up to AI through an action

	public AISquad( string name ) => Name = name;

	public void AddMember( AIController controller )
	{
		if ( !controller.IsValid() || members.Contains( controller ) )
			return;

		members.Add( controller );
		controller.AISquad = this;

		if ( !Leader.IsValid() )
		{
			Leader = controller;
			slotAssignments[controller] = 0;
		}
		else
		{
			slotAssignments[controller] = nextSlot++;
		}
	}

	public bool TryGetMemberEnemy( AIController member ) { return member.Blackboard.activeEnemy.IsValid; }

	public void RemoveMember( AIController controller )
	{
		if ( !controller.IsValid() ) return;
		if ( !members.Remove( controller ) ) return;

		slotAssignments.Remove( controller );
		controller.AISquad = null;

		if ( controller == Leader && chooseNewSquadLeaderOnDeath )
		{
			Leader = members.FirstOrDefault();
			if ( Leader != null ) slotAssignments[Leader] = 0;
		}
	}

	public void NotifySquadMemberDead()
	{
		if ( MemberCount == 1 ) return; // only one in squad, no one to report to
		foreach ( var ai in members )
		{
			ai.FriendDead();

		}
	}
	private readonly Dictionary<AIController, int> slotAssignments = new();
	private int nextSlot = 1;
	public IEnumerable<AIController> MembersBySlot()
	   => members.OrderBy( m => slotAssignments.GetValueOrDefault( m, 999 ) );

}
