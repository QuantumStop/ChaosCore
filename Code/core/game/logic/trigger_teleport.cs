using System;
using Sandbox.Diagnostics;

namespace Core;

[Description( "A trigger that teleports entities that touch its volume." )]
[Icon( "wifi_tethering" )]
public class trigger_teleport : BaseEntity, Component.ITriggerListener
{
	#region SpawnFlags

	[Group( "SpawnFlags" )]
	[Property, Title( "Clients" )] public bool b_Clients { get; set; } = true;

	[Group( "SpawnFlags" )]
	[Property, Title( "NPCs" )] public bool b_Npcs { get; set; } = false;

	[Group( "SpawnFlags" )]
	[Property, Title( "Pushables" )] public bool b_Pushables { get; set; } = false;

	[Group( "SpawnFlags" )]
	[Property, Title( "Physics Objects" )] public bool b_PhysicsObjects { get; set; } = false;

	[Group( "SpawnFlags" )]
	[Property, Title( "Only player ally NPCs" )] public bool b_PlayerAllyNPCs { get; set; } = false;

	[Group( "SpawnFlags" )]
	[Property, Title( "Only clients in vehicles" )] public bool b_ClientsInVehicles { get; set; } = false;

	[Group( "SpawnFlags" )]
	[Property, Title( "Only clients *not* in vehicles" )] public bool b_ClientsNotInVehicles { get; set; } = false;

	[Group( "SpawnFlags" )]
	[Property, Title( "Physics Debris" )] public bool b_PhysicsDebris { get; set; } = false;

	[Group( "SpawnFlags" )]
	[Property, Title( "Only NPCs in vehicles (respects player ally flag)" )] public bool b_NPCsInVehicles { get; set; } = false;

	[Group( "SpawnFlags" )]
	[Property, Title( "Everything (not including physics debris)" )] public bool b_Everything { get; set; } = false;

	[Property, Title( "Start Disabled" )] public bool b_StartDisabled { get; set; } = false;

	#endregion

	[Feature( "Debug" ), Title( "Enable Debug" ), Property] public bool isDebug = false;

	[HideIf( "isDebug", false )][Property, Feature( "Debug" ), Title( "Show Trigger Items List" )] public bool b_ShowTriggerItems { get; set; } = false;
	[DebugExpose] [HideIf( "b_ShowTriggerItems", false )][ReadOnly, Feature( "Debug" ), Title( "Objects in Trigger:" ), Property] public List<GameObject> inTriggerItems;

	/// <summary>
	/// The entity specifying the point to which entities should be teleported. Usually either a info_teleport_destination or info_target.
	/// </summary>
	[DebugExpose ( DisplayMember = "TargetName" )] [Property] public BaseEntity RemoteDestination { get; set; }

	/// <summary>
	/// If specified, then teleported entities are offset from the target by their initial offset from the landmark.
	/// </summary>
	[DebugExpose ( DisplayMember = "TargetName" )] [Property] public BaseEntity LocalDestinationLandmark { get; set; }

	/// <summary>
	/// If selected will rotate the teleported entities to match the rotation of the target.
	/// </summary>
	[DebugExpose,Title( "Rotation Offset" )] [Property] public bool b_RotationOffset { get; set; }

	[Property, MakeDirty] private bool isEnabled  { get; set; }


	protected override void OnStart()
	{
		isEnabled = !b_StartDisabled;
	}

	protected override void OnDirty()
	{
		if ( isEnabled && inTriggerItems.Count > 0 )
			HandleTeleport();
	}

	public void OnTriggerEnter( Collider activator )
	{
		TryAddToTriggerList( activator );

		if ( isEnabled )
			HandleTeleport();
		else if ( isDebug )
			Log.Warning( "trigger_teleport: Trigger is disabled, not teleporting entities." );
	}

	public void HandleTeleport()
	{
		if ( RemoteDestination == null )
		{
			if ( isDebug )
				Log.Warning( "trigger_teleport: No RemoteDestination set!" );

			return;
		}

		if ( inTriggerItems == null && isDebug || inTriggerItems.Count == 0  && isDebug )
		{
			Log.Warning( "trigger_teleport: No items in trigger to teleport!" );
			return;
		}

		foreach ( var item in inTriggerItems )
		{ 
			if ( item == null ) continue;

			item.WorldPosition = RemoteDestination.WorldPosition;

			if ( !b_RotationOffset )
				return;

			item.WorldRotation = RemoteDestination.WorldRotation;
		}

	}

	public void OnTriggerExit( Collider activator )
	{
		if ( inTriggerItems.Contains( activator.GameObject ) ) RemoveItemFromTrigger( activator.GameObject );

		if ( activator.GameObject.Tags.Has( "player" ) && inTriggerItems.Contains( activator.GameObject.Parent ) ) RemoveItemFromTrigger( activator.GameObject.Parent );
	}


	private void TryAddToTriggerList( Collider other )
	{
		var go = other.GameObject;

		// Handle players
		if ( b_Clients )
		{
			var player = go.GetComponentInParent<BasePlayer>();
			if ( player != null && !inTriggerItems.Contains( player.GameObject ) )
			{
				inTriggerItems.Add( player.GameObject );
				return;
			}
		}

		// Handle physics props (excluding debris)
		if ( b_PhysicsObjects && !go.Tags.Has( "debris" ) )
		{
			if ( go.GetComponent<Prop>() != null || go.GetComponent<Core.GameProp>() != null )
			{
				inTriggerItems.Add( go );
				return;
			}
		}

		// Handle physics debris
		if ( b_PhysicsDebris && go.Tags.Has( "debris" ) )
		{
			inTriggerItems.Add( go );
			return;
		}

		// Catch-all for everything else (excluding debris)
		if ( b_Everything && !go.Tags.Has( "debris" ) )
		{
			inTriggerItems.Add( go );
		}
	}

	private void RemoveItemFromTrigger( object item )
	{
		if ( inTriggerItems != null && inTriggerItems.Contains( item ) )
		{
			inTriggerItems.Remove( item as GameObject );
			//	CheckTriggerItemsEmpty();
		}
	}



}
