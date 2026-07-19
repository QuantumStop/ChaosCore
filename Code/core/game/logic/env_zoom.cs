namespace Core;

[Category( "Logic" ), Title( "env_zoom" )]
public class env_zoom : BaseEntity
{
	/// <summary>
	/// Target FOV to zoom to
	/// </summary>
	[Property] public int FOV { get; set; }
	/// <summary>
	/// How long does it take to zoom to the target
	/// </summary>
	[Property] public float Time { get; set; }

	[Button]
	public void TriggerZoom()
	{
		BasePlayer.Local.SetFOV( this, FOV, Time );
	}
}
