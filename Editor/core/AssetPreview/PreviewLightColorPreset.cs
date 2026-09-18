namespace Editor.Assets;

using Core;
using Core.Editor;
using System.Threading.Tasks;
using System;

[PresetPreviewFor( typeof( KelvinDirectionalLight ) )]
[PresetPreviewFor( typeof( KelvinPointLight ) )]
[PresetPreviewFor( typeof( KelvinSpotLight ) )]
public sealed class KelvinLightPresetPreview : IPresetPreviewProvider
{
	public async Task<Pixmap> RenderAsync( GenericPresetResource preset )
	{
		var target = CreatePreviewTarget( preset, out var temporaryGameObject );

		if ( target is null )
			return null;

		try
		{
			return await KelvinLightPresetPreviewScene.RenderAsync( target );
		}
		finally
		{
			temporaryGameObject?.Destroy();
		}
	}

	public bool BuildAssetPreview( GenericPresetResource preset, CameraComponent camera, out GameObject primaryObject, 
		out Vector3 sceneCenter, out Vector3 sceneSize )
	{
		primaryObject = null;
		sceneCenter = default;
		sceneSize = default;

		if ( preset is null || camera is null )
			return false;

		var target = CreatePreviewTarget( preset, out var temporaryGameObject );

		if ( target is null )
			return false;

		try
		{
			return KelvinLightPresetPreviewScene.Build( camera, target, out primaryObject, out sceneCenter, out sceneSize );
		}
		finally
		{
			temporaryGameObject?.Destroy();
		}
	}

	private static object CreatePreviewTarget( GenericPresetResource preset, out GameObject temporaryGameObject )
	{
		temporaryGameObject = null;

		if ( preset is null )
			return null;

		return PresetService.CreateEditableTarget( preset, out temporaryGameObject );
	}
}

internal static class KelvinLightPresetPreviewScene
{
	private static readonly Model _plane = Model.Load( "models/dev/plane.vmdl" );
	private static readonly Model _probe = Model.Load( "models/editor/env_cubemap.vmdl" );
	private static readonly Material _previewMaterial = Material.Load( "materials/dev/dev_measuregeneric01b.vmat" );
	private static readonly Texture _environmentTexture = Texture.Load( "textures/cubemaps/default2.vtex" );
	private static readonly Material _skyMaterial = Material.Load( "materials/dev/skybox.vmat" );

	public static async Task<Pixmap> RenderAsync( object light )
	{
		if ( light is null )
			return null;

		var scene = Scene.CreateEditorScene();
		scene.Name = "Light Preset Preview";

		try
		{
			using ( scene.Push() )
			{
				var cameraObject = new GameObject( true, "camera" );
				var camera = cameraObject.AddComponent<CameraComponent>();

				if ( !Build( camera, light, out _, out _, out _ ) )
					return null;

				scene.EditorTick( 0.0f, 0.0f );
			}

			await Task.Delay( 1 );

			var pixmap = new Pixmap( 128, 128 );
			scene.Camera.RenderToPixmap( pixmap );

			return pixmap;
		}
		finally
		{
			scene.Destroy();
		}
	}

	public static bool Build( CameraComponent camera, object light, out GameObject primaryObject, out Vector3 sceneCenter, out Vector3 sceneSize )
	{
		primaryObject = null;
		sceneCenter = default;
		sceneSize = default;

		if ( camera is null || light is null )
			return false;

		SetupCamera( camera );

		return light switch
		{
			KelvinDirectionalLight directional => BuildDirectional(
				directional,
				out primaryObject,
				out sceneCenter,
				out sceneSize
			),

			KelvinPointLight point => BuildPoint(
				point,
				out primaryObject,
				out sceneCenter,
				out sceneSize
			),

			KelvinSpotLight spot => BuildSpot(
				spot,
				out primaryObject,
				out sceneCenter,
				out sceneSize
			),

			_ => false
		};
	}

	private static void SetupCamera( CameraComponent camera )
	{
		camera.BackgroundColor = Color.Transparent;
		camera.FieldOfView = 32.0f;
		camera.ZNear = 1.0f;
		camera.ZFar = 100000.0f;
		camera.WorldPosition = new Vector3( -72, -96, 48 );
		camera.WorldRotation = Rotation.LookAt( Vector3.Up * 18.0f - camera.WorldPosition );
	}

	private static bool BuildDirectional( KelvinDirectionalLight source, out GameObject primaryObject, out Vector3 sceneCenter, out Vector3 sceneSize )
	{
		CreateAmbient( 0.08f );
		CreateSky();
		CreateGround( 6.0f );
		CreateEnvironment( Color.White * 0.25f );

		var lightObject = new GameObject( true, "preview_directional_light" );
		var light = lightObject.AddComponent<DirectionalLight>();

		ApplyCommonSettings( light, source );

		light.WorldRotation = new Angles( 48, 35, 0 );
		light.ShadowCascadeCount = source.ShadowCascadeCount;
		light.ShadowCascadeSplitRatio = source.ShadowCascadeSplitRatio;

		primaryObject = CreateProbe();
		primaryObject.WorldPosition = Vector3.Up * 18.0f;
		primaryObject.WorldScale = 1.25f;

		sceneCenter = Vector3.Up * 18.0f;
		sceneSize = new Vector3( 96, 96, 64 );

		return true;
	}

	private static bool BuildPoint( KelvinPointLight source, out GameObject primaryObject, out Vector3 sceneCenter, out Vector3 sceneSize )
	{
		CreateAmbient( 0.025f );
		CreateGround( 5.0f );
		CreateBackdropWall();
		CreateEnvironment( Color.White * 0.08f );

		var lightObject = new GameObject( true, "preview_point_light" );
		var light = lightObject.AddComponent<PointLight>();

		ApplyCommonSettings( light, source );

		light.WorldPosition = new Vector3( -8, -10, 34 );
		light.Radius = MathF.Max( source.Radius, 1.0f );
		light.Attenuation = source.Attenuation;

		primaryObject = CreateProbe();
		primaryObject.WorldPosition = Vector3.Up * 18.0f;
		primaryObject.WorldScale = 1.2f;

		sceneCenter = new Vector3( 0, 10, 18 );
		sceneSize = new Vector3( 88, 88, 64 );

		return true;
	}

	private static bool BuildSpot( KelvinSpotLight source, out GameObject primaryObject, out Vector3 sceneCenter, out Vector3 sceneSize )
	{
		CreateAmbient( 0.02f );
		CreateGround( 5.0f );
		CreateBackdropWall();
		CreateEnvironment( Color.White * 0.08f );

		var lightObject = new GameObject( true, "preview_spot_light" );
		var light = lightObject.AddComponent<SpotLight>();

		ApplyCommonSettings( light, source );

		light.WorldPosition = new Vector3( -10, -18, 36 );
		light.WorldRotation = Rotation.LookAt( Vector3.Up * 18.0f - light.WorldPosition );
		light.Radius = MathF.Max( source.Radius, 1.0f );
		light.Attenuation = source.Attenuation;
		light.ConeInner = source.ConeInner;
		light.ConeOuter = source.ConeOuter;
		light.Cookie = source.Cookie;

		primaryObject = CreateProbe();
		primaryObject.WorldPosition = Vector3.Up * 18.0f;
		primaryObject.WorldScale = 1.2f;

		sceneCenter = new Vector3( 0, 10, 18 );
		sceneSize = new Vector3( 88, 88, 64 );

		return true;
	}

	private static void ApplyCommonSettings( Light preview, Light source )
	{
		preview.LightColor = source.LightColor;
		preview.Contribution = source.Contribution;
		preview.FogMode = source.FogMode;
		preview.FogStrength = source.FogStrength;
		preview.Shadows = source.Shadows;
		preview.ShadowBias = source.ShadowBias;
		preview.ShadowHardness = source.ShadowHardness;
	}

	private static void CreateAmbient( float intensity )
	{
		var ambientObject = new GameObject( true, "ambient" );
		ambientObject.AddComponent<AmbientLight>().Color = Color.White * intensity;
	}

	private static void CreateEnvironment( Color tint )
	{
		var envObject = new GameObject( true, "envmap" );
		var env = envObject.AddComponent<EnvmapProbe>();

		env.Mode = EnvmapProbe.EnvmapProbeMode.CustomTexture;
		env.Texture = _environmentTexture;
		env.TintColor = tint;
		env.Bounds = BBox.FromPositionAndSize( Vector3.Zero, 100000 );
	}

	private static void CreateSky()
	{
		if ( !_skyMaterial.IsValid() )
			return;

		var skyObject = new GameObject( true, "skybox" );
		skyObject.AddComponent<SkyBox2D>().SkyMaterial = _skyMaterial;
	}

	private static void CreateGround( float scale )
	{
		var groundObject = new GameObject( true, "ground" );
		var ground = groundObject.AddComponent<ModelRenderer>();

		ground.Model = _plane;
		ground.MaterialOverride = _previewMaterial;
		ground.Tint = Color.White;

		groundObject.WorldScale = scale;
	}

	private static void CreateBackdropWall()
	{
		var wallObject = new GameObject( true, "wall" );
		var wall = wallObject.AddComponent<ModelRenderer>();

		wall.Model = _plane;
		wall.MaterialOverride = _previewMaterial;
		wall.Tint = Color.White.WithAlpha( 0.9f );

		wallObject.WorldPosition = new Vector3( 0, 40, 24 );
		wallObject.WorldRotation = Rotation.From( new Angles( 90, 0, 0 ) );
		wallObject.WorldScale = new Vector3( 5.0f, 1.0f, 3.0f );
	}

	private static GameObject CreateProbe()
	{
		var probeObject = new GameObject( true, "probe" );
		var probe = probeObject.AddComponent<ModelRenderer>();

		probe.Model = _probe;
		probe.Tint = Color.White;

		return probeObject;
	}
}
