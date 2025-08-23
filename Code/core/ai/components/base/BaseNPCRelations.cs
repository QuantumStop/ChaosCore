using System.Text.Json.Nodes;
using System;
namespace Core;

public class NpcRelations : Component
{
	public enum Relation
	{
		NEUTRAL,
		IGNORE,
		HATE,
		ALLY,
	}
	public enum TargetType
	{
		PLAYER,
		NPC,
		INTERACTIVE
	}

	[Property] public bool DrawDebug { get; set; }
	[Property] public string Faction { get; set; }
	[Property] public TargetType Type { get; set; } = TargetType.NPC;

	public BaseEntity Owner { get; set; }
	public Vector3 Velocity { get; set; }

//	not strictly relation related but targeting needs to know it
	public static Dictionary<string, Dictionary<string, Relation>> Relations { get; private set; }

	protected override void OnEnabled()
	{
//		load faction relations
		Relations = new();
		var file = JsonNode.Parse( FileSystem.Mounted.ReadAllText( "scripts\\ai_faction_relations.fac" ) ).AsObject();
		if ( !FileSystem.Mounted.FileExists( "scripts\\ai_faction_relations.fac" ) )
		{
			Log.Warning( "Faction file not found!" );
			return;
		}

		Log.Info( "Parsed Faction Relations:" );
		foreach ( var f in Relations )
		{
			Log.Info( $"Faction: {f.Key}" );
			foreach ( var r in f.Value )
				Log.Info( $"  -> {r.Key}: {r.Value}" );
		}

		foreach ( var f in Relations )
		{
			Log.Info( $"Faction: {f.Key}" );
			foreach ( var rel in f.Value )
				Log.Info( $"  {rel.Key} = {rel.Value}" );
		}

		foreach ( var faction in file )
		{
			Dictionary<string, Relation> relationset = new();
			foreach ( var relation in faction.Value.AsObject() )
				relationset.Add( relation.Key, (Relation)Enum.Parse( typeof( Relation ), relation.Value.ToString() ) );
			Relations.Add( faction.Key, relationset );
		}

		Dictionary<string, Relation> newfactionchunk = new();
		foreach ( var faction in Relations )
			newfactionchunk.Add( faction.Key, Relation.NEUTRAL );

		Relations.Add( "NEUTRAL", newfactionchunk );

		foreach ( var faction in Relations )
			Relations[faction.Key].Add( "NEUTRAL", Relation.NEUTRAL );

		base.OnEnabled();
	}

	public Relation GetDisposition( BaseEntity target )
	{
		if ( string.IsNullOrEmpty( Faction ) )
			Faction = "NEUTRAL";

		var relcomp = target.Components.Get<NpcRelations>();
		if ( relcomp == null || string.IsNullOrEmpty( relcomp.Faction ) )
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

	protected override void OnUpdate()
	{
		
		if ( DrawDebug )
		{
			foreach ( var ent in Scene.Components.GetAll<NpcRelations>() )
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
				Gizmo.Draw.Arrow( WorldPosition, ent.WorldPosition + Vector3.Up * 10f, 4, 2 );
			}
		}
		base.OnUpdate();
	}
}
