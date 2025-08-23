[GameResource( "Player Config", "plr", "LARGEST BABY SUPER STORE ONLINE", Icon = "accessibility_new" )]
public class PlayerConfig : GameResource
{
	public Model ViewmodelHands { get; set; }
	public Model BodyModel { get; set; }
	public List<HudRazorClass> HudEntries { get; set; }
	public string Faction { get; set; }
}

public class HudRazorClass
{
	[Property, Title( "Selected Razor: " ), FilePath( Extension = "razor" )] public string RazorPath { get; set; }
	public bool RequireSuitToDraw { get; private set; }
}

