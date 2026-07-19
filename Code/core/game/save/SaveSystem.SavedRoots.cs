#if IGNIS || STANDALONE
namespace Core;

using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

public sealed partial class SaveSystem
{
	private static JsonArray _stagedSavedRootsForSceneLoad;
	private static bool _isRestoringSave;
	private readonly Dictionary<string, JsonObject> _pendingSavedRoots = [];
	private bool _pendingSavedRootsApplyQueued;

	public static bool HasPendingSavedRoots => Current?._pendingSavedRoots.Count > 0 || _stagedSavedRootsForSceneLoad is not null;
	public static bool IsRestoringSave => _isRestoringSave;

	public static bool HasStagedSavedRoot( string key )
	{
		if ( string.IsNullOrWhiteSpace( key ) || _stagedSavedRootsForSceneLoad is null )
			return false;

		foreach ( var rootNode in _stagedSavedRootsForSceneLoad )
		{
			if ( rootNode is JsonObject rootData &&
				 string.Equals( rootData["Key"]?.ToString(), key, StringComparison.Ordinal ) )
				return true;
		}

		return false;
	}

	private JsonArray CollectSavedRoots()
	{
		var roots = new JsonArray();
		var seenKeys = new HashSet<string>();

		// Saved roots are runtime trees we skip from scene patching, like the local player.
		foreach ( var root in Scene.GetAllComponents<BaseCustomSerialize>().OfType<ISaveRoot>() )
		{
			if ( string.IsNullOrWhiteSpace( root.SaveRootKey ) || !seenKeys.Add( root.SaveRootKey ) )
				continue;

			var gameObject = root.SaveRootObject;
			if ( !gameObject.IsValid() )
				continue;

			root.BeforeSaveRoot();

			roots.Add( new JsonObject
			{
				["Key"] = root.SaveRootKey,
				["Object"] = gameObject.Serialize(),
				["CustomComponentData"] = CollectCustomComponentData( gameObject )
			} );
		}

		return roots;
	}

	private void RestoreSavedRoots( JsonArray roots )
	{
		_pendingSavedRoots.Clear();
		_stagedSavedRootsForSceneLoad = null;
		_isRestoringSave = false;

		if ( roots is null )
			return;

		foreach ( var rootNode in roots )
		{
			if ( rootNode is not JsonObject rootData )
				continue;

			var key = rootData["Key"]?.ToString();
			if ( string.IsNullOrWhiteSpace( key ) )
				continue;

			_pendingSavedRoots[key] = rootData.DeepClone().AsObject();
		}

		// Saved roots are full runtime trees. Replacing placeholders avoids nested duplicate children.
		DestroyLiveSavedRoots();
		CreateMissingSavedRoots();
		QueuePendingSavedRootsApply();
	}

	private static void StageSavedRootsForSceneLoad( JsonArray roots )
	{
		// This is read during scene startup, before the new SaveSystem instance restores the data.
		_isRestoringSave = true;
		_stagedSavedRootsForSceneLoad = roots?.DeepClone() as JsonArray;
	}

	private void DestroyLiveSavedRoots()
	{
		if ( _pendingSavedRoots.Count == 0 )
			return;

		foreach ( var root in Scene.GetAllComponents<BaseCustomSerialize>().OfType<ISaveRoot>().ToArray() )
		{
			if ( !_pendingSavedRoots.ContainsKey( root.SaveRootKey ) )
				continue;

			var gameObject = root.SaveRootObject;
			if ( !gameObject.IsValid() )
				continue;

			gameObject.Enabled = false;
			gameObject.Destroy();
		}
	}

	private void CreateMissingSavedRoots()
	{
		if ( _pendingSavedRoots.Count == 0 )
			return;

		foreach ( var (key, data) in _pendingSavedRoots.ToArray() )
		{
			if ( data["Object"] is not JsonObject objectState )
				continue;

			// Create a fresh root instead of deserializing over a prefab that already ran OnStart.
			var gameObject = Scene.CreateObject();
			gameObject.Deserialize( objectState );

			var root = GetComponentsInTree<BaseCustomSerialize>( gameObject ).OfType<ISaveRoot>().FirstOrDefault();
			if ( root is null )
				continue;

			RestoreCustomComponentData( gameObject, data["CustomComponentData"] as JsonArray );
			root.AfterLoadRoot();
			_pendingSavedRoots.Remove( key );
		}
	}

	private void QueuePendingSavedRootsApply()
	{
		if ( _pendingSavedRoots.Count == 0 || _pendingSavedRootsApplyQueued )
			return;

		_ = ApplyPendingSavedRootsDeferred();
	}

	private async Task ApplyPendingSavedRootsDeferred()
	{
		_pendingSavedRootsApplyQueued = true;

		try
		{
			for ( var attempt = 0; attempt < 8 && _pendingSavedRoots.Count > 0; attempt++ )
			{
				CreateMissingSavedRoots();

				if ( _pendingSavedRoots.Count == 0 )
					break;

				await GameTask.Delay( 1 );
			}

			if ( _pendingSavedRoots.Count > 0 )
				Log.Warning( $"Saved root restore is still pending for: {string.Join( ", ", _pendingSavedRoots.Keys )}" );
		}
		catch ( Exception e )
		{
			Log.Error( $"Deferred saved root restore failed: {e}" );
		}

		_pendingSavedRootsApplyQueued = false;
	}

	private static JsonArray CollectCustomComponentData( GameObject root )
	{
		var customData = new JsonArray();

		foreach ( var component in GetComponentsInTree<BaseCustomSerialize>( root ) )
			customData.Add( component.CustomSerialize() );

		return customData;
	}

	private void RestoreCustomComponentData( GameObject root, JsonArray customData )
	{
		if ( customData is null )
			return;

		var components = GetComponentsInTree<BaseCustomSerialize>( root ).ToArray();

		foreach ( var node in customData )
		{
			if ( node is not JsonObject componentData )
				continue;

			var guid = componentData["SerializedGuid"]?.ToString();
			if ( string.IsNullOrWhiteSpace( guid ) )
				continue;

			var component = components.FirstOrDefault( x => x.SerializedGuid == guid || x.Id.ToString() == guid );
			component?.CustomDeserialize( componentData );
		}
	}

	private static IEnumerable<T> GetComponentsInTree<T>( GameObject root ) where T : Component
	{
		if ( !root.IsValid() )
			yield break;

		foreach ( var component in root.Components.GetAll<T>() )
			yield return component;

		foreach ( var child in root.Children )
		{
			foreach ( var component in GetComponentsInTree<T>( child ) )
				yield return component;
		}
	}
}
#endif
