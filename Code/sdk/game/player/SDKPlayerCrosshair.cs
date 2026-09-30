namespace SDK;

using Core;
[Category( "SDK" )]
public class CustomCrosshair : Crosshair
{
	protected override Vector2 CalculateCenter( Vector2 screen )
	{
		if ( BasePlayer.Local?.Controller is PlayerController playerController && playerController.IsIsometricCamera )
			return CalculateDefaultCenter( screen );

		return screen * new Vector2( 0.5f, Player.Halo2Crosshair ? 0.6f : 0.5f );
	}
}
