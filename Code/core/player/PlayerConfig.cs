namespace Core;

[AssetType( Name = "Player Config", Extension = "plr", Category = "Player" )]
public class PlayerConfig : GameResource
{
	public Model ViewmodelHands { get; set; }
	public Model BodyModel { get; set; }
	public List<HudRazorClass> HudEntries { get; set; }
	[InlineEditor, WideMode] public List<PlayerData> PlayerData { get; set; }

	public string Faction { get; set; }

	protected override Bitmap CreateAssetTypeIcon( int width, int height ) => CreateSimpleAssetTypeIcon( "accessibility", width, height );
}

public class HudRazorClass
{
	[Property, Title( "Selected Razor: " ), FilePath( Extension = "razor" )] public string RazorPath { get; set; }
	[Property] public bool RequireSuitToDraw { get; set; }

	public override string ToString() => RazorPath;
}

[AssetType( Name = "Player Data", Extension = "plreq", Category = "Player" )]
public class PlayerData : GameResource
{
	protected override Bitmap CreateAssetTypeIcon( int width, int height ) => CreateSimpleAssetTypeIcon( "psychology_alt", width, height );
}

