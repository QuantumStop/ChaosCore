using Editor;
using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

[CanEdit( "fac" )]
[EditorApp( "Npc Faction Relations Editor", "manage_accounts", "With which to edit faction tables" )]
public class NpcFactionTableEditor : BaseWindow
{
	private bool Dirty { get; set; }

	public Dictionary<Guid, string> FactionNames { get; set; } = new();
	public Dictionary<Guid, Dictionary<Guid, NpcRelations.Relation>> Relations { get; set; } = new();

	private Layout PageBody;
	public NpcFactionTableEditor()
	{
		WindowTitle = "Faction Relations Editor";
		SetWindowIcon( "manage_accounts" );

		Size = new Vector2( 400f, 800f );
		Layout = Layout.Column();

		Load();
	}

	public void DrawUI( bool makeDirty = true )
	{
		Layout.Clear( true );

		if ( makeDirty )
			Save();

		if ( Dirty )
			WindowTitle = "Faction Relations Editor *";
		else
			WindowTitle = "Faction Relations Editor";

		Layout.Margin = 16f;
#if false
		var fileactions = Layout.AddRow();
		var savebutton = new Button.Primary("Save");
		savebutton.Icon = "save";
		savebutton.Pressed += Save;
		fileactions.Add(savebutton);
		fileactions.AddStretchCell();
#endif

		Layout.AddSpacingCell( 10f );
		var scroll = new ScrollArea( null );
		scroll.Canvas = new Widget( scroll );
		scroll.Canvas.Layout = Layout.Column();
		PageBody = scroll.Canvas.Layout;
		PageBody.Margin = 0f;
		PageBody.Alignment = TextFlag.LeftTop;

		foreach ( var faction in Relations )
		{
			PageBody.AddSpacingCell( 5f );

			var factionline = PageBody.AddRow();
			factionline.Alignment = TextFlag.Left;
			factionline.Spacing = 5f;

			var factionname = new LineEdit();
			factionname.Text = FactionNames[faction.Key];
			factionname.EditingFinished += delegate { FactionNames[faction.Key] = factionname.Text.ToUpper(); DrawUI(); };
			factionline.Add( factionname );

			var factionremove = new IconButton( "close" );
			factionremove.OnClick += delegate { RemoveFaction( faction.Key ); };
			factionline.Add( factionremove );

			factionline.AddSpacingCell( 10f );
		}

		PageBody.AddSpacingCell( 5f );
		var addnewbottom = PageBody.AddRow();
		addnewbottom.Alignment = TextFlag.CenterHorizontally;
		var newbutton = new IconButton( "+" );
		newbutton.OnClick += AddNewFaction;
		addnewbottom.Add( newbutton );
		addnewbottom.AddStretchCell();

		foreach ( var faction in Relations )
		{
			PageBody.AddSpacingCell( 15f );
			PageBody.AddRow().Add( new Label( FactionNames[faction.Key] == "" ? "----" : FactionNames[faction.Key] ) );

			foreach ( var relation in faction.Value )
			{
				var relationrow = PageBody.AddRow();
				relationrow.Alignment = TextFlag.Left;
				relationrow.Spacing = 20f;
				relationrow.AddSpacingCell( 25f );

				relationrow.Add( new Label( FactionNames[relation.Key] == "" ? "----" : FactionNames[relation.Key] ) );
				relationrow.AddStretchCell();

				var relationoption = new ComboBox();
				foreach ( var disposition in typeof( NpcRelations.Relation ).GetEnumNames() )
					relationoption.AddItem( disposition, null, delegate { Relations[faction.Key][relation.Key] = (NpcRelations.Relation)Enum.Parse( typeof( NpcRelations.Relation ), disposition ); Dirty = true; Save(); } );
				relationoption.TrySelectNamed( Relations[faction.Key][relation.Key].ToString() );
				relationrow.Add( relationoption );

				relationrow.AddSpacingCell( 50f );
			}
		}

		Layout.Add( scroll );
	}

	public void AddNewFaction()
	{
		Dictionary<Guid, NpcRelations.Relation> newfactionchunk = new();
		foreach ( var faction in Relations )
			newfactionchunk.Add( faction.Key, NpcRelations.Relation.NEUTRAL );
		var id = Guid.NewGuid();
		Relations.Add( id, newfactionchunk );
		foreach ( var faction in Relations )
			Relations[faction.Key].Add( id, NpcRelations.Relation.NEUTRAL );
		FactionNames.Add( id, "" );
		DrawUI();
	}

	public void RemoveFaction( Guid id )
	{
		foreach ( var faction in Relations )
			Relations[faction.Key].Remove( id );
		Relations.Remove( id );
		FactionNames.Remove( id );
		DrawUI();
	}

	public void Load()
	{
		var loadPath = Editor.FileSystem.Content.GetFullPath( "scripts\\ai_faction_relations.fac" );
		if ( loadPath == null )
			return;

		FactionNames = new();
		Relations = new();
		var file = JsonNode.Parse( File.ReadAllText( loadPath ) ).AsObject();
		Dictionary<string, Guid> guids = new();
		foreach ( var faction in file )
			guids.Add( faction.Key, Guid.NewGuid() );
		foreach ( var faction in file )
		{
			Dictionary<Guid, NpcRelations.Relation> relationset = new();
			foreach ( var relation in faction.Value.AsObject() )
				relationset.Add( guids[relation.Key], (NpcRelations.Relation)Enum.Parse( typeof( NpcRelations.Relation ), relation.Value.ToString() ) );
			Relations.Add( guids[faction.Key], relationset );
			FactionNames.Add( guids[faction.Key], faction.Key );
		}

		Dirty = false;
		DrawUI( false );
	}

	public void Save()
	{
		var savePath = Editor.FileSystem.Content.GetFullPath( "/scripts/ai_faction_relations.fac" );
		if ( savePath == null )
		{
			Log.Error( "Couldn't find .fac!" );
			return;
		}

		//serialize
		var file = new JsonObject();
		foreach ( var faction in Relations )
		{
			var relset = new JsonObject();
			foreach ( var relation in faction.Value )
				relset.Add( FactionNames[relation.Key], relation.Value.ToString() );
			file.Add( FactionNames[faction.Key], relset );
		}
		File.WriteAllText( savePath, file.ToString() );

		AssetSystem.RegisterFile( savePath );
		//MainAssetBrowser.Instance?.UpdateList();

		Dirty = false;

		return;
	}
}
