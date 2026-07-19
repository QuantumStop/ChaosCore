#if IGNIS || STANDALONE
namespace Core;

using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public sealed partial class SaveSystem
{
	private static string _explicitPrimarySceneSource;

	void ISceneLoadingEvents.BeforeLoad( Scene scene, SceneLoadOptions options )
	{
		var sceneFile = options.GetSceneFile();
		if ( sceneFile is null )
			return;

		if ( !options.IsAdditive )
		{
			_loadedScenes.Clear();
			_metadata.Clear();
			LoadedSavePath = null;
			ClearTransientDebugData();
		}

		TrackSceneSource( sceneFile, scene );
	}

	Task ISceneLoadingEvents.OnLoad( Scene scene, SceneLoadOptions options, LoadingContext context )
	{
		if ( _suppressSystemScene )
		{
			scene.WantsSystemScene = false;
			_suppressSystemScene = false;
		}

		return Task.CompletedTask;
	}

	private void EnsureLoadedSceneSourceTracked()
	{
		if ( !string.IsNullOrWhiteSpace( _explicitPrimarySceneSource ) )
		{
			var explicitScene = ResourceLibrary.Get<SceneFile>( _explicitPrimarySceneSource );
			if ( explicitScene.IsValid() )
			{
				_loadedScenes.Clear();
				ClearTransientDebugData();
				TrackSceneSource( explicitScene, Scene );
				return;
			}
		}

		if ( _loadedScenes.Count > 0 )
			return;

		if ( Scene.Source is SceneFile sceneFile )
			TrackSceneSource( sceneFile, Scene );
	}

	public static void RememberPrimarySceneSource( SceneFile sceneFile )
	{
		if ( !sceneFile.IsValid() || string.IsNullOrWhiteSpace( sceneFile.ResourcePath ) )
			return;

		_explicitPrimarySceneSource = sceneFile.ResourcePath;
		Current?.SetPrimarySceneSource( sceneFile );
	}

	public void SetPrimarySceneSource( SceneFile sceneFile )
	{
		if ( !sceneFile.IsValid() )
			return;

		if ( !string.IsNullOrWhiteSpace( sceneFile.ResourcePath ) )
			_explicitPrimarySceneSource = sceneFile.ResourcePath;

		_loadedScenes.Clear();
		ClearTransientDebugData();
		TrackSceneSource( sceneFile, Scene );
	}

	private void TrackSceneSource( SceneFile sceneFile, Scene scene )
	{
		var resourcePath = sceneFile.ResourcePath;
		if ( string.IsNullOrWhiteSpace( resourcePath ) && scene.Source is SceneFile sourceFile )
			resourcePath = sourceFile.ResourcePath;

		if ( string.IsNullOrWhiteSpace( resourcePath ) && sceneFile.Id != Guid.Empty )
		{
			var match = ResourceLibrary.GetAll<SceneFile>()
				.FirstOrDefault( x => x.Id == sceneFile.Id && !string.IsNullOrWhiteSpace( x.ResourcePath ) );

			if ( match is not null )
				resourcePath = match.ResourcePath;
		}

		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return;

		if ( _loadedScenes.Any( x => string.Equals( x.ResourcePath, resourcePath, StringComparison.OrdinalIgnoreCase ) ) )
			return;

		_loadedScenes.Add( new LoadedSceneEntry
		{
			ResourcePath = resourcePath,
			SceneFileId = sceneFile.Id
		} );
	}

	private SceneFile GetPrimarySceneFile()
	{
		if ( _loadedScenes.Count == 0 )
			return null;

		return ResourceLibrary.Get<SceneFile>( _loadedScenes[0].ResourcePath );
	}

	private static List<SceneFile> ResolveSceneSources( JsonArray sceneSources )
	{
		var sceneFiles = new List<SceneFile>();
		if ( sceneSources is null )
			return sceneFiles;

		foreach ( var node in sceneSources )
		{
			var source = node?.ToString();
			if ( string.IsNullOrWhiteSpace( source ) )
				continue;

			var sceneFile = ResourceLibrary.Get<SceneFile>( source );
			if ( sceneFile is null )
			{
				Log.Warning( $"Scene source '{source}' was not found." );
				continue;
			}

			sceneFiles.Add( sceneFile );
		}

		return sceneFiles;
	}

	private sealed class LoadedSceneEntry
	{
		public string ResourcePath { get; init; }
		public Guid SceneFileId { get; init; }
	}
}
#endif
