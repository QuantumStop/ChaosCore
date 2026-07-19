#if IGNIS || STANDALONE
namespace Core;

using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System;

public sealed partial class SaveSystem
{
	private static JsonArray CollectRequiredPackages( List<LoadedSceneEntry> loadedScenes, JsonObject currentSceneJson )
	{
		var packages = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		foreach ( var entry in loadedScenes )
		{
			var sceneFile = ResourceLibrary.Get<SceneFile>( entry.ResourcePath );
			if ( sceneFile is null )
				continue;

			foreach ( var package in sceneFile.GetReferencedPackages() )
			{
				if ( !string.IsNullOrWhiteSpace( package ) )
					packages.Add( package );
			}
		}

		if ( currentSceneJson is not null )
		{
			foreach ( var package in Cloud.ResolvePrimaryAssetsFromJson( currentSceneJson ) )
			{
				if ( !string.IsNullOrWhiteSpace( package.FullIdent ) )
					packages.Add( package.FullIdent );
			}
		}

		var result = new JsonArray();
		foreach ( var package in packages )
			result.Add( JsonValue.Create( package ) );

		return result;
	}

	private static async Task MountRequiredPackages( JsonArray packageArray )
	{
		foreach ( var node in packageArray )
		{
			var ident = node?.ToString();
			if ( string.IsNullOrWhiteSpace( ident ) )
				continue;

			if ( Package.TryGetCached( ident, out _ ) )
				continue;

			await Package.MountAsync( ident, false );
		}
	}

	private static JsonObject CollectNetworkOwnership( Scene scene )
	{
		var result = new JsonObject();

		foreach ( var gameObject in scene.GetAllObjects( true ) )
		{
			if ( !gameObject.Network.Active )
				continue;

			var owner = gameObject.Network.Owner;
			if ( owner is null )
				continue;

			result[gameObject.Id.ToString()] = owner.SteamId.Value;
		}

		return result;
	}

	private static void RestoreNetworkOwnership( Scene scene, JsonObject ownershipData )
	{
		var steamIdToConnection = new Dictionary<long, Connection>();
		foreach ( var connection in Connection.All )
			steamIdToConnection.TryAdd( connection.SteamId.Value, connection );

		using var _ = scene.BatchGroup();

		foreach ( var (gameObjectGuid, node) in ownershipData )
		{
			if ( !Guid.TryParse( gameObjectGuid, out var guid ) )
				continue;

			var gameObject = scene.Directory.FindByGuid( guid ) as GameObject;
			if ( !gameObject.IsValid() )
				continue;

			var steamId = node?.GetValue<long>() ?? 0;
			if ( steamId == 0 || !steamIdToConnection.TryGetValue( steamId, out var target ) )
				continue;

			if ( !gameObject.Network.Active )
				gameObject.NetworkSpawn( target );
			else
				gameObject.Network.AssignOwnership( target );
		}
	}

	private static JsonObject CollectSyncState( Scene scene )
	{
		var result = new JsonObject();

		foreach ( var gameObject in scene.GetAllObjects( true ) )
		{
			if ( gameObject.Flags.Contains( GameObjectFlags.DontDestroyOnLoad ) )
				continue;

			foreach ( var component in gameObject.Components.GetAll() )
			{
				var typeDescription = TypeLibrary.GetType( component.GetType() );
				if ( typeDescription is null )
					continue;

				var syncProperties = typeDescription.Properties.Where( property => property.HasAttribute<SyncAttribute>() );
				JsonObject componentData = null;

				foreach ( var syncProperty in syncProperties )
				{
					if ( syncProperty.HasAttribute<PropertyAttribute>() )
						continue;

					try
					{
						var value = syncProperty.GetValue( component );
						JsonNode node;

						try
						{
							node = Json.ToNode( value, syncProperty.PropertyType );
						}
						catch
						{
							var stream = ByteStream.Create( 256 );
							try
							{
								Game.TypeLibrary.ToBytes( value, ref stream );
								node = new JsonObject { ["__bytepack"] = Convert.ToBase64String( stream.ToArray() ) };
							}
							finally
							{
								stream.Dispose();
							}
						}

						componentData ??= new JsonObject();
						componentData[syncProperty.Name] = node;
					}
					catch ( Exception e )
					{
						Log.Warning( $"Failed to serialize [Sync] property {component.GetType().Name}.{syncProperty.Name}: {e.Message}" );
					}
				}

				if ( componentData is not null )
					result[component.Id.ToString()] = componentData;
			}
		}

		return result;
	}

	private static void RestoreSyncState( Scene scene, JsonObject syncData )
	{
		foreach ( var (componentGuid, node) in syncData )
		{
			if ( !Guid.TryParse( componentGuid, out var guid ) || node is not JsonObject propertyData )
				continue;

			var target = scene.Directory.FindComponentByGuid( guid );
			if ( target is null )
				continue;

			var typeDescription = TypeLibrary.GetType( target.GetType() );
			if ( typeDescription is null )
				continue;

			var syncProperties = typeDescription.Properties.Where( property => property.HasAttribute<SyncAttribute>() );
			foreach ( var syncProperty in syncProperties )
			{
				if ( syncProperty.HasAttribute<PropertyAttribute>() || !propertyData.ContainsKey( syncProperty.Name ) )
					continue;

				try
				{
					var jsonValue = propertyData[syncProperty.Name];
					object value;

					if ( jsonValue is JsonObject wrapper && wrapper.ContainsKey( "__bytepack" ) )
					{
						var bytes = Convert.FromBase64String( wrapper["__bytepack"]!.GetValue<string>() );
						var reader = ByteStream.CreateReader( bytes );
						try
						{
							value = Game.TypeLibrary.FromBytes<object>( ref reader );
						}
						finally
						{
							reader.Dispose();
						}
					}
					else
					{
						value = Json.FromNode( jsonValue, syncProperty.PropertyType );
					}

					syncProperty.SetValue( target, value );
				}
				catch ( Exception e )
				{
					Log.Warning( $"Failed to restore [Sync] property {target.GetType().Name}.{syncProperty.Name}: {e.Message}" );
				}
			}
		}
	}
}
#endif
