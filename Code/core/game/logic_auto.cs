using System.Threading.Tasks;

[Icon( "auto" )]
[Description( "Logic that runs immediately after certain game event. Usually immediately when a map starts." )]
public class logic_auto : BaseEntity
{
	public new delegate void ChaosOutput( logic_auto activator );

	/// <summary>
	/// Fired shortly after the the scene starts (including loading saves).
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnMapSpawn { get; set; }

	/// <summary>
	/// Fired when the map is loaded to start a new game.
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnNewGame { get; set; }

	/// <summary>
	/// Fired when the map is loaded from a saved game.
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnLoadGame { get; set; }

	/// <summary>
	/// Fired when the map is loaded due to a level transition.
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnMapTransition { get; set; }

	protected override async void OnStart()
	{
		await GameTask.Delay( 1 );

		OnMapSpawn?.Invoke( this );
	}


}
