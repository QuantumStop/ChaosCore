namespace Core;

public class ProjectileObjectSystem : GameObjectSystem
{
	public ProjectileObjectSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.StartFixedUpdate, 10, ProjectileTransform, "ProjectileTransform" );
	}

	/// <summary>
	/// Call fake FixedUpdate on all projectiles
	/// </summary>
	private void ProjectileTransform()
	{
		var all = Scene.GetAllComponents<BaseProjectile>();

		if ( all is null )
			return;

		if ( all.Any() )
		{
			foreach ( var proj in all )
			{
				try
				{
					proj?.CallThink();
				}
				catch
				{
					Log.Warning( "Bullet trace was bad, probably null surface or effect!" );
					proj?.Destroy();
				}
			}
		}
	}
}

[Hide]
public class BaseProjectile : BaseEntity
{
	/// <summary>
	/// Override per type, should be in units (inches, not feet or yards or duyms)
	/// </summary>
	protected virtual float _velocityPerTick => 0;
	/// <summary>
	/// The ammo this is using (filled in through DamageInfo)
	/// </summary>
	[Property, ReadOnly] public AmmoInfo Ammo { get; set; }

	public void CallThink() => FixedThink();

	/// <summary>
	/// Kinda like FixedUpdate but we call it ourselves in the GOS (is that even faster)
	/// </summary>
	protected virtual void FixedThink()
	{

	}
}
