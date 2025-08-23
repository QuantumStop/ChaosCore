namespace chaoscore;

[Title( "chaoscore Player" )]
[Category( "chaoscore" )]
public partial class Player : BasePlayer
{
	public override string PlayerName { get; protected set; } = "Chaos Player";

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		CalculateFOV();
		if ( Halo2Crosshair ) Local.Controller.Camera.GameObject.LocalRotation *= new Angles( -9, 0, 0 );
	}

	protected override void CheckPrefabSetup()
	{
		base.CheckPrefabSetup();

		if ( !AnimInteraction.Interaction.IsValid() )
			Log.Error( "AnimInteraction manager does not exist!" );
	}

	protected override void OnEnabled()
	{
		if ( worldInput == null )
		{
			worldInput = new Sandbox.UI.WorldInput();
		}

		worldInput.Enabled = true;
	}

	protected override void OnDisabled()
	{
		if ( worldInput != null )
			worldInput.Enabled = false;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
		CalculateBob();

		if ( worldInput == null ) return;

		if ( Local?.Controller != null )
		{
			worldInput.Ray = Local.Controller.AimRay;
			worldInput.MouseLeftPressed = Input.Down( "attack1" );
		}

	}
}
