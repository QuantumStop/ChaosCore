using System;
using System.Collections.Generic;
using System.Text;

namespace Core.AI;

/// <summary>
/// An attempt to centralize the most commonly accessed important members
/// </summary>
public abstract class AIModule
{
	public AIController Owner;
	public bool Active;

	/// <summary>
	/// Initializes the module within a controller class
	/// </summary>
	/// <param name="owner"></param>
	public virtual void Init( AIController owner ) { Owner = owner; }

	/// <summary>
	/// Called every think
	/// </summary>
	public virtual void Tick() { }

	/// <summary>
	/// Ensures modules cleanly are shut off before death or removal
	/// </summary>
	public virtual void Terminate() { }

	/// <summary>
	/// Can draw debug visuals
	/// </summary>
	public virtual void DrawDebug() { }

}

