using System;

namespace Core.AI;

public class HintNode : BaseEntity
{
	protected override string GetEditorVis() => "models/editor/ground_node_hint.vmdl";
	/// <summary>
	/// This could technically be data driven using resources
	/// </summary>
	public enum AI_Hint
	{
		HINT_NONE = 0,
		HINT_GENERIC_COVER,
		HINT_XEN_FOOD_BURIED,
		HINT_XEN_FOOD
	};

	public enum AIHintContext
	{
		CLOSEST_HINT,
		RANDOM_HINT,
		FAR_HINT, // this is kinda weird an arbitrary, may not be needed
	}

	[Property] public AI_Hint HintType { get; set; }

	/// <summary>
	/// How far an npc should be from this node before it considers it theirs
	/// </summary>
	[Property] public float DistanceThreshold { get; set; } = 15f;

	/// <summary>
	/// Defines the radius in which an npc must be to use this hint. Leave at -1 for infinite.
	/// </summary>
	[Property] public float UsableRadius { get; set; } = -1f;

	public AIController CurrentUser { get; set; }
	public bool NodeLocked { get; set; } = false;

	[Property] public bool TimeoutAfterUse { get; set; } = false;
	[Property, ShowIf( nameof( TimeoutAfterUse ), true )] public float TimeoutLength { get; set; } = 5f;

	public float _timeoutStartedTime;

	public bool CanUseThisNode( AIController potentialUser )
	{
		if ( NodeLocked )
			return false;

		if ( TimeoutAfterUse && WorldTime.Now >= (_timeoutStartedTime + TimeoutLength) )
			return false;
		var inRadius = UsableRadius > 0 && Vector3.DistanceBetween( WorldPosition, potentialUser.WorldPosition ) >= UsableRadius;
		Color lineColor = inRadius ? Color.Green : Color.Red;
		Gizmo.Draw.Color = lineColor;
		Gizmo.Draw.Line( WorldPosition, potentialUser.WorldPosition );


		if ( inRadius )
		{
			Log.Info( "Hint node has a radius, which we arent in!" );
			return false;

		}

		return true;
	}


	public void SetActiveHintNode( AIController user )
	{
		CurrentUser = user;
		NodeLocked = true;

		user.ActiveHintNode = this;

		if ( TimeoutAfterUse )
			_timeoutStartedTime = WorldTime.Now;
	}

	public void ClearActiveHintNode( AIController user )
	{
		CurrentUser = null;
		NodeLocked = false;

		user.ActiveHintNode = null;
	}

	protected override void EntityDefaultGizmo( string editorVis, bool isModel )
	{
		if ( GetEditorVis() is null ) return;

		Model vmdl = Model.Load( GetEditorVis() );
		Gizmo.Hitbox.Model( vmdl );



		if ( Gizmo.IsSelected )
		{
			Gizmo.Draw.Color = Color.Yellow;
			Gizmo.Draw.LineBBox( vmdl.Bounds );

			if ( UsableRadius > 0 )
				Gizmo.Draw.LineSphere( Vector3.Zero, UsableRadius ); // i always forget not to use worldposition here
		}
		else
		{
			if ( Gizmo.IsHovered )
			{
				Gizmo.Draw.Color = Color.White.WithAlpha( (((float)Math.Sin( WorldTime.Now * 20f )) * 0.3f) + 0.7f );
				Gizmo.Draw.LineBBox( vmdl.Bounds );
			}
		}
		base.EntityDefaultGizmo( editorVis, isModel );
	}




}
