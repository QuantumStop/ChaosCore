using System;
using Core;
using Core.AI;
using SDK.AI;

namespace SDK;

public partial class SDKGameManager
{
	public override void DrawDebugText( Action<DebugTextBlock> draw )
	{
		base.DrawDebugText( draw );

		foreach ( var npc in Scene.GetAll<AIController>() )
		{
			if ( !npc.DebugPlanning )
				continue;

			if ( !npc.Active || !npc.IsAlive || npc.Blackboard is null || npc.BehaviorModule is not RoamerBehaviors roamer ) 
				continue;
		
			draw( roamer.GetDebugText() );
		}
	}
}
