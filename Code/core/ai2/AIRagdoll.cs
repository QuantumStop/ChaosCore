namespace Core;

public enum PhysicsMode
{
	Animated, // drivne by anims
	Blended, // ragdoll driven by animation with a weight param
	Physics // full ragdoll mode
}

/// <summary>
/// Wrapper around the modelphysics component that lets us access ragdolls easier
/// </summary>
public class Ragdoll : BaseEntity
{

	[Property] public ModelPhysics PhysicsModel;
	public SkinnedModelRenderer ModelRenderer;

	public PhysicsMode physicsMode = PhysicsMode.Animated; // default to anim state
	public float driveInfluence = 1f; // how much to drive joints using anim, only works when in blended physmode

	protected override void OnFixedUpdate()
	{
		if ( physicsMode == PhysicsMode.Animated ) return; // skip when we're animating
	}

	public void EnableRagdoll()
	{
		PhysicsModel.MotionEnabled = true;
	}
	public void DisableRagdoll()
	{
		PhysicsModel.MotionEnabled = true;
	}
	public float GetMass()
	{
		return PhysicsModel.Mass;
	}
	public Vector3 GetCenterOfMass()
	{
		return PhysicsModel.MassCenter;
	}

}

