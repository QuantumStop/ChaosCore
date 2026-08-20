namespace Core;

/// <summary>
/// Intermediate class to specify which props can receive use input
/// </summary>
[Hide]
public class BaseUsable : BaseEntity, Component.IPressable
{
	// we aim to get the player
	public new delegate void ChaosOutput( BasePlayer Activator );

	/// <summary>
	/// Can this entity be held
	/// </summary>
	[Property, Group( "Physics Properties" ), Order( 13 ), ShowIf( nameof( _canBeHeldAccessor ), true )] public virtual bool CanBeHeld { get; set; } = true;

	/// <summary>
	/// Since the property is needed everywhere but sometimes hidden based on other variables, change this to hide it
	/// </summary>
	protected virtual bool _canBeHeldAccessor { get; set; } = true;

	/// <summary>
	/// When tried using the entity
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 )] public ChaosOutput OnUse { get; set; }
	/// <summary>
	/// Started holding the entity
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 )] public ChaosOutput OnHoldStart { get; set; }
	/// <summary>
	/// OnFixedUpdate while holding it
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 )] public ChaosOutput OnHoldFixedUpdate { get; set; }
	/// <summary>
	/// OnUpdate while holding it
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 )] public ChaosOutput OnHoldUpdate { get; set; }
	/// <summary>
	/// When dropping the entity
	/// </summary>
	[Property, Group( "Outputs" ), Order( 100 )] public ChaosOutput OnDropped { get; set; }

	public virtual bool Press( IPressable.Event press )
	{
		if ( !press.Source.Components.TryGet<BasePlayer>( out var basePlayer ) || basePlayer?.LifeState == LifeState.Dead ) return false;

		// Source of the Use should be a player, as this is specifically a player input press thing rather than general "anyone" (NPC) interaction
		OnUse?.Invoke( basePlayer );

		return true;
	}

	/// <summary> Determine what allows us to get a success sound when pressing interaction input, vs fail sound</summary>
	public virtual bool CanInteract => true;

	/// <summary>Filter IPressable trace with our bool</summary>
	/// <param name="e"></param>
	/// <returns></returns>
	public bool CanPress( IPressable.Event e ) => CanInteract;

	public virtual bool Pressing( IPressable.Event press ) => !press.Source.Components.TryGet<BasePlayer>( out var basePlayer ) || basePlayer.LifeState != LifeState.Dead;

}
