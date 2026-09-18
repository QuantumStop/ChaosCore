namespace Editor.Assets;

using System;
using System.Threading.Tasks;

using Core;
using Core.Editor;

[AssetPreview( "preset" )]
public sealed class GenericPresetAssetPreview( Asset asset ) : AssetPreview( asset )
{
	private const float _spriteSize = 16.0f;

	public override float PreviewWidgetCycleSpeed => 0.0f;

	private bool _isFallbackPreview;

	public override Task InitializeAsset()
	{
		var preset = Asset.LoadResource<GenericPresetResource>();

		if ( preset is null )
			return Task.CompletedTask;

		// Unique preset preview providers always win!

		var provider = PresetService.GetPreviewProvider( preset );

		if ( provider is not null )
		{
			using ( Scene.Push() )
			{
				if ( provider.BuildAssetPreview(
					preset,
					Camera,
					out var primaryObject,
					out var sceneCenter,
					out var sceneSize
				) )
				{
					PrimaryObject = primaryObject;
					SceneCenter = sceneCenter;
					SceneSize = sceneSize;

					return Task.CompletedTask;
				}
			}
		}

		// BaseEntity targets may provide an EditorVis.

		if ( TryBuildEditorVisFallback( preset ) )
			return Task.CompletedTask;


		// Component or target icon

		if ( TryBuildIconFallback( preset ) )
			return Task.CompletedTask;

		// Nothing else to render, asset type fallback.

		return Task.CompletedTask;
	}

	private bool TryBuildEditorVisFallback( GenericPresetResource preset )
	{
		var targetInfo = PresetRegistry.Find( preset.PresetType );

		if ( targetInfo?.RuntimeType is null )
			return false;

		if ( !typeof( BaseEntity ).IsAssignableFrom( targetInfo.RuntimeType ) )
			return false;

		var target = PresetService.CreateEditableTarget(
			preset,
			out var temporaryGameObject
		);

		try
		{
			if ( target is not BaseEntity entity )
				return false;

			var editorVis = entity.EditorVis;

			if ( string.IsNullOrWhiteSpace( editorVis ) )
				return false;

			if ( !FileSystem.Mounted.FileExists( editorVis ) &&
				!FileSystem.Mounted.FileExists( editorVis + "_c" ) )
			{
				return false;
			}

			using ( Scene.Push() )
			{
				if ( editorVis.EndsWith(
					".vmdl",
					StringComparison.OrdinalIgnoreCase
				) )
				{
					return BuildModelFallback( editorVis );
				}

				return BuildTextureFallback( editorVis );
			}
		}
		finally
		{
			temporaryGameObject?.Destroy();
		}
	}

	private bool BuildModelFallback( string path )
	{
		var model = Model.Load( path );

		if ( !model.IsValid() )
			return false;

		PrimaryObject = new GameObject( true, "preset_preview_model" );

		var renderer = PrimaryObject.AddComponent<ModelRenderer>();

		renderer.Model = model;

		// We're keeping this dim.

		renderer.Tint = Color.White * 0.45f;

		PrimaryObject.WorldTransform = Transform.Zero;

		SceneCenter = model.Bounds.Center;
		SceneSize = model.Bounds.Size;

		_isFallbackPreview = true;

		AddFallbackBadge(
			SceneCenter,
			SceneSize
		);

		return true;
	}

	private bool BuildTextureFallback( string path )
	{
		var texture = Texture.Load( path );

		if ( !texture.IsValid() )
			return false;

		PrimaryObject = new GameObject( true, "preset_preview_sprite" )
		{
			WorldTransform = Transform.Zero
		};

		var renderer = PrimaryObject.AddComponent<SpriteRenderer>();

		renderer.Sprite = new Sprite
		{
			Animations =
			[
				new()
				{
					Name = "Default",
					Frames =
					[
						new Sprite.Frame
						{
							Texture = texture
						}
					]
				}
			]
		};

		renderer.Size = new Vector2( _spriteSize / 1.5f, _spriteSize / 1.5f );

		// Visually dim also!
		renderer.Color = Color.White.WithAlpha( 0.15f );

		Camera.Orthographic = true;
		Camera.OrthographicHeight = _spriteSize;

		SceneCenter = Vector3.Zero;

		SceneSize = new Vector3( _spriteSize, _spriteSize, 1.0f );

		_isFallbackPreview = true;

		AddFallbackBadge(
			SceneCenter,
			SceneSize
		);

		return true;
	}

	private void AddFallbackBadge( Vector3 sceneCenter, Vector3 sceneSize )
	{
		if ( !_isFallbackPreview )
			return;

		var badgeObject = new GameObject( true, "preset_resource_badge" );

		var verticalOffset = MathF.Max( sceneSize.z * 0.15f, 2.0f );
		var text = badgeObject.AddComponent<TextRenderer>();

		// TODO: Yuck, we'll do tihs better later
//		badgeObject.WorldPosition = sceneCenter * verticalOffset;

		var scope = new TextRendering.Scope
		{
			Text = "Preset*",
			FontName = "RobotoMono",
			FontSize = 3f,
			FontWeight = 600,
			TextColor = Color.White.WithAlpha(1f),
			LineHeight = 0.85f,
		};

		scope.Outline.Enabled = true;
		scope.Outline.Color = Color.Black;
		scope.Outline.Size = 0.5f;

		text.HorizontalAlignment = TextRenderer.HAlignment.Center;
		text.VerticalAlignment = TextRenderer.VAlignment.Center;
		
		text.Billboard = TextRenderer.BillboardMode.Always;

		text.FogStrength = 0.0f;
		text.TextScope = scope;
	}

	private bool TryBuildIconFallback( GenericPresetResource preset )
	{
		var targetInfo = PresetRegistry.Find( preset.PresetType );

		if ( targetInfo is null )
			return false;

		var icon = targetInfo.Icon;

		if ( string.IsNullOrWhiteSpace( icon ) )
			return false;

		return BuildMaterialIconFallback( icon );
	}

	private bool BuildMaterialIconFallback( string icon )
	{
		// No custom scene is built here yet.
		// Returning false allows us to use the default
		// asset type icon instead.

		return false;
	}
}
