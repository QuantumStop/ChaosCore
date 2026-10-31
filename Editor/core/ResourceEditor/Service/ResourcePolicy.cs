namespace Core.Editor;

using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ActionGraphs;

/// <summary>
/// Defines which resources ResourceEditor can display and edit.
/// 
/// IgnoredResourceTypes to hide resource types, AllowedResourceTypes to allow only selected types,
/// or AllowsPath to exclude individual files. 
/// 
/// A resource must pass every policy to appear or be editable.
/// </summary>
public abstract class ResourcePolicy
{
	/// <summary>
	/// Excluded base types also exclude all of their derived resource types.
	/// </summary>
	public virtual IEnumerable<Type> IgnoredResourceTypes => [];

	/// <summary>
	/// Null permits all types, an empty list permits none.
	/// </summary>
	public virtual IEnumerable<Type> AllowedResourceTypes => null;

	public virtual bool AllowsType( Type resourceType ) => !IgnoredResourceTypes.Any( x => x.IsAssignableFrom( resourceType ) ) && (AllowedResourceTypes is null || AllowedResourceTypes.Any( x => x.IsAssignableFrom( resourceType ) ));

	/// <summary>
	/// Additional per-file exclusions. 
	/// Called with an absolute source path, including new drafts.
	/// </summary>
	public virtual bool AllowsPath( Type resourceType, string absolutePath ) => true;
}

/// <summary>
/// Built-in resources we explicitly ignore.
/// </summary>
public sealed class CoreResourcePolicy : ResourcePolicy
{
	public override IEnumerable<Type> IgnoredResourceTypes => [
		typeof( PrefabFile ), 
		typeof( GenericPresetResource ), 
		typeof( Sandbox.MovieMaker.MovieResource ), 
		typeof( SceneFile ), typeof( RenderTextureAsset ), 
		typeof( ActionGraphResource )
	];
}

/// <summary>
/// Combines project policies and denies access when any policy rejects a resource or fails.
/// </summary>
internal static class ResourceAccess
{
	private static readonly HashSet<string> _reportedFailures = [];
	private static ResourcePolicy[] _policies;

	[EditorEvent.Hotload]
	private static void OnHotload()
	{
		_policies = null;
		_reportedFailures.Clear();
	}

	internal static ResourcePolicy[] Policies() => _policies ??= EditorTypeLibrary.GetTypes<ResourcePolicy>().Where( x => !x.TargetType.IsAbstract ).Select( x => x.Create<ResourcePolicy>() ).ToArray();

	/// <summary>
	/// Checks type restrictions and, when a path is supplied, file restrictions.
	/// </summary>
	internal static bool Allows( Type type, string path = null )
	{
		if ( type is null )
			return false;

		try
		{
			return Policies().All( x => x.AllowsType( type ) && (path is null || x.AllowsPath( type, path )) );
		}
		catch ( Exception e )
		{
			if ( _reportedFailures.Add( e.GetType().FullName + ":" + e.Message ) )
				Log.Warning( $"Resource Editor resource policy failed: {e.Message}" );

			return false;
		}
	}
}
