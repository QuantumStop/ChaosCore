namespace Core.Editor;

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

public sealed partial class ResourceEditor
{
	private static ResourceEditorType[] _types => ResourceCatalog.Resources;

	/// <summary>
	/// Creates an allowed resource instance using its own constructor and property defaults.
	/// </summary>
	internal static GameResource CreateDefault( AssetType type )
	{
		if ( !ResourceAccess.Allows( type.ResourceType ) )
			throw new InvalidOperationException( "This resource type is excluded from Resource Editor." );

		return EditorTypeLibrary.GetType( type.ResourceType )?.Create<GameResource>()
			?? throw new InvalidOperationException( $"Unable to create {type.FriendlyName}." );
	}

	private static bool HasEditableSource( Asset asset ) => asset?.AssetType is not null && !string.IsNullOrWhiteSpace( asset.AbsolutePath )
		&& File.Exists( asset.AbsolutePath ) && string.Equals( Path.GetExtension( asset.AbsolutePath ), 
		"." + asset.AssetType.FileExtension, StringComparison.OrdinalIgnoreCase )
		&& ResourceAccess.Allows( asset.AssetType.ResourceType, asset.AbsolutePath );

	/// <summary>
	/// A resource and it's pending edits. 
	/// New entries have no backing asset until saved.
	/// </summary>
	private sealed class Entry
	{
		public Asset Asset;
		public string Path;
		public ResourceEditorType Type;
		public GameResource Resource;
		public bool Dirty;
		public bool Removed;
		public string Original;
		
		/// <summary>
		/// Serialized values after loading or saving, used to detect actual resource edits.
		/// </summary>
		public JsonObject SavedValues;

		public string Name => System.IO.Path.GetFileNameWithoutExtension( Path );
	}

	private static bool AllowsEntry( Entry entry ) => entry is not null && ResourceAccess.Allows( entry.Resource?.GetType()
		?? entry.Asset?.AssetType?.ResourceType
		?? entry.Type.ResourceType, entry.Path );

	[Event( "asset.contextmenu" )]
	private static void OnAssetContextMenu( AssetContextMenu context )
	{
		if ( context.SelectedList?.Count != 1 )
			return;

		var asset = context.SelectedList[0].Asset;

		if ( !HasEditableSource( asset ) || !asset.AssetType.IsGameResource )
			return;

		context.Menu.AddOption( "Open in Resource Editor", "dataset", () => OpenAsset( asset ) );
	}

	private ResourceTypeGroup[] _browserGroups = [];
	private readonly Dictionary<string, List<Entry>> _entriesByType = new( StringComparer.OrdinalIgnoreCase );
	private static bool _reloadAfterHotload;

	[EditorEvent.Hotload]
	private static void QueueAssetRefresh() => _reloadAfterHotload = true;

	/// <summary>
	/// Refreshes cached types and resources after hotload, only while the editor is open.
	/// </summary>
	[EditorEvent.Frame]
	private static void RefreshAfterHotload()
	{
		if ( !_reloadAfterHotload )
			return;

		_reloadAfterHotload = false;

		// We don't invalidate unless our editor is present and we reload it immediately if it does,
		// as this is just safer to get any change coming our way from resources additions/edits.
		var editor = All.OfType<ResourceEditor>().FirstOrDefault( window => window.IsValid );
		if ( editor is null )
			return;

		ResourceCatalog.Invalidate();
		editor.Reload();
	}

	/// <summary>
	/// Refreshes resource discovery and the outliner while retaining staged edits.
	/// </summary>
	public void Reload()
	{
		if ( _inspectorPanel?.IsValid != true )
			return;

		if ( _current is not null && !AllowsEntry( _current ) )
			ClearSelection();

		var paths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		
		foreach ( var entry in _entries )
		{ 
			paths.Add( entry.Path );
		}

		foreach ( var asset in AssetSystem.All )
		{
			var type = ResourceCatalog.FindResource( asset.AssetType?.FileExtension );
			if ( type is null || !HasEditableSource( asset ) || !paths.Add( asset.AbsolutePath ) )
				continue;

			_entries.Add( new Entry { Asset = asset, Path = asset.AbsolutePath, Type = type } );
		}

		_entriesByType.Clear();
		
		foreach ( var entry in _entries )
		{
			var type = ResourceCatalog.FindResource( entry.Type.Extension );
			if ( type is null || !AllowsEntry( entry ) )
				continue;

			entry.Type = type;
		
			if ( !_entriesByType.TryGetValue( type.Extension, out var entries ) )
				_entriesByType[type.Extension] = entries = [];
				
			entries.Add( entry );
		}

		foreach ( var entries in _entriesByType.Values )
		{
			entries.Sort( ( left, right ) => string.Compare( left.Name, right.Name, StringComparison.CurrentCulture ) );
		}

		_browserGroups = ResourceOrganizationRegistry.BuildGroups( _types );
		_linkCache.Clear();
		BuildBrowser();
	}

	private void BuildBrowser()
	{
		if ( _tree?.IsValid != true )
		{
			UpdateTitle();
			return;
		}

		_tree.Clear();
		_browserNodes.Clear();
		
		var query = _search.Text?.Trim() ?? "";
		var groups = _browserGroups.ToLookup( group => group.Owner );
		
		foreach ( var owner in new[] { "Core", Project.Current?.Config?.Title ?? "Game" } )
		{
			var ownerNode = new BrowserGroupNode( this, "owner:" + owner, owner, owner == "Core" ? "layers" : "sports_esports", [], projectIcon: owner != "Core" );
			var matchOwner = owner.Contains( query, StringComparison.OrdinalIgnoreCase );
			
			foreach ( var bucket in groups[owner] )
			{
				var definition = bucket.Definition;
				var matchGroup = matchOwner || definition.Name.Contains( query, StringComparison.OrdinalIgnoreCase );
				var section = new BrowserGroupNode( this, "group:" + owner + ":" + definition.Key, definition.Name, definition.Icon, bucket.Types.ToArray() );
				
				foreach ( var type in bucket.Types )
				{
					var matchType = matchGroup || type.Name.Contains( query, StringComparison.OrdinalIgnoreCase );
					var assets = new List<Entry>();
					
					if ( _entriesByType.TryGetValue( type.Extension, out var entries ) )
					{
						foreach ( var entry in entries )
						{						
							if ( matchType || entry.Path.Contains( query, StringComparison.OrdinalIgnoreCase ) )
								assets.Add( entry );
						}
					}

					if ( !matchType && assets.Count == 0 )
						continue;

					var group = new BrowserGroupNode( this, "type:" + definition.Key + ":" + type.Extension, $"{type.Name} ({assets.Count})", type.Icon, [type], type );
				
					foreach ( var asset in assets )
					{
						var node = new BrowserResourceNode( this, asset, definition.Key );
						_browserNodes.Add( node );
						group.AddItem( node );
					}
				
					section.AddItem( group );
					
					if ( query.Length > 0 )
						_tree.Open( group );
				}

				if ( !section.HasChildren )
					continue;

				ownerNode.AddItem( section );

				if ( query.Length > 0 || !_treeInitialized )
					_tree.Open( section );
			}

			_tree.AddItem( ownerNode );
			
			if ( query.Length > 0 || !_treeInitialized )
				_tree.Open( ownerNode );
		}

		_treeInitialized = true;
		UpdateTitle();
	}

	/// <summary>
	/// Opens a supported asset in this editor after capturing the current pending edits.
	/// </summary>
	public void AssetOpen( Asset asset )
	{
		if ( asset == CurrentAsset || !HasEditableSource( asset ) || ResourceCatalog.FindResource( asset?.AssetType?.FileExtension ) is null )
			return;

		if ( !CaptureCurrent() )
			return;

		OpenResource( asset );

		if ( !Visible )
			Show();

		Focus();
	}

	public void SelectMember( string memberName ) { }

	private void OpenResource( Asset asset )
	{
		var entry = _entries.FirstOrDefault( x => x.Asset == asset );

		if ( entry is null )
		{
			Reload();
			entry = _entries.FirstOrDefault( x => x.Asset == asset );
		}

		if ( entry is not null )
			OpenEntry( entry );
	}

	void AssetSystem.IEventListener.OnAssetChanged( Asset asset )
	{
		if ( ResourceCatalog.FindResource( asset?.AssetType?.FileExtension ) is not null )
			Reload();
	}

	void AssetSystem.IEventListener.OnAssetSystemChanges() => Reload();
}
