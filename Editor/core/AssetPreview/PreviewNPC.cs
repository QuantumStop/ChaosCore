using System.Threading.Tasks;
using Core.AI;
using Core;
namespace Editor.Assets;

[AssetPreview( "npc" )]
class PreviewNPC( Asset asset ) : AssetPreview( asset )
{
	public override float PreviewWidgetCycleSpeed => 0.2f;

	SkinnedModelRenderer modelRenderer;

	/// <summary>
	/// Create the model or whatever needs to be viewed
	/// </summary>
	public override async Task InitializeAsset()
	{
		using ( EditorUtility.DisableTextureStreaming() )
		{
			var model = await Model.LoadAsync( Asset.LoadResource<NpcDefinition>().Models?.FirstOrDefault()?.ResourcePath );
			if ( !model.IsValid() ) return;

			using ( Scene.Push() )
			{
				SceneCenter = model.RenderBounds.Center;
				SceneSize = Vector3.Zero;

				if ( model.MeshCount == 0 )
					return;

				PrimaryObject = new GameObject( true, "preview npc" ) { WorldTransform = Transform.Zero };

				var tonemap = Camera.AddComponent<Tonemapper>().Mode = Tonemapper.TonemappingMode.Saturated;

				modelRenderer = PrimaryObject.AddComponent<SkinnedModelRenderer>();
				modelRenderer.Model = model;

				SceneSize = model.Bounds.Size;
				SceneCenter = model.Bounds.Center;
			}
		}
	}

}
