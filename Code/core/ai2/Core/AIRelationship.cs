using System;
using System.Text.Json.Nodes;


using static Core.AI.RelationshipResource;
using static Core.AI.NpcRelations;

namespace Core.AI;
/*
class AIRelationship
{
	public RelationshipResource resource;
	public static Dictionary<string, Dictionary<string, RelationshipType>> Relations { get; private set; }
	public AIController Controller;
}*/

public abstract class TargetSignature : BaseEntity
{
	public abstract TargetType Type { get; }
	public virtual ScentEmitter scentEmitter { get; set; }

	public virtual Vector3 WorldPos
		=> Transform.World.Position;

	public virtual Vector3 Velocity
	{

		get
		{

			var rb = GameObject.GetComponent<Rigidbody>();
			return rb?.Velocity ?? Vector3.Zero;
		}
	}
}
public sealed class PlayerTargetSignature : TargetSignature
{
	public override TargetType Type
		=> TargetType.PLAYER;

	public bool shouldIgnore { get; set; } = false;

	public override ScentEmitter scentEmitter { get; set; }

	public override Vector3 Velocity
	{
		get
		{
			var controller = GameObject.GetComponent<XMovement.PlayerMovement>();
			if ( controller is not null )
				return controller.Velocity;

			var rb = GameObject.GetComponent<Rigidbody>();
			return rb?.Velocity ?? Vector3.Zero;
		}
	}
}
public sealed class BullseyeTargetSignature : TargetSignature
{
	public override TargetType Type
		=> TargetType.PLAYER;

	public bool shouldIgnore { get; set; } = false;

	public override Vector3 Velocity
	{
		get
		{
			var rb = GameObject.GetComponent<Rigidbody>();
			return rb?.Velocity ?? Vector3.Zero;
		}
	}
}


public partial class NpcRelations : TargetSignature
{
	[Property]
	public override NpcRelations.TargetType Type { get; }

	public override Vector3 Velocity
		=> Owner.Agent?.Velocity ?? Vector3.Zero;
	public enum Relation
	{
		NEUTRAL,
		IGNORE,
		HATE,
		ALLY,
		FEAR,
	}
	public enum TargetType
	{
		PLAYER,
		NPC,
		BULLSEYE,
		INTERACTIVE
	}

	[Property] public bool DrawDebug { get; set; } = true;
	public string Faction { get; set; }


	public AIController Owner { get; set; }


	//	not strictly relation related but targeting needs to know it
	public Dictionary<string, Dictionary<string, Relation>> Relations { get; private set; }
	public void Init()
	{
		var playerTarget = Owner.Blackboard.playerReference.AddComponent<PlayerTargetSignature>();
		var scentEmit = playerTarget.scentEmitter ?? new ScentEmitter();
		scentEmit.Position = playerTarget.WorldPosition;
		scentEmit.SourceEnt = playerTarget;
		scentEmit.Intensity = 1.0f; // maybe the player should.. smell different sometimes. like when exhausted? sweaty and gross
		scentEmit.Category = ScentCategory.Player;
		scentEmit.DecayRate = 0f;

		AIScentManager.Current.Register( scentEmit );


		// this whole system is primordial tbh
		Relations = [];
		if ( !FileSystem.Mounted.FileExists( "scripts\\ai_faction_relations.fac" ) )
		{
			Log.Warning( "Faction file not found!" );
			return;
		}

		var raw = FileSystem.Mounted.ReadAllText( "scripts\\ai_faction_relations.fac" );

		if ( string.IsNullOrWhiteSpace( raw ) )
		{
			Log.Warning( "Faction file is empty!" );
			return;
		}

		var file = JsonNode.Parse( raw ).AsObject();

		foreach ( var faction in file )
		{
			Dictionary<string, Relation> relationset = [];
			foreach ( var relation in faction.Value.AsObject() )
				relationset.Add( relation.Key, Enum.Parse<Relation>( relation.Value.ToString() ) );
			Relations.Add( faction.Key, relationset );
		}

		Dictionary<string, Relation> neutralChunk = [];
		foreach ( var faction in Relations )
			neutralChunk.Add( faction.Key, Relation.NEUTRAL );
		Relations.Add( "NEUTRAL", neutralChunk );

		foreach ( var faction in Relations )
			Relations[faction.Key].Add( "NEUTRAL", Relation.NEUTRAL );
	}
	public Relation GetDisposition( BaseEntity target )
	{
		if ( string.IsNullOrEmpty( Faction ) )
			Faction = "NEUTRAL";

		var relcomp = target.Components.Get<NpcRelations>();
		if ( !relcomp.IsValid() || string.IsNullOrEmpty( relcomp.Faction ) )
		{
			Log.Warning( $"EMPTY FACS" );
			return Relation.NEUTRAL;
		}


		if ( !Relations.TryGetValue( Faction, out var factionRelations ) )
		{
			Log.Warning( $"Faction '{Faction}' not found in Relations." );
			return Relation.NEUTRAL;
		}

		if ( !factionRelations.TryGetValue( relcomp.Faction, out var disposition ) )
		{
			Log.Warning( $"No relation defined between '{Faction}' and '{relcomp.Faction}'." );
			return Relation.NEUTRAL;
		}

		return disposition;
	}

	public void Tick()
	{

		if ( DrawDebug )
		{
			foreach ( var ent in Owner.Scene.Components.GetAll<NpcRelations>() )
			{
				if ( ent == this )
					continue;
				Gizmo.Draw.IgnoreDepth = true;
				switch ( GetDisposition( ent.Owner ) )
				{
					case Relation.NEUTRAL:
						Gizmo.Draw.Color = Color.Yellow;
						break;
					case Relation.IGNORE:
						Gizmo.Draw.Color = Color.Gray;
						break;
					case Relation.HATE:
						Gizmo.Draw.Color = Color.Red;
						break;
					case Relation.ALLY:
						Gizmo.Draw.Color = Color.Green;
						break;
				}
				Gizmo.Draw.Arrow( Owner.WorldPosition, ent.Owner.WorldPosition + Vector3.Up * 10f, 4, 2 );
			}
		}
	}
}
