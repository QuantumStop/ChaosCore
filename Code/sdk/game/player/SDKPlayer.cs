namespace SDK;

using Core;

[Title( "SDK Example Player" )]
[Category( "SDK" )]
public partial class Player : BasePlayer
{
	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( Halo2Crosshair ) Local.Controller.Camera.GameObject.LocalRotation *= new Angles( -9, 0, 0 );
	}

	protected override void OnStart()
	{
		base.OnStart();

		WorldInput ??= Controller.Camera.Components.GetOrCreate<WorldInput>();
		WorldInput?.LeftMouseAction = "use";
	}

	protected override void OnDisabled()
	{
		WorldInput?.Enabled = false;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		CalculateBob();
	}
}
