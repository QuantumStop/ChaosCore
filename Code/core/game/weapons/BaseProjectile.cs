namespace Core;

public class ProjectileObjectSystem : GameObjectSystem
{
	public ProjectileObjectSystem( Scene scene ) : base( scene ) => Listen( Stage.StartFixedUpdate, 10, ProjectileTransform, "ProjectileTransform" );

	/// <summary>Call fake FixedUpdate on all projectiles</summary>
	private void ProjectileTransform()
	{
		var all = Scene.GetAllComponents<BaseProjectile>();

		if ( all is null ) return;

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

abstract public class BaseProjectile : BaseEntity
{
	/// <summary>Override per type, should be in units (inches, not feet or yards or duyms)</summary>
	protected virtual float _velocityPerTick => 0;
	/// <summary>The ammo this is using (filled in through DamageInfo)</summary>
	[Property, ReadOnly] public AmmoInfo Ammo { get; set; }

	/// <summary>Public access for fixed think so I can call it from the GOS, but youre not supposed to call it anywhere else</summary>
	public void CallThink() => FixedThink();

	/// <summary>Kinda like FixedUpdate but we call it ourselves in the GOS (is that even faster)</summary>
	protected virtual void FixedThink() { }
}
