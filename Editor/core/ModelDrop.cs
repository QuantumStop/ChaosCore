using System.Threading;
using System.Threading.Tasks;
using Application = Editor.Application;

[DropObject( "model", "vmdl", "vmdl_c" )]
class ModelDropObject : BaseDropObject
{
	Model model;
	string archetype;

	protected override async Task Initialize( string dragData, CancellationToken token )
	{
		Asset asset = await InstallAsset( dragData, token );

		if ( asset is null )
			return;

		if ( token.IsCancellationRequested )
			return;

		archetype = asset.FindStringEditInfo( "model_archetype_id" );

		PackageStatus = "Lo	ading Model";
		model = await Model.LoadAsync( asset.Path );
		PackageStatus = null;

		Bounds = model.Bounds;
		PivotPosition = Bounds.ClosestPoint( Vector3.Down * 10000 );
	}

	public override void OnUpdate()
	{
		using var scope = Gizmo.Scope( "DropObject", traceTransform );

		Gizmo.Draw.Color = Color.White.WithAlpha( 0.3f );
		Gizmo.Draw.LineBBox( Bounds );

		Gizmo.Draw.Color = Color.White;

		if ( model is not null )
		{
			var so = Gizmo.Draw.Model( model );
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

		if ( model is null )
			return;

		using var scene = SceneEditorSession.Scope();

		using ( SceneEditorSession.Active.UndoScope( "Drop Model" ).WithGameObjectCreations().Push() )
		{
			GameObject = new GameObject( false )
			{
				Name = model.ResourceName,
				WorldTransform = traceTransform
			};

			bool isProp = (model.Physics?.Parts.Count ?? 0) > 0;
			if ( isProp )
			{
				var prop = GameObject.Components.Create<Core.GameProp>();
				prop.Model = model;
				prop.IsStatic = archetype == "static_prop_model";

				Log.Info( prop.Model );
			}
			else if ( model.BoneCount > 0 )
			{
				var renderer = GameObject.Components.Create<SkinnedModelRenderer>();
				renderer.Model = model;
			}
			else
			{
				var renderer = GameObject.Components.Create<ModelRenderer>();
				renderer.Model = model;
			}

			GameObject.Enabled = true;

			EditorScene.Selection.Clear();
			EditorScene.Selection.Add( GameObject );
		}
	}
}
