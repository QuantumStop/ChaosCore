namespace Core;

using System;

/// <summary>
/// Generic asset container for presets.
/// </summary>
[AssetType( Name = "Preset", Extension = "preset", Category = "Preset", IconColor = "#70c6ff", Flags = AssetTypeFlags.NoEmbedding )]
public class GenericPresetResource : GameResource
{
	[Property, Hide] public string PresetType { get; set; }

	[Property, Hide] public string PresetMode { get; set; } = PresetModes.Default;

	[Property] public string PresetName { get; set; }

	[Property] public string PresetIcon { get; set; } = "tune";

	[Property, TextArea] public string PresetDescription { get; set; }

	[Property, Hide] public string SourceAssetPath { get; set; }

	[Property, Hide] public string PayloadJson { get; set; }

	protected override Bitmap CreateAssetTypeIcon( int width, int height ) => CreateSimpleAssetTypeIcon( "photo_filter", width, height, "#467392", "#e2e2e2" );

}

[AttributeUsage( AttributeTargets.Class, Inherited = true )]
public sealed class PresetTargetAttribute( string key ) : Attribute
{
	public string Key { get; } = key;

	public string Name { get; set; }

	public string Icon { get; set; }

	public string Category { get; set; }

	/// <summary>
	/// Additional named partial modes.
	///
	/// The implicit default mode always exists.
	/// </summary>
	public string[] Modes { get; set; } = [];

	/// <summary>
	/// If true, the implicit default mode includes every eligible
	/// [Property] except properties marked with [PresetIgnore].
	/// </summary>
	public bool IncludeAll { get; set; }

	/// <summary>
	/// Includes eligible [Property] members inherited from base classes.
	/// </summary>
	public bool IncludeInherited { get; set; } = true;
}

[AttributeUsage( AttributeTargets.Property, AllowMultiple = true, Inherited = true )]
public sealed class PresetPropertyAttribute : Attribute
{
	public IReadOnlyList<string> Modes { get; }

	public PresetPropertyAttribute( params string[] modes )
	{
		if ( modes is null || modes.Length == 0 )
		{
			Modes = [ PresetModes.Default ];

			return;
		}

		var normalized = modes
			.Where( x => !string.IsNullOrWhiteSpace( x ) )
			.Select( PresetModes.Normalize )
			.Distinct( StringComparer.OrdinalIgnoreCase )
			.ToArray();

		Modes = normalized.Length > 0 
			? normalized 
			: [ PresetModes.Default ];
	}

	public bool IncludesMode( string mode )
	{
		mode = PresetModes.Normalize( mode );

		return Modes.Any( x => string.Equals( x, mode, StringComparison.OrdinalIgnoreCase ) );
	}
}

[AttributeUsage( AttributeTargets.Property, Inherited = true )]
public sealed class PresetIgnoreAttribute : Attribute { }

[AttributeUsage( AttributeTargets.Property )]
public sealed class PresetSelectorAttribute : Attribute
{
	public string PresetKey { get; }

	public Type TargetType { get; }

	public PresetSelectorAttribute() { }

	public PresetSelectorAttribute( string presetKey )
	{
		PresetKey = presetKey;
	}

	public PresetSelectorAttribute( Type targetType )
	{
		TargetType = targetType;
	}
}

[AttributeUsage( AttributeTargets.Class, AllowMultiple = true )]
public sealed class PresetPreviewForAttribute( Type targetType ) : Attribute
{
	public Type TargetType { get; } = targetType;
}
