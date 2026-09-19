namespace Core;

[Hide]
public class BaseVanityOverlay : PanelComponent
{	 
	[Property, ReadOnly] public Dictionary<VanitySlot, VanityChannel> ActiveChannels => VanityAPI.ActiveChannelsDebug;
	
	public BaseVanityOverlay()
	{
		// TODO: We should do this better
		// for now just lazily make sure its clear initially.

		// Might be even fine for this use??
		VanityAPI.Clear();
	}

	protected override void OnUpdate()
	{
		if ( VanityAPI.Update() )
			StateHasChanged();
	}
}
