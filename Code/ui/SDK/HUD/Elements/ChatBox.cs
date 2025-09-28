using Sandbox.UI;
using System;

internal class ChatBox : TextEntry
{
	public Action OnTabPressed { get; set; }

	public override void OnButtonTyped( ButtonEvent e )
	{
		e.StopPropagation = true;

		var button = e.Button;

		if ( button == "tab" )
		{
			OnTabPressed?.Invoke();
		}

		base.OnButtonTyped( e );
	}
}
