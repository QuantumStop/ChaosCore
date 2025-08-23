using System;

namespace Core;

// i would rather ask for an actual resource
/*
/// <summary>
/// Resource that stores references to webm that we can play in the game.
/// </summary>
[GameResource( "WebM Video", "webm", $"A video that plays in {nameof(ScreenProp)} component.", Icon = "video_file" )]
public sealed class VideoWebMResource : GameResource
{
	// S&Box doesn't actually have a resource for videos, so I have to do this...
	// Otherwise there's nothing else to store. At most we could embed default effects here per vid
}*/

[Title( "Video Player" )]
[Description( "Prop that specificaly lets you play all kinds of videos on a screen" )]
[Icon( "movie" )]
public class ScreenProp : BaseEntity, Component.ExecuteInEditor
{
	protected override string GetEditorVis() { return null; }

	public enum VideoFX
	{
		None,
		Glitch,
		Distort

	}

	protected override void OnDirty()
	{
		base.OnDirty();
		PlayVideo();
	}
	[Space]
	[DebugExpose][Property, Order( 0 ), Title( "Selected Video: " ), MakeDirty, FilePath( Extension = "mp4" ), WideMode()] private string VideoPath { get; set; } = "resource/videos/screen/editor.mp4";
	[Space]
	[DebugExpose][Property, Order( 10 ), MakeDirty] public VideoFX _VideoFX { get; set; }
	[DebugExpose][Property, Order( 10 ), MakeDirty] public bool ShouldLoop { get; set; } = false;
	[DebugExpose][Property, Order( 10 ), MakeDirty] public bool ShouldBeMuted { get; set; } = false;
	[DebugExpose][ReadOnly, Property, Order( 20 ), Feature( "Debug" )] private ModelRenderer modelRenderer { get; set; }
	[DebugExpose][ReadOnly, Property, Order( 21 ), Feature( "Debug" )] private VideoPlayer _videoPlayer;
	[Property, Order( 22 ), Feature( "Cookie" )] private KelvinSpotLight _kelvinSpotLight { get; set; }
	[DebugExpose][Property, FeatureEnabled( "Cookie" )] private bool Cookie { get; set; } = false;

	string EditorVideo { get; set; } = "resource/videos/screen/editor.mp4";

	[Button, Order( 10 ), Feature( "Debug" )]
	private void CopyEditorTestVideoToGame()
	{
		if ( VideoPath != EditorVideo )
			VideoPath = EditorVideo;
		else
			Log.Warning( "It is already bizking the limp" );
	}

	protected override void OnStart()
	{
		base.OnStart();

		modelRenderer = Components.Get<ModelRenderer>();

		_videoPlayer = new VideoPlayer();

		if ( modelRenderer.IsValid() )
			modelRenderer.SceneObject.Attributes.Set( "ScreenTexture", _videoPlayer.Texture );

		if ( Cookie )
		{
			if ( _kelvinSpotLight.IsValid() )
				_kelvinSpotLight.Cookie = _videoPlayer.Texture;
		}

		PlayVideo();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		ClearVideo();
	}
	public void PlayVideo()
	{

		var whatvid = string.IsNullOrEmpty( VideoPath ) ? EditorVideo : VideoPath;

		if ( _videoPlayer != null )
		{
			_videoPlayer?.Play( FileSystem.Mounted, whatvid );

			_videoPlayer.Repeat = Game.IsPlaying ? ShouldLoop : true;
			_videoPlayer.Muted = Game.IsPlaying ? ShouldBeMuted : true;

			modelRenderer?.SceneObject.Attributes.Set( "ScreenTexture", _videoPlayer.Texture );

			if ( Cookie )
			{
				if ( _kelvinSpotLight.IsValid() )
					_kelvinSpotLight.Cookie = _videoPlayer.Texture;
			}
		}
	}

	public void ClearVideo( bool fullclear = true )
	{
		_videoPlayer?.Stop();
		_videoPlayer?.Dispose();
		_kelvinSpotLight?.Cookie.Dispose();
	}

	[Button, Title( "Force Start Video" ), Feature( "Debug" ), WideMode]
	public void ForceStart()
	{
		PlayVideo();
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( _videoPlayer != null )
		{
			_videoPlayer?.Present();

			if ( modelRenderer.IsValid() )
				modelRenderer?.SceneObject.Attributes.Set( "ScreenTexture", _videoPlayer.Texture );

			if ( Cookie )
			{
				if ( _kelvinSpotLight.IsValid() )
					_kelvinSpotLight.Cookie = _videoPlayer.Texture;
			}
		}
	}

}

[Title( "Video Player: Manager " )]
[Description( "This manager replaces all default props with props that are catered towards one's that play videos in a render target." )]
[Icon( "movie" )]
public class videoplayer_manager : BaseEntity, Component.ExecuteInEditor
{
	[Property, Title( "Play All On Start" ), MakeDirty]
	[Description( "True by default. Plays all videos on start as long as they're not disabled on start. If this is set to false videos will only play when you tell em to manually." )]
	public bool b_PlayOnStart { get; set; } = true;

	[Property, Title( "Max Volume" ), Range( 0, 1 ), MakeDirty]
	[Description( "Clamping max producable volume from these videos if there is any." )]
	public float GlobalVolume { get; set; } = 1.0f;

	// this needs to be changed to accomodate the non-Prop class solution, as ScreenProp is now a regular component
	/*
		[Button, Title( "Replace Old Props with a new specialized prop" )]
		[Description( "Replaces all assets that have a screen tag" )]
		public void ReplaceLegacy()
		{
			var legacyProps    = Scene.Components.GetAll<ModelRenderer>();
			var newCameraProps = Scene.Components.GetAll<ScreenProp>();

			foreach ( var obj in legacyProps )
			{
				if ( obj.Tags.Has( "screen" ) && !newCameraProps.Contains( obj ) )
				{
					var enhancer = obj.AddComponent<ScreenProp>();

					// Well now we don't need the old prop component, bye.
					obj.Destroy();
				}
			}
		}
	*/

}
