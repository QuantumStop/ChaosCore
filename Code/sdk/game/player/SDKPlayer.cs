namespace SDK;

using Core;

[Title( "SDK Example Player" )]
[Category( "SDK" )]
public partial class Player : BasePlayer
{
	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		// This should only work in first person mode, as it will break aiming otherwise!
		if ( Halo2Crosshair && Controller.CameraMode == XMovement.PlayerWalkControllerComplex.CameraModes.FirstPerson ) Local.Controller.Camera.GameObject.LocalRotation *= new Angles( -9, 0, 0 );
	}

	[Property, ReadOnly, Feature( "Debug" )] public override bool HasSuit => true;

	protected override void OnStart()
	{
		base.OnStart();

		WorldInput ??= Controller.Camera.Components.GetOrCreate<WorldInput>();
		WorldInput?.LeftMouseAction = "use";
	}

	protected override void OnDisabled() => WorldInput?.Enabled = false;

	protected override void OnUpdate()
	{
		if ( LifeState == LifeState.Alive ) AnimatePlayerModel();

		base.OnUpdate();

		CalculateBob();
	}

	public override void OnDeath( DamageInfo dmginfo )
	{
		base.OnDeath( dmginfo );

		// TODO: Better SDK example how to do this, take more from 2K in regards to structure/handling.

		if ( CurrentWeapon.IsValid() ) DropWeapon( CurrentWeapon, false );

		CurrentWeapon = null;
	}

}
	