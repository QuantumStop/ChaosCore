using System.Threading;
using System.Threading.Tasks;
using Application = Editor.Application;

[DropObject( "model", "vmdl", "vmdl_c"
#if IGNIS
, Priority = 100 
#endif
)]
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

		PackageStatus = "Loading Model";
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

		if ( model.IsValid() )
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

		if ( !model.IsValid() )
			return;

		using var scene = SceneEditorSession.Scope();

		using ( SceneEditorSession.Active.UndoScope( "Drop Model" ).WithGameObjectCreations().Push() )
		{
			GameObject = new GameObject( false )
			{
				Name = model.ResourceName,
				WorldTransform = traceTransform
			};

			bool IsPhysicsProp( Model m ) => (m.Physics?.Parts.Count ?? 0) > 0;
			bool IsRagdoll( Model m ) => m.BoneCount > 0 && m.Physics?.Joints.Count > 0 && IsPhysicsProp( m );
			bool IsSkinnedOnly( Model m ) => m.BoneCount > 0 && !IsPhysicsProp( m );

			if ( IsPhysicsProp( model ) && !IsRagdoll( model ) )
			{
				// Regular physics prop is GameProp
				var prop = GameObject.Components.Create<Core.GameProp>();
				prop.Model = model;
				prop.IsStatic = archetype == "static_prop_model";
			}
			else if ( IsSkinnedOnly( model ) || IsRagdoll( model ) )
			{
				// Skinned model with no physics is SkinnedModelRenderer
				var renderer = GameObject.Components.Create<SkinnedModelRenderer>();
				renderer.Model = model;
			}
			else
			{
				// Fallback to regular ModelRenderer
				var prop = GameObject.Components.Create<ModelRenderer>();
				prop.Model = model;
			}

			GameObject.Enabled = true;

			EditorScene.Selection.Clear();
			EditorScene.Selection.Add( GameObject );
		}
	}
}
