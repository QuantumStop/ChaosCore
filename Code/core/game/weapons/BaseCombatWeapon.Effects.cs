namespace Core;

public partial class BaseCombatWeapon
{
	protected virtual KelvinSpotLight MuzzleSpotLight { get; set; }
	protected TemporaryEffect delete { get; set; }

	[ConVar( "debug_muzzle" )]
	static public bool DebugMuzzle { get; set; }

	/// <summary>
	/// Create the light effect with all settings from the weapon resource
	/// </summary>
	protected virtual void CreateMuzzleLight()
	{
		var muzzletransform = BasePlayer.Local.ViewmodelWeapon.GetAttachmentObject( "muzzle" );

		// auto clear
		GameObject delete_go = Scene.CreateObject();
		delete = delete_go.Components.Create<TemporaryEffect>();
		delete.DestroyAfterSeconds = WeaponData.MuzzleLightTime;
		delete.WaitForChildEffects = false;

		delete_go.Parent = muzzletransform;

		MuzzleSpotLight = delete_go.Components.Create<KelvinSpotLight>();
		MuzzleSpotLight.Brightness = 20000f;
		MuzzleSpotLight.Shadows = false;
		MuzzleSpotLight.Cookie = WeaponData.MuzzleLightCookie;
		MuzzleSpotLight.ConeInner = 0;
		MuzzleSpotLight.ConeOuter = WeaponData.MuzzleLightFOV;
		MuzzleSpotLight.Attenuation = 0.5f;
		MuzzleSpotLight.Refresh();
	}

	/// <summary>
	/// Force clear all effects
	/// </summary>
	public virtual void ClearEffects( GameObject go )
	{
		foreach ( var light in go?.Components.GetAll<KelvinSpotLight>( FindMode.EverythingInSelf ) )
		{
			if ( DebugMuzzle ) { Log.Info( "A muzzle light was force cleared!" ); }
			light?.Destroy();
		}
	}
}
