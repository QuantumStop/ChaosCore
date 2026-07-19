using System;
namespace Core.AI;

public abstract class AIAbility
{

	/// <summary>
	/// Little modular classes that can be stuck on AI for reusable behaviors
	/// </summary>

	public AIController Controller;

	public AIAbility( AIController controller )
	{
		Controller = controller;
		Controller.Damaged += OnOwnerDamaged;
		Controller.Touched += OnOwnerTouched;
	}

	public virtual void Tick() { }

	/// <summary>
	/// Abilities can have code run on different events. These will be expanded in the future
	/// </summary>
	/// <param name="dmg"></param>
	public virtual void OnOwnerDamaged( DamageInfo dmg )
	{
	}
	public virtual void OnOwnerTouched()
	{
	}

	public virtual Type GetAbilityClass() { return GetType(); }

}

