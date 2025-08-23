using System;

/// <summary>
/// As opposed to TemporaryEffect, this can delete just the components but not the GameObject, in cases where you don't want the GameObject to go away
/// </summary>
public class TimedKill : Component
{
	/// <summary>
	/// How long until we clear the effects?
	/// </summary>
	[Property] public float DestroyAfterSeconds { get; set; } = 1f;
	/// <summary>
	/// Do we want to delete the GameObject or all components on it?
	/// </summary>
	[Property] public bool DeleteGameObject { get; set; } = true;
	[Property, ReadOnly] private TimeSince timeAlive;

	public Delegate OnKill { get; set; }

	protected override void OnEnabled() { timeAlive = 0f; }

	protected override void OnUpdate()
	{
		if ( (!Scene.IsEditor || GameObject.Flags.HasFlag( GameObjectFlags.NotSaved ) || GameObject.Flags.HasFlag( GameObjectFlags.Hidden )) && timeAlive > DestroyAfterSeconds )
		{
			// if we are deleting the GameObject, don't bother clearing the components
			if ( DeleteGameObject )
			{
				OnKill?.DynamicInvoke();
				GameObject.Destroy();
			}
			else { GameObject.Clear(); }
		}
	}
}
