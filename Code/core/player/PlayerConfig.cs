[GameResource( "Player Config", "plr", "LARGEST BABY SUPER STORE ONLINE", Icon = "accessibility_new" )]
public class PlayerConfig : GameResource
{
	public Model ViewmodelHands { get; set; }
	public Model BodyModel { get; set; }
	public List<string> HudRazorClasses { get; set; }
	public string Faction { get; set; }
}
