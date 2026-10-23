using Core.AI;
namespace Core;

#if FMOD
using FMODSbox;
#endif

public partial class BaseCombatWeapon
{
	protected virtual KelvinSpotLight _muzzleSpotLight { get; set; }
	protected TemporaryEffect _delete { get; set; }

	[ConVar( "debug_muzzle" )] static public bool DebugMuzzle { get; set; }

	public static Vector3 ReprojectToViewmodel( Vector3 worldPos, Vector3 cameraPos, Vector3 cameraForward, float viewmodelFov, float cameraFov, float flatten = 0f )
	{
		Vector3 toPoint = worldPos - cameraPos;

		float zOffsetAmount = Vector3.Dot( toPoint, cameraForward );
		Vector3 zOffset = zOffsetAmount * cameraForward;

		float fovScale = viewmodelFov / cameraFov;
		Vector3 adjusted = worldPos - zOffset * (1.0f - fovScale);

		// Flatten
		if ( flatten > 0f ) adjusted = Vector3.Lerp( adjusted, cameraPos, flatten );

		return adjusted;
	}

	/// <summary>Easy way to get world transform of the attachment as an attachment object, if there is one</summary>
	/// <returns>Attachment object of the muzzle, and fov scaled transform if applicable</returns>
	public static GameObject GetWeaponAttachObject( BaseEntity owner, string name, out Transform muzzleTransform )
	{
		muzzleTransform = new();
		GameObject muzzleObject = null;

		if ( owner is BasePlayer basePlayer )
		{
			muzzleObject = (basePlayer.ShouldDrawViewmodel() ? basePlayer.ViewmodelWeapon : basePlayer.WorldModelRenderer).GetAttachmentObject( name );

			if ( muzzleObject.IsValid() )
			{
				muzzleTransform = muzzleObject.WorldTransform;
				if ( basePlayer.ShouldDrawViewmodel() && basePlayer.UseFOVShader ) muzzleTransform.Position = ReprojectToViewmodel( muzzleObject.WorldPosition,
				 basePlayer.PawnCamera.WorldPosition,
				 basePlayer.PawnCamera.WorldRotation.Forward,
				 BasePlayer.ViewmodelFOV,
				 GameSettings.FieldOfView ); // don't scale if player isnt local
			}
		}
		else if ( owner is AIController ai )
		{
			muzzleObject = ai.WeaponModel.GetAttachmentObject( name ); // TODO: replace with proper when we have the gun refs
			muzzleTransform = muzzleObject.WorldTransform;
		}

		return muzzleObject;
	}

	/// <summary>Create the muzzle effect (particle and light) with all settings from the weapon resource</summary>
	[Rpc.Broadcast]
	protected virtual void CreateMuzzleFlash( bool player = true )
	{
		var attachmentObj = GetWeaponAttachObject( player ? Owner.Player : Owner.NPC, "muzzle", out var adjustedTransform );

		if ( !attachmentObj.IsValid() )
		{
			Log.Warning( "No valid muzzle attachment was found, skipping..." );
			return;
		}

		// auto clear
		GameObject delete_go = Scene.CreateObject();
		if ( _delete.IsValid() ) _delete.DestroyGameObject();
		_delete = delete_go.Components.GetOrCreate<TemporaryEffect>();
		_delete.DestroyAfterSeconds = WeaponData.MuzzleLightTime;
		_delete.WaitForChildEffects = false;

		// TODO: In the future will have own muzzleflash particle component we can pass stuff to.
		// Seems nicer than just having a prefab only, particles should be scaleable per our need
		DebrisManager.CreateMuzzleflashObject( WeaponData, adjustedTransform.Position, adjustedTransform.Rotation, attachmentObj );

		// stay parented
		delete_go.Parent = attachmentObj;

		_muzzleSpotLight = delete_go.Components.Create<KelvinSpotLight>();
		_muzzleSpotLight.Brightness = WeaponData.MuzzleLightBrightness;
		_muzzleSpotLight.Shadows = false;
		_muzzleSpotLight.Cookie = WeaponData.MuzzleLightCookie;
		_muzzleSpotLight.ConeInner = 0;
		_muzzleSpotLight.ConeOuter = WeaponData.MuzzleLightFOV;
		_muzzleSpotLight.Attenuation = 0.5f;
		_muzzleSpotLight.Refresh();
	}

	/// <summary>Force clear all effects</summary>
	public virtual void ClearEffects( GameObject go )
	{
		foreach ( var gameobject in go.Children )
		{
			if ( DebugMuzzle ) Log.Info( "A muzzle light was force cleared!" );
			gameobject?.Destroy();
		}
	}

	[Rpc.Broadcast]
	protected virtual void AttackSound( bool primary = true )
	{
#if FMOD
		var sound = IsFirstperson() ? primary ? WeaponData.AttackSoundPrimary : WeaponData.AttackSoundSecondary : primary ? WeaponData.AttackSoundPrimaryThirdPerson : WeaponData.AttackSoundSecondaryThirdPerson;

		if ( !sound.IsValid() ) return;

		var snd = IsFirstperson() ? FMODSound.Play( sound ) : FMODSound.Play( sound, GetWeaponAttachObject( Owner.Player.IsValid() ? Owner.Player : Owner.NPC, "muzzle", out Transform attachmentObj ) );
		if ( WeaponData.WantNearEmptySound ) FMODSound.SetParameter( snd, "parameter:/Weapons/MagPercent", (float)PrimaryAmmoLoaded / WeaponData.PrimaryAmmoCapacity );
#else
		var sounds = IsFirstperson() ? primary ? WeaponData.AttackSoundsPrimary : WeaponData.AttackSoundsSecondary : primary ? WeaponData.AttackSoundPrimaryThirdPerson : WeaponData.AttackSoundSecondaryThirdPerson;
		if ( sounds.Count < 1 ) return;

		_shootHandle?.Stop( 0.1f ); // cut off previous sound first, as the engine doesnt have voice stealing

		GameObject obj = IsFirstperson() ? Owner.Player.Controller.Head : GetWeaponAttachObject( Owner.Player.IsValid() ? Owner.Player : Owner.NPC, "muzzle", out Transform muzzleTransform );

		foreach ( var sound in sounds ) _shootHandle = obj.PlaySound( sound );
#endif
	}

	/// <summary>Spawn a shell effect on a shell_eject attachment. Need Bone Objects!</summary>
	[Rpc.Broadcast]
	protected virtual void EjectShells()
	{
		if ( (IsFirstperson() ? GetViewmodel() : GetWorldmodel())?.Attachments.Get( "shell_eject" ) is not null )
		{
			var ejectattachment = GetWeaponAttachObject( Owner.Player.IsValid() ? Owner.Player : Owner.NPC, "shell_eject", out var muzzleTransform );
			var velocity = Owner.Player?.Movement?.Velocity ?? Vector3.Zero;
			PrefabFile shellPrefab = WeaponData?.BulletCasingParticle;

			if ( ejectattachment.IsValid() && shellPrefab.IsValid() )
			{
				var adjustedPos = muzzleTransform.Position;
				var ejectRotation = ejectattachment.WorldRotation;
				var cameraRotation = Scene.Camera.WorldRotation;

				float strafeSpeed = Vector3.Dot( velocity, cameraRotation.Right );
				float forwardSpeed = Vector3.Dot( velocity, cameraRotation.Forward );
				Vector3 inheritedVelocity = (ejectRotation.Right * strafeSpeed) + (ejectRotation.Forward * forwardSpeed);

				// Nudge the spawn position slightly out of the receiver to avoid try clipping through the viewmodel.
				// TODO: Undo this, doesn't completely eradicate it. We need a better solution.
				adjustedPos += ejectRotation.Right * 1.5f;

				DebrisManager.Instance.CreateShellCasing( shellPrefab.ResourcePath, adjustedPos, ejectRotation, ejectRotation.Up * 100f + ejectRotation.Right * 300f + inheritedVelocity * 0.7f );
			}
		}
	}
}
