using System;

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

	[ShowIf( nameof( isDebug ), true ), Property, Feature( "Debug" ), Title( "Show Trigger Items List" )] public bool b_ShowTriggerItems { get; set; } = false;
#if IGNIS
	[DebugExpose]
#endif
	[ShowIf( nameof( b_ShowTriggerItems ), true ), ReadOnly, Feature( "Debug" ), Title( "Objects in Trigger:" ), Property] public List<GameObject> inTriggerItems = new();

	/// <summary>
	/// The entity specifying the point to which entities should be teleported. Usually either a info_teleport_destination or info_target.
	/// </summary>
#if IGNIS
	[DebugExpose( DisplayMember = "TargetName" )]
#endif
	[Property] public BaseEntity RemoteDestination { get; set; }

	/// <summary>
	/// If specified, then teleported entities are offset from the target by their initial offset from the landmark.
	/// </summary>
#if IGNIS
	[DebugExpose( DisplayMember = "TargetName" )]
#endif
	[Property] public BaseEntity LocalDestinationLandmark { get; set; }

	/// <summary>
	/// If selected will rotate the teleported entities to match the rotation of the target.
	/// </summary>
#if IGNIS
	[DebugExpose]
#endif
	[Title( "Rotation Offset" ), Property] public bool b_RotationOffset { get; set; }

	[Property, Title( "Soften Velocity" )] public bool b_SoftenVelocity { get; set; } = false;

	[Property]
	private bool _isEnabled
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;

				if ( _isEnabled && inTriggerItems.Count > 0 )
					HandleTeleport();
			}
		}
	}


	protected override void OnStart()
	{
		inTriggerItems ??= new();
		_isEnabled = !b_StartDisabled;
	}

	public void OnTriggerEnter( Collider activator )
	{
		TryAddToTriggerList( activator );

		if ( _isEnabled )
			HandleTeleport();
		else if ( isDebug )
			Log.Warning( "trigger_teleport: Trigger is disabled, not teleporting entities." );
	}

	public void HandleTeleport()
	{
		if ( !RemoteDestination.IsValid() )
		{
			if ( isDebug )
				Log.Warning( "trigger_teleport: No RemoteDestination set!" );

			return;
		}

		if ( (inTriggerItems is null || inTriggerItems.Count == 0) && isDebug )
		{
			Log.Warning( "trigger_teleport: No items in trigger to teleport!" );
			return;
		}

		if ( inTriggerItems is null )
			return;

		foreach ( var item in inTriggerItems )
		{
			if ( !item.IsValid() ) continue;

			var player = item.Components.Get<BasePlayer>( FindMode.EverythingInSelfAndChildren );
			var sourceRotation = GetTeleportRotation( item, player );

			item.WorldPosition = GetTeleportPosition( item );
			SoftenVelocity( item );

			if ( !b_RotationOffset )
				continue;

			SetTeleportRotation( item, player, sourceRotation );
		}

	}

	private Vector3 GetTeleportPosition( GameObject item )
	{
		if ( !LocalDestinationLandmark.IsValid() )
			return RemoteDestination.WorldPosition;

		var localOffset = (item.WorldPosition - LocalDestinationLandmark.WorldPosition) * LocalDestinationLandmark.WorldRotation.Inverse;
		return RemoteDestination.WorldPosition + localOffset * RemoteDestination.WorldRotation;
	}

	private Rotation GetTeleportRotation( GameObject item, BasePlayer player )
	{
		if ( player.IsValid() && player.Controller.IsValid() )
			return player.Controller.EyeAngles.ToRotation();

		return item.WorldRotation;
	}

	private Rotation GetTeleportRotationTarget( Rotation sourceRotation )
	{
		if ( !LocalDestinationLandmark.IsValid() )
			return RemoteDestination.WorldRotation;

		var localRotation = LocalDestinationLandmark.WorldRotation.Inverse * sourceRotation;
		return RemoteDestination.WorldRotation * localRotation;
	}

	private void SetTeleportRotation( GameObject item, BasePlayer player, Rotation sourceRotation )
	{
		var targetRotation = GetTeleportRotationTarget( sourceRotation );

		if ( player.IsValid() && player.Controller.IsValid() )
		{
			player.Controller.LocalEyeAngles = targetRotation.Angles();

			if ( player.Controller.Head.IsValid() )
				player.Controller.Head.WorldRotation = targetRotation;

			return;
		}

		item.WorldRotation = targetRotation;
	}

	private void SoftenVelocity( GameObject item )
	{
		if ( !b_SoftenVelocity )
			return;

		if ( item.Components.Get<BasePlayer>( FindMode.EverythingInSelfAndChildren ) is { } player && player.IsValid() )
		{
			if ( player.Movement.IsValid() )
			{
				player.Movement.Velocity = Vector3.Lerp( player.Movement.Velocity, Vector3.Zero, 1f );
				player.Movement.BaseVelocity = Vector3.Lerp( player.Movement.BaseVelocity, Vector3.Zero, 1f );
				player.Movement.WishVelocity = Vector3.Lerp( player.Movement.WishVelocity, Vector3.Zero, 1f );
			}
		}

		foreach ( var rigidbody in item.Components.GetAll<Rigidbody>( FindMode.EverythingInSelfAndChildren ) )
		{
			rigidbody.Velocity = Vector3.Lerp( rigidbody.Velocity, Vector3.Zero, 1f );
			rigidbody.AngularVelocity = Vector3.Lerp( rigidbody.AngularVelocity, Vector3.Zero, 1f );
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
			if ( player.IsValid() && !inTriggerItems.Contains( player.GameObject ) )
			{
				inTriggerItems.Add( player.GameObject );
				return;
			}
		}

		// Handle physics props (excluding debris)
		if ( b_PhysicsObjects && !go.Tags.Has( "debris" ) )
		{
			if ( go.GetComponent<Prop>().IsValid() || go.GetComponent<GameProp>().IsValid() )
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
		if ( inTriggerItems is not null && inTriggerItems.Contains( item ) )
		{
			inTriggerItems.Remove( item as GameObject );
			//	CheckTriggerItemsEmpty();
		}
	}



}
