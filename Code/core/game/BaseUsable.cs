/// <summary>
/// Intermediate class to specify which props can receive use input
/// </summary>
public class BaseUsable : BaseEntity, Component.IPressable
{
	// we aim to get the player
	public new delegate void ChaosOutput( BasePlayer Activator );

	/// <summary>
	/// Can this entity be held
	/// </summary>
	[Property, Order( 23 ), ShowIf( nameof( CanBeHeldAccessor ), true )] public bool CanBeHeld { get; set; } = true;

	/// <summary>
	/// Since the property is needed everywhere but sometimes hidden based on other variables, change this to hide it
	/// </summary>
	protected virtual bool CanBeHeldAccessor { get; set; } = true;

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
		if ( BasePlayer.Local.LifeState == LifeState.Dead )
			return false;

		var TryGet = press.Source.Components.TryGet<BasePlayer>( out var basePlayer );

		// Source of the Use should be a player, as this is specifically a player input press thing rather than general "anyone" (NPC) interaction
		OnUse?.Invoke( basePlayer );

		return true;
	}

}
