namespace SDK.UI;

using Sandbox.UI;
using System;

public class ChaosChatBox : TextEntry
{
	public Action OnTabPressed { get; set; }

	public override void OnButtonTyped( ButtonEvent e )
	{
		e.StopPropagation = true;

		if ( e.Button == "tab" )
			OnTabPressed?.Invoke();

		base.OnButtonTyped( e );
	}
}
