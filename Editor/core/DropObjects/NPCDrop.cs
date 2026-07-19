using System.Threading;
using System.Threading.Tasks;
using Application = Editor.Application;

[DropObject( "NpcDefinition", "npc", "npc_c"
#if IGNIS
	, Priority = 100
#endif
)]
class NpcDropObject : BaseDropObject
{
	Core.AI.NpcDefinition definition;
	string archetype;

	protected override async Task Initialize( string dragData, CancellationToken token )
	{
		Asset asset = await InstallAsset( dragData, token );

		if ( asset is null )
			return;

		if ( token.IsCancellationRequested )
			return;

		archetype = asset.FindStringEditInfo( "npc_archetype_id" );

		PackageStatus = "Loading NPC";
		definition = asset.LoadResource<Core.AI.NpcDefinition>();
		PackageStatus = null;

		if ( !definition.IsValid() )
		{
			Log.Warning( $"NpcDropObject: Failed to load NpcDefinition from '{asset.Path}'" );
			return;
		}

		var firstModel = definition.Models?.FirstOrDefault();
		if ( firstModel is null )
		{
			Log.Warning( $"NpcDropObject: NpcDefinition '{definition.ResourcePath}' has no models" );
			return;
		}

		Bounds = firstModel.Bounds;
		PivotPosition = Bounds.ClosestPoint( Vector3.Down * 10000 );
	}

	public override void OnUpdate()
	{
		using var scope = Gizmo.Scope( "DropObject", traceTransform );

		Gizmo.Draw.Color = Color.White.WithAlpha( 0.3f );
		Gizmo.Draw.LineBBox( Bounds );

		Gizmo.Draw.Color = Color.White;

		if ( definition.IsValid() )
		{
			var so = Gizmo.Draw.Model( definition.Models?.FirstOrDefault() );
			if ( so.IsValid() )
			{
				so.Flags.CastShadows = true;
			}
		}

		if ( !string.IsNullOrWhiteSpace( PackageStatus ) )
		{
			Gizmo.Draw.Text( PackageStatus, new Transform( Bounds.Center ), "Poppins", 14 * Application.DpiScale );

			Gizmo.Draw.Color = Color.White.WithAlpha( 0.3f );
			Gizmo.Draw.Sprite( Bounds.Center + Vector3.Up * 12, 16, "materials/gizmo/downloads.png" );
		}
	}

	public override async Task OnDrop()
	{
		await WaitForLoad();

		if ( !definition.IsValid() )
			return;

		using var scene = SceneEditorSession.Scope();

		using ( SceneEditorSession.Active.UndoScope( "Drop NPC" ).WithGameObjectCreations().Push() )
		{
			GameObject = new GameObject( false )
			{
				Name = definition.ResourceName,
				WorldTransform = traceTransform
			};

			var controller = GameObject.Components.Create<Core.AI.AIController>();
			controller.Definition = definition;

			EditorScene.Selection.Clear();
			EditorScene.Selection.Add( GameObject );
			GameObject.Enabled = true;
		}
	}
}
