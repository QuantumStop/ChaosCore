using System;
[Inspector( typeof( PrefabPlacer ) )]
public class PrefabPlacerInspector : InspectorWidget
{
	public PrefabPlacerInspector( SerializedObject so ) : base( so )
	{
		if ( so.Targets.FirstOrDefault() is not PrefabPlacer placer )
			return;

		Layout = Layout.Column();
		Layout.Add( placer.BuildUI() );
	}
}

[EditorTool( "ktl.prefab-placer" )]
[Title( "Prefab placer" )]
[Icon( "dashboard_customize" )]
[Group( "4" )]
public class PrefabPlacer : EditorTool
{
	private Layout ControlLayout { get; set; }
	public PrefabFile prefab { get; set; } = new PrefabFile();
	[Range( 0, 360 ), Step( 1 )]
	public float YawRotation { get; set; } = 0f;

	[Description( "Offset distance from surface normal if 'use select normal' is selected, otherwise uses vertical offset" )]
	public float DistanceOffset { get; set; } = 0f;
	public bool SelectWhenPlaced { get; set; } = false;
	public bool BreakFromPrefab { get; set; } = false;

	[Description( "Uses surface normal for distance offset only" )]
	public bool useSurfaceNormal { get; set; } = false;
	public NavigationView navView { get; set; }

	public override void OnEnabled()
	{
		base.OnEnabled();
		AllowGameObjectSelection = false;
		Selection.Clear();
		Log.Info( "prefab placer active" );
	}

	public override void OnUpdate()
	{
		base.OnUpdate();
		SimulateCursor();
		EditorUtility.InspectorObject = this;
	}

	public Widget BuildUI()
	{
		var widget = new Widget( null );
		widget.Layout = Layout.Column();
		widget.Layout.Margin = 4;

		if ( prefab is not null )
		{
			var cs = new ControlSheet();

			cs.AddProperty( this, x => x.prefab );
			cs.AddProperty( this, x => x.YawRotation );
			cs.AddProperty( this, x => x.useSurfaceNormal );
			cs.AddProperty( this, x => x.DistanceOffset );
			cs.AddProperty( this, x => x.SelectWhenPlaced );
			cs.AddProperty( this, x => x.BreakFromPrefab );
			widget.Layout.Add( cs );

		}

		widget.Layout.AddSpacingCell( 16 );

		navView = widget.Layout.Add( new NavigationView() );

		widget.Layout.AddSpacingCell( 8 );

		var options = widget.Layout.AddRow();

		options.Margin = 2;
		options.Spacing = 4;

		var saveButton = new Button( "save list config", icon: "save" );
		saveButton.Clicked = () =>
		{
			SaveConfig();
		};
		options.Add( saveButton );

		//TODO: implement load/save function
		var loadButton = new Button( "Load list config", icon: "folder_open" );
		loadButton.Clicked = () =>
		{
			//EditorUtility.OpenFileDialog("Load prefab placer configuration","placer","");
			UpdatePages();
		};
		options.Add( loadButton );

		UpdatePages();

		return widget;
	}

	void SaveConfig()
	{
		var cfg = new PlacerConfigData();
		cfg.categories["npc"] = new CategoryData { icon = "bug_report", items = ["prefabs/npc/houndeye.prefab"] };
		cfg.categories["weapons"] = new CategoryData { icon = "whatshot", items = ["prefabs/weapons/weapon_glock.prefab", "prefabs/weapons/weapon_smg2.prefab"] };
		cfg.categories["debug"] = new CategoryData { icon = "science", items = ["prefabs/interactive_objects/subway_cash_register.prefab", "prefabs/game/particles/water_splash/water_splash.prefab"] };
		PlacerConfigData.Save( cfg );
	}

	void UpdatePages()
	{
		var cfg = PlacerConfigData.Load();

		if ( cfg is null )
		{
			SaveConfig();
			UpdatePages();
			return;
		}



		foreach ( var category in cfg.categories )
		{
			ConstructPages( category.Key, category.Value.icon, category.Value.items );
		}

	}


	private HashSet<string> createdPages = new();

	public void ConstructPages( string name, string icon, string[] items )
	{

		if ( createdPages.Contains( name ) )
			return;
		var page1 = new NavigationView.Option( name, icon );

		page1.CreatePage = () =>
		{
			var scroll = new ScrollArea( null );
			scroll.Canvas = new Widget( scroll );
			scroll.Canvas.Layout = Layout.Column();
			scroll.Canvas.Layout.Margin = 8;


			var body = scroll.Canvas.Layout;
			body.Spacing = 6;

			var title = new Label.Subtitle( name );
			title.Alignment = TextFlag.Center;
			title.Color = Theme.Blue;

			body.Add( title );
			scroll.Canvas.Layout.AddSeparator();

			foreach ( var item in items )
			{
				if ( !ResourceLibrary.TryGet( item, out PrefabFile temp ) )
				{
					Log.Warning( $"One or more prefabs not found, check {Sandbox.FileSystem.Data.GetFullPath( "placerconfig.json" )} for spelling errors" );
					return scroll;
				}
				var button = new Button( temp.ResourceName );
				button.Clicked = () =>
				{
					prefab = temp;
				};

				body.Add( button );
			}

			body.AddStretchCell();
			return scroll;
		};
		createdPages.Add( name );

		navView.AddPage( page1 );
	}



	//	Rotation rot;
	//TODO: implement normal height offset
	public void SimulateCursor()
	{
		if ( prefab.ResourceName is null )
		{
			Scene.DebugOverlay.ScreenText( new Vector2( 25, 25 ), "WARNING: No prefab specified", flags: TextFlag.Left, size: 16, color: Color.Yellow );
			return;
		}

		var tr = Trace
		.UseRenderMeshes( true )
		.UsePhysicsWorld( true )
		.Run();

		if ( !tr.Hit )
		{
			var plane = new Plane( Vector3.Up, 0f );
			if ( plane.TryTrace( new Ray( tr.StartPosition, tr.Direction ), out tr.EndPosition, true ) )
			{
				tr.Hit = true;
				tr.Normal = plane.Normal;
			}
		}

		if ( tr.Hit )
		{
			if ( EditorScene.GizmoSettings.SnapToGrid )
			{
				tr.EndPosition = tr.EndPosition.SnapToGrid( EditorScene.GizmoSettings.GridSpacing, true, true, true );
			}

			var rot = useSurfaceNormal ? tr.Normal : Vector3.Up;

			using ( Gizmo.Scope( "tool", new Transform( tr.EndPosition + rot * DistanceOffset, Rotation.FromYaw( YawRotation ) ) ) )
			{

				//Preview model if prefab has it
				var t = SceneUtility.GetPrefabScene( prefab );
				var edmdl = t.GetComponentsInChildren<ModelRenderer>( includeSelf: true ).FirstOrDefault();
				Gizmo.Draw.Color = Color.Yellow;
				if ( edmdl is not null )
				{
					Gizmo.Draw.Model( edmdl.Model.Name );
					Gizmo.Draw.LineBBox( edmdl.Bounds );
				}

				//"Crosshair"


				Gizmo.Draw.LineBBox( BBox.FromPositionAndSize( 0, 4f ) );
				Gizmo.Draw.LineSphere( Vector3.Zero, 0.4f );

				Gizmo.Transform = new Transform( tr.EndPosition + tr.Normal * 0, Rotation.LookAt( rot ) );
				Gizmo.Draw.Color = Color.Cyan;
				Gizmo.Draw.LineCircle( 0, 2f );

				if ( Gizmo.HasClicked ) AddObject( prefab, tr.EndPosition + tr.Normal * DistanceOffset, tr, Rotation.FromYaw( YawRotation ) );
			}
		}
	}

	void AddObject( PrefabFile entry, Vector3 pos, SceneTraceResult tr, Rotation rot )
	{

		using ( Gizmo.Scope( "tool" ) )
		{

			if ( entry.ResourceName is not null )
			{
				using ( SceneEditorSession.Active.UndoScope( "prefabplace" ).WithComponentCreations().WithGameObjectCreations().Push() )
				{
					var go = SceneUtility.GetPrefabScene( entry )?.Clone();
					if ( BreakFromPrefab ) go.BreakFromPrefab();

					go.Name = entry.MenuPath.Split( '/' ).Last();

					go.Transform.Local = new Transform( pos, rot );
					if ( SelectWhenPlaced )
					{
						Selection.Set( go );
						EditorUtility.InspectorObject = go;
						EditorToolManager.SetTool( nameof( ObjectEditorTool ) );
					}
				}

			}

		}
	}

	[Shortcut( "ktl.prefab-placer", "Shift+E", typeof( SceneViewportWidget ) )]
	public static void ActivateTool()
	{
		EditorToolManager.SetTool( nameof( PrefabPlacer ) );
	}

	public override void OnDisabled()
	{
		base.OnDisabled();
		if ( SelectWhenPlaced ) return;
		EditorUtility.InspectorObject = null;
	}

}

public class PlacerConfigData
{
	public Dictionary<string, CategoryData> categories { get; set; } = new();
	public static PlacerConfigData Load()
	{
		return Sandbox.FileSystem.Data.ReadJson<PlacerConfigData>( "placerconfig.json" );
	}

	public static void Save( PlacerConfigData data )
	{
		Sandbox.FileSystem.Data.WriteJson( "placerconfig.json", data );
	}
}
public class CategoryData
{
	public string icon { get; set; }
	public string[] items { get; set; }
}

