using Sandbox.Animation;

namespace Core;

/// <summary>
/// Because interface only looks on self gameobjects and parents, our viewmodel renderer never sends anything to player, this does.
/// Pretend this doesn't exist.	
/// </summary>
public sealed class ViewmodelProxy : Component, Component.IAnimationEvent
{
	void IAnimationEvent.OnAnimationEvent( AnimationEvent data ) => BasePlayer.Local.AnimationEventHack( data );
}
