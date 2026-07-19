namespace Core;

using System;
using Core.AI;

// Maybe this should be in a general AIAttributes file. I barely know what im doing or if this will even work, so it is prone ot change
[AttributeUsage( AttributeTargets.Property | AttributeTargets.Field )]
public class AIFactSelectorAttribute : Attribute
{
	public Type TargetType { get; set; } = typeof( AIFacts );
}

