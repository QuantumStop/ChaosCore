namespace Core;
#if FMOD
#endif

[Title( "Video Player" )]
[Description( "Prop that specificaly lets you play all kinds of videos on a screen" )]
[Icon( "movie" )]

public partial class PropScreen : BaseEntity, Component.ExecuteInEditor
{
	protected override string GetEditorVis() => null;

	public enum VideoFX
	{
		None,
		Glitch,
		Distort
	}

	public enum VideoType
	{
		[Description( "The screen is using a video file" )]
		File,
		[Description( "The screen is using a live video transmission using .rtex" )]
		RenderTarget
	}
#if IGNIS
	[DebugExpose]
#endif
	[Property, Order( 0 ), Title( "Video Type:" )]
	public VideoType Type
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;

				if ( value == VideoType.RenderTarget )
					_videoPlayer = null;
				else
					_renderTarget = null;

				if ( Scene.IsEditor && value == VideoType.File ) PlayVideo();
			}
		}
	} = VideoType.File;

	[Space]
	[ShowIf( nameof( Type ), VideoType.RenderTarget )]
#if IGNIS
	[DebugExpose]
#endif
	[Property, Order( 0 ), Title( "Render Target: " ), WideMode]
	private RenderTextureAsset _renderTarget { get; set; }


	[Space]
	[ShowIf( nameof( Type ), VideoType.File )]
#if IGNIS
	[DebugExpose]
#endif
	[Property, Order( 1 ), Title( "Selected Video: " ), FilePath( Extension = "mp4" ), WideMode()]
	private string _videoPath
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				if ( Scene.IsEditor ) PlayVideo();
			}
		}
	} = "resource/videos/screen/editor.mp4";
	[Space]
#if IGNIS
	[DebugExpose]
#endif
	[Property, Order( 10 )] public VideoFX _VideoFX { get; set; }
	[ShowIf( nameof( Type ), VideoType.File )]
#if IGNIS
	[DebugExpose]
#endif
	[Property, Order( 10 )] public bool ShouldLoop { get; set; } = false;
	[ShowIf( nameof( Type ), VideoType.File )]
#if IGNIS
	[DebugExpose]
#endif
	[Property, Order( 10 )] public bool ShouldBeMuted { get; set; } = false;
#if IGNIS
	[DebugExpose]
#endif
	private ModelRenderer _modelRenderer { get; set; }
#if IGNIS
	[DebugExpose]
#endif
	[ReadOnly, Property, Order( 21 ), Feature( "Debug" )] private VideoPlayer _videoPlayer;
	[Property, Order( 22 ), Feature( "Cookie" )] private KelvinSpotLight _kelvinSpotLight { get; set; }
#if IGNIS
	[DebugExpose]
#endif
	[Property, FeatureEnabled( "Cookie" )] private bool _cookie { get; set; } = false;

	private const string _editorVideo = "resource/videos/screen/editor.mp4";

	public bool IsPlaying => _videoPlayer is not null && !_videoPlayer.IsPaused && _videoPlayer.Duration > 0;

	[Button, Order( 10 ), Feature( "Debug" )]
	private void CopyEditorTestVideoToGame()
	{
		if ( _videoPath != _editorVideo )
			_videoPath = _editorVideo;
		else
			Log.Warning( "It is already bizking the limp" );
	}

	[Button, Feature( "Debug" )]
	private void ResetRenderer()
	{
		if ( Components.TryGet<ModelRenderer>( out var renderer ) )
			_modelRenderer ??= renderer;

		//	if ( Scene.IsEditor ) SyncProceduralDebug();
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		ResetRenderer();

		if ( Scene.IsEditor ) PlayVideo();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		ClearVideo();
	}
	public void PlayVideo()
	{
		if ( Type == VideoType.File )
		{
			_videoPlayer ??= new VideoPlayer();

			if ( _videoPlayer is not null )
			{
				var whatvid = string.IsNullOrEmpty( _videoPath ) ? _editorVideo : _videoPath;
				_videoPlayer?.Play( FileSystem.Mounted, whatvid );

				_videoPlayer.Repeat = Scene.IsEditor || ShouldLoop;
#if FMOD
				_videoPlayer.Muted = Scene.IsEditor || UseFMODSound || ShouldBeMuted;
				if ( !Scene.IsEditor ) PlayFMODSound();
#else
				_videoPlayer.Muted = Scene.IsEditor || ShouldBeMuted;
#endif

				UpdateAttributeAndCookie();
			}
		}
	}

	[Button, Title( "Force Clear" ), Feature( "Debug" ), WideMode]
	public void ClearVideo()
	{
		_videoPlayer?.Stop();
		_videoPlayer?.Dispose();
		_kelvinSpotLight?.Cookie?.Dispose();
	}

	[Button, Title( "Force Start Video" ), Feature( "Debug" ), WideMode]
	public void ForceStart() => PlayVideo();

	protected override void OnUpdate()
	{
		if ( Type == VideoType.File ) _videoPlayer?.Present(); // null checking doesnt work i guess
		UpdateAttributeAndCookie( Type == VideoType.File );
	}

	private void UpdateAttributeAndCookie( bool isVideo = true )
	{
		if ( !IsPlaying && _cookie ) { _kelvinSpotLight?.Enabled = false; return; }

		_modelRenderer?.Attributes.Set( "ScreenTexture", isVideo ? _videoPlayer?.Texture : _renderTarget?.Texture );
		if ( _cookie ) _kelvinSpotLight?.Cookie = isVideo ? _videoPlayer?.Texture : _renderTarget?.Texture;
	}

	/*
		[ReadOnly, Property, Order( 20 ), Feature( "Debug" )] private ModelRenderer _rendererDebug { get; set; }

		private void SyncProceduralDebug()
		{
			_rendererDebug = null;

			if ( _modelRenderer.IsValid() )
				_rendererDebug = _modelRenderer;
		}
	*/
}
