
[GameResource( "Chapters Definition", "chptdef", "A script file for all chapters", Icon = "flag_circle", IconBgColor = "#399bc2ff" )]
public class GameChaptersDefinition : GameResource
{
	[Header( "All of the chapters" )]

	[InfoBox( "These are all the levels that the main menu processes and lets you go-to on per chapter basis. Order is set inside each entry" )]
	[ConfigButton, Property, Title( "Entries" )] public List<GameChapter> GameChapterList { get; set; }

}

public partial class GameChapter
{
	[Property] public string ChapterName { get; set; }
	[Property] public int ChapterOrder { get; set; }
	[Property] public SceneFile SceneSource { get; set; }
	[Property, FilePath( Extension = "webm" )] public string SceneVid { get; set; }
	public override string ToString() { return ChapterName; }
}
