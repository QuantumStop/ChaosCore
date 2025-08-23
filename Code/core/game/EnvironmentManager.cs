namespace Core;

/// <summary>
/// Used to layer sounds for various things based on the environment
/// </summary>
public class EnvironmentManager : BaseEntity
{
	public enum EnvironmentType
	{
		[Description( "Nothing" )]
		None,
		[Description( "Interior rooms, small indoor spaces" )]
		IndoorSmall,
		[Description( "Warehouses, big indoor spaces" )]
		IndoorLarge,
		[Description( "Narrow urban areas" )]
		UrbanSmall,
		[Description( "Big urban areas" )]
		UrbanLarge,
		[Description( "Near hills" )]
		Hills
	}

	/// <summary>
	/// Static instance
	/// </summary>
	static public EnvironmentManager Instance { get; set; }

	protected override void OnEnabled()
	{
		base.OnEnabled();
		Instance = this;
	}

	[Property, Feature( "Debug" )] public EnvironmentType CurrentEnv { get; set; }

	string GetPlayerGunfireLayer( EnvironmentType type )
	{
		switch ( type )
		{
			default:
				return null;
			case EnvironmentType.IndoorSmall:
				return "sound/weapons/env/close_indoor_s.sound";
			case EnvironmentType.IndoorLarge:
				return "sound/weapons/env/close_indoor_l.sound";
			case EnvironmentType.UrbanSmall:
				return "sound/weapons/env/close_urban_s.sound";
			case EnvironmentType.UrbanLarge:
				return "sound/weapons/env/close_urban_l.sound";
			case EnvironmentType.Hills:
				return "sound/weapons/env/close_hills.sound";
		}
	}

	public void PlayEnviromentGunfire()
	{
		if ( CurrentEnv != EnvironmentType.None )
			Sound.Play( GetPlayerGunfireLayer( CurrentEnv ) ).ListenLocal = true;
	}

}