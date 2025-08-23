namespace Editor;

public static class Sidebar
{
	[Event( "editor.created" )]
	public static void OnEditorCreated( EditorMainWindow _ )
	{
		var sidebar = MainAssetBrowser.Instance.GetDescendants<VerticalTab>().First().Parent;
		sidebar.Hide();
	}
}
