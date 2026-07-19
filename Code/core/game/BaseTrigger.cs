using System;

namespace Core;

[Hide]
public class BaseTrigger : BaseEntity, Component.ITriggerListener, Component.IPressable
{
	public new delegate void ChaosOutput( BaseEntity activator );

	/// <summary>
	/// Used to hold the reference to the collider of this trigger
	/// </summary>
	protected Collider EntityCollider { get; set; }

	#region SpawnFlags

	[Flags]
	public enum SpawnFlags
	{
		/// <summary>
		/// Clients
		/// </summary>
		Clients = 1 << 0,
		/// <summary>
		/// NPCs
		/// </summary>
		Npcs = 1 << 1,
		/// <summary>
		/// Pushables
		/// </summary>
		Pushables = 1 << 2,
		/// <summary>
		/// Physics Objects
		/// </summary>
		PhysicsObjects = 1 << 3,
		/// <summary>
		/// Only player ally NPCs
		/// </summary>
		PlayerAllyNPCs = 1 << 4,
		/// <summary>
		/// Only clients in vehicles
		/// </summary>
		ClientsInVehicles = 1 << 5,
		/// <summary>
		/// Only clients not in vehicles
		/// </summary>
		ClientsNotInVehicles = 1 << 6,
		/// <summary>
		/// Physics Debris
		/// </summary>
		PhysicsDebris = 1 << 7,
		/// <summary>
		/// Only NPCs in vehicles, respects player ally flag
		/// </summary>
		NPCsInVehicles = 1 << 8,
		/// <summary>
		/// Everything, EXCLUDING physics debris
		/// </summary>
		Everything = Clients | Npcs | Pushables | PhysicsObjects | PlayerAllyNPCs | ClientsInVehicles | ClientsNotInVehicles | NPCsInVehicles
	}

	[Group( "SpawnFlags" ), ActionGraphIgnore, Property]
	public SpawnFlags spawnFlags { get; set; } = SpawnFlags.Clients;
	#endregion

	[Property, Title( "Start Disabled" )] public bool StartDisabled { get; set; } = false;

	[Feature( "Debug" ), Title( "Enable Debug" ), Property] public bool isDebug = false;

	[Feature( "Debug" ), ShowIf( "isDebug", true ), ReadOnly, Property, Title( "Tracked Objects Count" )] public int TrackedCount => trackedItems.Count;

	[ShowIf( "isDebug", true ), ReadOnly]
	[ActionGraphIgnore, Feature( "Debug" ), Title( "Objects in Trigger:" ), Property] public Dictionary<GameObject, int> trackedItems = [];


	/// <summary>
	/// Fire when the inTriggerItems list becomes empty.
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnTriggerItemsEmpty { get; set; }

	/// <summary>
	/// Fires whenever an entity matches all of the trigger's criteria.
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 )] public ChaosOutput OnTrigger { get; set; }


	#region PressableOutputs

	/// <summary>
	/// When used the entity. Could be a player or an NPC.
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 ), ShowIf( nameof( IsPressable ), true )] public ChaosOutput OnUse { get; set; }

	/// <summary>
	/// When actively using the entity. Could be a player or an NPC.
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 ), ShowIf( nameof( IsPressable ), true )] public ChaosOutput OnUsing { get; set; }

	/// <summary>
	/// When just stopped using the entity. Could be a player or an NPC.
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 ), ShowIf( nameof( IsPressable ), true )] public ChaosOutput OnRelease { get; set; }

	#endregion

	#region TriggerOutputs

	/// <summary>
	/// Fired when a valid entity starts touching this trigger.
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 ), ShowIf( nameof( IsTouchable ), true )] public ChaosOutput OnStartTouch { get; set; }

	/// <summary>
	/// Fired when a valid entity starts touching this trigger, and no other entities are touching it. 
	/// If there are any other entities touching the trigger when a new one begins to touch, only OnStartTouch will fire.
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 ), ShowIf( nameof( IsTouchable ), true )] public ChaosOutput OnStartTouchAll { get; set; }

	/// <summary>
	/// Fired when a valid entity stops touching this trigger.
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 ), ShowIf( nameof( IsTouchable ), true )] public ChaosOutput OnEndTouch { get; set; }

	/// <summary>
	/// Fired when all valid entities stop touching this trigger.
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 ), ShowIf( nameof( IsTouchable ), true )] public ChaosOutput OnEndTouchAll { get; set; }

	/// <summary>
	/// Fired if something is currently touching this trigger.
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 ), ShowIf( nameof( IsTouchable ), true )] public ChaosOutput OnTouching { get; set; }

	/// <summary>
	/// Fired if something is currently touching this trigger.
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 ), ShowIf( nameof( IsTouchable ), true )] public ChaosOutput OnNotTouching { get; set; }

	#endregion


	/// <summary>
	/// Returns true if this trigger can be pressed by a player or an NPC. 
	/// This is used to determine if the OnUse, OnUsing, and OnRelease outputs should be shown.
	/// </summary>
	public virtual bool IsPressable => false;

	/// <summary>
	/// Returns true if this trigger can be touched by entities.
	/// This is used to determine if the OnStartTouch, OnEndTouch, OnTouching, and OnNotTouching outputs should be shown.
	/// </summary>
	public virtual bool IsTouchable => false;

	public bool isEnabled { get; set; } = true;

	private readonly object triggerItemsLock = new();

	protected override void OnStart()
	{
		isEnabled = !StartDisabled;

		EntityCollider ??= GameObject.GetComponent<Collider>();

		RegisterOutputEvents();
	}

	protected virtual void RegisterOutputEvents()
	{
		OnTriggerItemsEmpty?.Invoke( this );
	}

	protected override void DrawGizmos()
	{
		// Not valid in "point entity" like usage, at least for now. Thus no icon!
		var editorvis = this.GetEditorVis();
		editorvis = null;
	}


	#region Interface Implementations

	[ActionGraphIgnore]
	public void OnTriggerEnter( Collider activator )
	{
		TryAddToTriggerList( activator );

		if ( !isEnabled )
			return;

		OnTriggerIn();
	}

	[ActionGraphIgnore]
	public void OnTriggerExit( Collider activator )
	{
		var go = GetTrackedObject( activator );
		if ( !go.IsValid() ) return;

		RemoveItemFromTrigger( go );
		OnTriggerOut();
	}


	[ActionGraphIgnore]
	public bool Press( IPressable.Event press )
	{
		if ( !TryGetEntity( press.Source, out var entity ) )
			return false;

		OnUse?.Invoke( entity );

		OnTriggerPress();

		return true;
	}

	[ActionGraphIgnore]
	public bool Pressing( IPressable.Event pressing )
	{
		if ( !TryGetEntity( pressing.Source, out var entity ) )
			return false;

		OnUsing?.Invoke( entity );

		OnTriggerPressing();

		return true;
	}

	[ActionGraphIgnore]
	public bool Release( IPressable.Event release )
	{
		if ( !TryGetEntity( release.Source, out var entity ) )
			return false;

		OnRelease?.Invoke( entity );

		OnTriggerRelease();

		return true;
	}

	#endregion


	/// <summary>
	/// Called when an entity enters the trigger volume.
	/// </summary>
	protected virtual void OnTriggerIn() { }

	/// <summary>
	/// Called when an entity exits the trigger volume.
	/// </summary>
	protected virtual void OnTriggerOut() { }

	/// <summary>
	/// Called when the trigger is pressed by an entity.
	/// </summary>
	protected virtual void OnTriggerPress() { }

	/// <summary>
	/// Called when the trigger is being pressed by an entity.
	/// </summary>
	protected virtual void OnTriggerPressing() { }

	/// <summary>
	/// Called when the trigger is released by an entity.
	/// </summary>
	protected virtual void OnTriggerRelease() { }

	/// <summary>
	/// Called when the inTriggerItems list becomes empty.
	/// This is used to perform any cleanup or reset actions when there are no items left in the trigger.
	/// </summary>
	protected virtual void OnItemsEmpty() { ClearInTriggerItems(); }


	private void ClearInTriggerItems()
	{
		lock ( triggerItemsLock )
		{
			trackedItems.Clear();
		}
	}

	private static bool TryGetEntity( Component source, out BaseEntity entity )
	{
		entity = source?.Components?.Get<BaseEntity>();

		if ( entity is BasePlayer player )
		{
			if ( player.LifeState != LifeState.Dead )
				return true;

			entity = null;
			return false;
		}

		return entity.IsValid();
	}

	private void TryAddToTriggerList( Collider activator )
	{
		var go = GetTrackedObject( activator );
		if ( !go.IsValid() ) return;

		lock ( triggerItemsLock )
		{
			if ( trackedItems.TryGetValue( go, out var count ) )
			{
				trackedItems[go] = count + 1;
			}
			else if ( IsValidTriggerEntity( go ) )
			{
				trackedItems[go] = 1;
			}
		}
	}

	private bool IsValidTriggerEntity( GameObject go )
	{
		bool isDebris = go.Tags.Has( "debris" );
		var player = go.GetComponentInParent<BasePlayer>();

		// Everything: all non-debris flags, ignore debris
		if ( spawnFlags == SpawnFlags.Everything && !isDebris )
			return true;

		if ( (spawnFlags & SpawnFlags.Clients) != 0 && player.IsValid() )
			return true;

		if ( (spawnFlags & SpawnFlags.PhysicsObjects) != 0 &&
			!isDebris &&
			(go.GetComponent<Prop>().IsValid() || go.GetComponent<GameProp>().IsValid()) )
			return true;

		if ( (spawnFlags & SpawnFlags.PhysicsDebris) != 0 && isDebris )
			return true;

		return false;
	}


	private void RemoveItemFromTrigger( GameObject go )
	{
		if ( !go.IsValid() ) return;

		lock ( triggerItemsLock )
		{
			if ( trackedItems.TryGetValue( go, out var count ) )
			{
				if ( count > 1 )
				{
					trackedItems[go] = count - 1;
				}
				else
				{
					trackedItems.Remove( go );

					if ( trackedItems.Count == 0 )
					{
						OnTriggerItemsEmpty?.Invoke( this );
						OnItemsEmpty();
					}
				}
			}
		}
	}

	public IEnumerable<GameObject> GetTrackedItems()
	{
		// Simply return the keys (make a copy to avoid modification during iteration)
		return [.. trackedItems.Keys];
	}

	private GameObject GetTrackedObject( Collider collider )
	{
		// Try to resolve to a consistent top-level GameObject to track
		return collider?.GameObject?.Root ?? collider?.GameObject?.Parent ?? collider?.GameObject;
	}

	/// <summary>
	/// Behave as if the caller entity had just entered the trigger volume. Accepts non-physical entities.
	/// </summary>
	public BaseEntity StartTouch( BaseEntity activator = null )
	{
		OnTriggerEnter( activator?.GameObject?.GetComponent<Collider>() );

		return activator ?? null;
	}

	/// <summary>
	/// Behave as if caller had just exited the trigger volume.
	/// </summary>
	public BaseEntity EndTouch( BaseEntity activator = null )
	{
		OnTriggerExit( activator?.GameObject?.GetComponent<Collider>() );

		return activator ?? null;
	}

	/// <summary>
	/// Disables this trigger and calls EndTouch on all currently-touching entities.
	/// </summary>
	public BaseEntity DisableAndEndTouch( BaseEntity activator = null )
	{
		isEnabled = false;

		foreach ( var item in trackedItems.Keys.ToList() )
		{
			EndTouch( item.GetComponent<BaseEntity>() );
		}

		return activator ?? null;
	}


}
