#if FMOD
using FMODSbox;
#endif
namespace Core;

public partial class BaseCombatWeapon
{
	protected virtual KelvinSpotLight _muzzleSpotLight { get; set; }
	protected TemporaryEffect _delete { get; set; }

	[ConVar( "debug_muzzle" )] static public bool DebugMuzzle { get; set; }

	public static Vector3 ReprojectToViewmodel( Vector3 worldPos, Vector3 cameraPos, Vector3 cameraForward,
		float viewmodelFov, float cameraFov, float flatten = 0f )
	{
		Vector3 toPoint = worldPos - cameraPos;

		float zOffsetAmount = Vector3.Dot( toPoint, cameraForward );
		Vector3 zOffset = zOffsetAmount * cameraForward;

		float fovScale = viewmodelFov / cameraFov;
		Vector3 adjusted = worldPos - zOffset * (1.0f - fovScale);

		// Flatten
		if ( flatten > 0f )
			adjusted = Vector3.Lerp( adjusted, cameraPos, flatten );

		return adjusted;
	}

	/// <summary>
	/// Easy way to get world transform of the attachment as an attachment object, if there is one
	/// </summary>
	/// <returns>FOV scaled transform of the muzzle, if the player is local</returns>
	public static Transform GetPlayerAttachObject( BasePlayer owner, string name, out GameObject muzzleObject )
	{
		Transform output = new();
		bool isUs = owner == BasePlayer.Local;
		muzzleObject = isUs ? owner.ViewmodelWeapon.GetAttachmentObject( name ) : owner.Controller.Head;

		if ( muzzleObject.IsValid() )
		{
			output = muzzleObject.WorldTransform;
			if ( isUs ) output.Position = ReprojectToViewmodel( muzzleObject.WorldPosition,
			 owner.PawnCamera.WorldPosition,
			 owner.PawnCamera.WorldRotation.Forward,
			 BasePlayer.ViewmodelFOV,
			 GameSettings.FieldOfView ); // don't scale if player isnt local
		}

		return output;
	}

	/// <summary>
	/// TODO: Fill out the stub
	/// </summary>
	/// <param name="name"></param>
	/// <param name="muzzleObject"></param>
	/// <returns></returns>
	public static Transform GetNPCAttachObject( string name, out GameObject muzzleObject )
	{
		Transform output = new();
		muzzleObject = new();
		return output;
	}

	/// <summary>
	/// Create the muzzle effect (particle and light) with all settings from the weapon resource
	/// </summary>
	[Rpc.Broadcast]
	protected virtual void CreateMuzzleFlash()
	{
		Transform adjustedTransform = GetPlayerAttachObject( Owner.Player, "muzzle", out var attachmentObj );

		if ( !attachmentObj.IsValid() )
		{
			Log.Warning( "No valid muzzle attachment was found, skipping..." );
			return;
		}

		// auto clear
		GameObject delete_go = Scene.CreateObject();
		_delete = delete_go.Components.Create<TemporaryEffect>();
		_delete.DestroyAfterSeconds = WeaponData.MuzzleLightTime;
		_delete.WaitForChildEffects = false;

		// TODO: In the future will have own muzzleflash particle component we can pass stuff to.
		// Seems nicer than just having a prefab only, particles should be scaleable per our need
		DebrisManager.CreateViewMuzzleflashObject( WeaponData, adjustedTransform.Position, adjustedTransform.Rotation, attachmentObj );

		// stay parented
		delete_go.Parent = attachmentObj;

		_muzzleSpotLight = delete_go.Components.Create<KelvinSpotLight>();
		_muzzleSpotLight.Brightness = 2f;
		_muzzleSpotLight.Shadows = false;
		_muzzleSpotLight.Cookie = WeaponData.MuzzleLightCookie;
		_muzzleSpotLight.ConeInner = 0;
		_muzzleSpotLight.ConeOuter = WeaponData.MuzzleLightFOV;
		_muzzleSpotLight.Attenuation = 0.5f;
		_muzzleSpotLight.Refresh();
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

	[Rpc.Broadcast]
	protected virtual void AttackSound( bool primary = true )
	{
#if FMOD
		var sound = primary ? WeaponData.AttackSoundPrimary : WeaponData.AttackSoundSecondary;

		if ( !sound.IsValid() ) return;

		var snd = FMODSound.Play( sound );
		if ( WeaponData.WantNearEmptySound ) FMODSound.SetParameter( snd, "parameter:/Weapons/MagPercent", (float)PrimaryAmmoLoaded / WeaponData.PrimaryAmmoCapacity );
#else
		var sounds = primary ? WeaponData.AttackSoundsPrimary : WeaponData.AttackSoundsSecondary;
		if ( sounds.Count < 1 ) return;

		_shootHandle?.Stop( 0.1f ); // cut off previous sound first, as the engine doesnt have voice stealing
		foreach ( var sound in sounds )
		{
			_shootHandle = Owner.Player.Controller.Head.PlaySound( sound, new( 16, 0, 0 ) ); // for that yummy steam audio
		}
#endif
	}

	/// <summary>
	/// Spawn a shell effect on a shell_eject attachment. Need Bone Objects!
	/// </summary>
	protected virtual void EjectShells()
	{
		SkinnedModelRenderer viewmodel = Owner.Player?.ViewmodelWeapon;

		if ( WeaponData?.WeaponViewmodel?.Attachments.Get( "shell_eject" ) is not null )
		{
			var ejectattachment = viewmodel.GetAttachmentObject( "shell_eject" );
			var velocity = Owner.Player?.Movement?.Velocity ?? Vector3.Zero;
			PrefabFile shellPrefab = WeaponData?.BulletCasingParticle;

			if ( ejectattachment.IsValid() && shellPrefab.IsValid() )
			{
				Vector3 adjustedPos = ReprojectToViewmodel(
					ejectattachment.WorldPosition,
					Scene.Camera.WorldPosition,
					Scene.Camera.WorldRotation.Forward,
					BasePlayer.ViewmodelFOV,
					GameSettings.FieldOfView
				);

				var ejectRotation = ejectattachment.WorldRotation;
				var cameraRotation = Scene.Camera.WorldRotation;

				float strafeSpeed = Vector3.Dot( velocity, cameraRotation.Right );
				float forwardSpeed = Vector3.Dot( velocity, cameraRotation.Forward );
				Vector3 inheritedVelocity =
					(ejectRotation.Right * strafeSpeed) +
					(ejectRotation.Forward * forwardSpeed);

				// Nudge the spawn position slightly out of the receiver to avoid try clipping through the viewmodel.
				// TODO: Undo this, doesn't completely eradicate it. We need a better solution.
				adjustedPos += ejectRotation.Right * 1.5f;

				DebrisManager.Instance.CreateShellCasing(
					shellPrefab.ResourcePath,
					adjustedPos,
					ejectRotation,
					ejectRotation.Up * 100f +
					ejectRotation.Right * 300f +
					inheritedVelocity * 0.7f
				);
			}
		}
	}
}
