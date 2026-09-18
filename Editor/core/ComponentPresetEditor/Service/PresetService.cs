namespace Core.Editor;

/// <summary>
/// <para> Public editor API and service for generic component presets. </para>
///
/// This is the primary preset API used to discover and load preset assets, capture target
/// state, create presets, apply them to compatible targets, inspect or edit payload values,
/// and persist preset resources. 
/// 
/// <para> Target schemas are declared with <b>[PresetTargetAttribute] </b> </para>
/// </summary>
public static partial class PresetService
{
	/// <summary>
	/// File extension used by generic preset assets.
	/// </summary>
	public const string AssetExtension = "preset";

	/// <summary>
	/// Returns whether an asset is a generic preset asset handled by this API.
	/// </summary>
	public static bool IsPresetAsset( Asset asset )
	{
		return asset?.AssetType?.FileExtension == AssetExtension;
	}
}

public sealed class PresetAsset
{
	/// <summary>
	/// Backing editor asset. Null until the preset has been saved.
	/// </summary>	
	public Asset Asset { get; init; }
	
	/// <summary>
	/// Editable preset data.
	/// </summary>
	public GenericPresetResource Resource { get; init; }

	/// <summary>
	/// Preset target metadata.
	/// </summary>
	public PresetTargetInfo Target { get; init; }

	public PresetModeInfo Mode => Target?.FindMode( Resource?.PresetMode );

	public string Name => Resource?.PresetName
		?? Asset?.Name
		?? "Preset";

	public string Icon => !string.IsNullOrWhiteSpace( Resource?.PresetIcon )
		? Resource.PresetIcon
		: Target?.Icon ?? "tune";

	public string Description => Resource?.PresetDescription;

	public string SourcePath => Asset?.Path;

	public bool CanApplyTo( object target ) => PresetService.CanApply( this, target );

	public bool ApplyTo( object target ) => PresetService.Apply( this, target );

}
