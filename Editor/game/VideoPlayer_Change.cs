using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;
using System;

namespace Editor;

public class UpdateVideoProp : Widget
{
	[Event("assettags.updated", Priority = 100)]
	void OnTagChanged()
	{
		Log.Info("A tag changed");
	}
}
