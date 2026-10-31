namespace Core.Editor;

using System;
using System.Collections.Generic;

public sealed partial class ResourceEditor
{
	private readonly List<BrowserResourceNode> _browserNodes = new();
	private readonly Dictionary<Type, Pixmap> _resourceTypeIcons = new();
	private readonly Dictionary<Type, Color> _resourceTypeIconBackgrounds = new();
	private bool _treeInitialized;

	private static Color _usedColor => Color.Parse( "#64b58b" ) ?? Theme.Blue;

	private Pixmap _projectIcon;
	private const int _projectIconSize = 20;
	private string _projectIconSource;
	private bool _projectIconLoading;

	private Pixmap LoadProjectIcon()
	{
		var project = Project.Current;

		var localIcon = System.IO.Path.ChangeExtension( project.ConfigFilePath, ".png" );

		var source = project.Package?.Thumb ?? localIcon;

		if ( _projectIconSource != source )
		{
			_projectIconSource = source;
			_projectIcon = null;
			_projectIconLoading = false;

			if ( System.IO.File.Exists( localIcon ) )
			{
				using var local = Bitmap.CreateFromBytes( System.IO.File.ReadAllBytes( localIcon ) );

				if ( local.Width > 0 && local.Width == local.Height )
					_projectIcon = PrepareProjectIcon( local );
			}
		}

		if ( _projectIcon is null && !_projectIconLoading && !string.IsNullOrWhiteSpace( project.Package?.Thumb ) )
			DownloadProjectIcon( project.Package.Thumb );

		return _projectIcon;
	}

	private async void DownloadProjectIcon( string source )
	{
		_projectIconLoading = true;

		try
		{
			using var bitmap = Bitmap.CreateFromBytes( await Http.RequestBytesAsync( source ) );

			if ( !IsValid || source != _projectIconSource )
				return;

			_projectIcon = PrepareProjectIcon( bitmap );

			if ( _tree?.IsValid == true )
				_tree.Update();
		}
		catch ( Exception e )
		{
			Log.Warning( $"Resource Editor project icon could not be loaded: {e.Message}" );
		}
	}

	private Pixmap PrepareProjectIcon( Bitmap source )
	{
		var pixels = Math.Max( 1, (int)MathF.Ceiling( _projectIconSize * DpiScale ) );

		return Resample( source, pixels );
		static Pixmap Resample( Bitmap image, int size )
		{
			// Repeated smooth reductions filter fine details before the final small draw.
			if ( image.Width > size * 2 || image.Height > size * 2 )
			{
				using var half = image.Resize( Math.Max( size, image.Width / 2 ), Math.Max( size, image.Height / 2 ), true );

				return Resample( half, size );
			}

			using var resized = image.Resize( size, size, true );

			return Pixmap.FromBitmap( resized );
		}
	}

	/// <summary>
	/// Clears link counts and refreshes affected resource branches without rebuilding the whole outliner.
	/// </summary>
	private void RefreshLinkedTrees()
	{
		_linkCache.Clear();
		if ( _tree?.IsValid == true )
		{
			foreach ( var node in _browserNodes )
				node.RefreshLinks();

			_tree.Update();
		}
	}

	/// <summary>
	/// Draws the resource thumbnail or type icon, using the fallback icon when neither is available.
	/// </summary>
	private void DrawAssetIcon( Rect rect, Asset asset, ResourceEditorType type = null )
	{
		var assetType = asset?.AssetType ?? type?.AssetType;

		var resourceType = assetType?.ResourceType;

		if ( resourceType is not null && !_resourceTypeIcons.ContainsKey( resourceType ) )
		{
			Pixmap icon = null;

			try
			{
				var bitmap = EditorTypeLibrary.GetType( resourceType )?.Create<GameResource>()?.GetAssetTypeIcon( 16, 16 );

				if ( bitmap is not null )
				{
					icon = Pixmap.FromBitmap( bitmap );
					// Make the rounded icons square again
					_resourceTypeIconBackgrounds[resourceType] = bitmap.GetPixel( 0, bitmap.Height / 2 );
				}
			}
			catch ( Exception e )
			{
				Log.Warning( $"Resource Editor icon for '{resourceType.Name}' failed: {e.Message}" );
			}

			_resourceTypeIcons[resourceType] = icon;
		}

		if ( resourceType is not null && _resourceTypeIcons.TryGetValue( resourceType, out var resourceIcon ) && resourceIcon is not null )
		{
			if ( _resourceTypeIconBackgrounds.TryGetValue( resourceType, out var background ) )
			{
				Paint.ClearPen();
				Paint.SetBrush( background );
				Paint.DrawRect( rect );
			}

			Paint.Draw( rect, resourceIcon );
		}
		else if ( assetType?.Icon16 is not null )
			Paint.Draw( rect, assetType.Icon16 );
		else
			Paint.DrawIcon( rect, type?.Icon ?? "description", 16, TextFlag.Center );
	}
}
