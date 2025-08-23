using System.Threading.Tasks;
using Core;
namespace Editor.Assets;

[AssetPreview( "amn" )]
class PreviewAmmo : AssetPreview
{
	public override float PreviewWidgetCycleSpeed => 0.2f;

	ModelRenderer modelRenderer;

	public PreviewAmmo( Asset asset ) : base( asset )
	{

	}

	/// <summary>
	/// Create the model or whatever needs to be viewed
	/// </summary>
	public override async Task InitializeAsset()
	{
		using ( EditorUtility.DisableTextureStreaming() )
		{
			var model = await Model.LoadAsync( Asset.LoadResource<AmmoInfo>().AmmoModel.ResourcePath );
			if ( model is null ) return;

			using ( Scene.Push() )
			{
				SceneCenter = model.RenderBounds.Center;
				SceneSize = Vector3.Zero;

				if ( model.MeshCount == 0 )
					return;

				PrimaryObject = new GameObject( true, "preview weapon" );
				PrimaryObject.WorldTransform = Transform.Zero;

				var tonemap = Camera.AddComponent<Tonemapper>();

				modelRenderer = PrimaryObject.AddComponent<ModelRenderer>();
				modelRenderer.Model = model;

				SceneSize = model.Bounds.Size;
				SceneCenter = model.Bounds.Center;
			}
		}
	}

}
