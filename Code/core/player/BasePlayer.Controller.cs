namespace Core;

partial class BasePlayer
{
	/// <summary>
	/// Player controlled "pawns" where we still want to be this player pawn, like vehicles or whatever else
	/// Null = nothing is controlled; this = playing is onfoot and self controlling; anything else = controlling other mini-pawn
	/// </summary>
	[Property, ReadOnly, Feature( "Debug" )] public IPlayerControllable CurrentControllable { get; set; }
	/// <summary>Is this player is in control of itself? (useful for checking externally like in weapons or something)</summary>
	public bool IsControllingSelf => CurrentControllable is not null && CurrentControllable == this;

	// all of these are under "IsControlledLocally"

	void IPlayerControllable.HandleActionUpdate() => HandleActionUpdate();

	protected virtual void HandleActionUpdate()
	{
		Controller?.DoInput();
		Controller?.BuildFrameInput();
		Controller?.DoUsing();
	}

	void IPlayerControllable.HandleActionFixedUpdate() => HandleActionFixedUpdate();

	protected virtual void HandleActionFixedUpdate()
	{
		Controller?.UpdateCrouching();
		HandleWeaponSelection();
		HandleLastSelected();
		UpdateFallDamage();
	}

	void IPlayerControllable.HandleLookInput() => HandleLookInput();
	protected virtual void HandleLookInput()
	{
		Controller?.DoEyeLook();

		if ( Controller.IsIsometricCamera )
		{
			if ( !Controller.UpdateIsometricCameraToggle() ) Controller?.UpdateIsometricCamera();
		}
		else
		{
			Controller.ResetBaseCameraProjection(); // Not sure these should be OnUpdate, especially for cases when a game is FP only
			if ( !Controller.UpdateIsometricCameraToggle() ) Controller?.UpdateCamera();
		}
	}
}

/// <summary>We have pawns, but we also have things that player can control while still "being" the player, this helps us send player inputs to the correct thing</summary>
public interface IPlayerControllable
{
	// all of these are abstract because im evil and forcing you to have them you should have these anyway

	/// <summary>Handle OnUpdate movement, actions, interactions etc (keyboard inputs, non-thumbstick controller buttons, Input.AnalogMove etc)</summary>
	abstract void HandleActionUpdate();
	/// <summary>Handle OnFixedUpdate movement, actions, interactions etc (keyboard inputs, non-thumbstick controller buttons, Input.AnalogMove etc)</summary>
	abstract void HandleActionFixedUpdate();
	/// <summary>
	/// Handle camera movement (mouse/controller thumbsticks, Input.AnalogLook etc)
	/// Always OnUpdate (or should be)
	/// </summary>
	abstract void HandleLookInput();
}
