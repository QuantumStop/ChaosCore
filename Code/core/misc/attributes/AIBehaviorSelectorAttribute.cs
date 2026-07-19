namespace Core;

using System;
using Core.AI;

[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
public class AIBehaviorSelectorAttribute : Attribute
{
	public Type TargetType { get; set; } = typeof( AIBehavior );
}
