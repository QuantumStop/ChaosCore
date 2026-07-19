#if FMOD
#endif
namespace Core;

/// <summary>
/// Interface with player events, dispatched on various moments involving a player pawn
/// </summary>
public interface IPlayerEvents : ISceneEvent<IPlayerEvents>
{
	/// <summary>
	/// This player has died
	/// </summary>
	/// <param name="Player">Player in question</param>
	void OnDeath( BasePlayer Player ) { }
	/// <summary>
	/// This player has spawned (for the first and only time, respawns/revives/rewhatevers don't count)
	/// </summary>
	/// <param name="Player">Player in question</param>
	void OnSpawn( BasePlayer Player ) { }
	/// <summary>
	/// This player has taken damage
	/// </summary>
	/// <param name="Player">Player in question</param>
	/// <param name="damageInfo">Damage in question</param>
	void OnTookDamage( BasePlayer Player, DamageInfo damageInfo ) { }
}

public partial class BasePlayer
{
	protected virtual void OnPickupTriggerEnter( Collider collider )
	{
		if ( collider.GameObject.Components.TryGet<BaseItem>( out var item ) && item.AllowTouchPickup() ) item.StartPickingUp( this );
	}

	protected virtual void OnPickupTriggerExit( Collider collider )
	{
		if ( collider.GameObject.Components.TryGet<BaseItem>( out var item ) && item.AllowTouchPickup() ) item.StopPickingUp( this );
	}

	protected virtual void OnLandedEvent( float FallDist, Vector3 impactVel, Surface surface )
	{
#if FMOD
		if ( WaterLevel < WaterLvl.Waist )
		{
			SolveNullStringsInSurface( surface, out var surfstring );
			FootSound( "event:/Physics/Land", GameObject, surfstring );
		}
#endif
	}
}
