using System;

namespace Core.AI;

public class Jump : AIAbility
{
	TimeSince timeSinceStart = 0;
	float duration;
	Vector3 start;
	Vector3 end;
	float peakHeight;

	bool isJumping;

	public Jump( AIController controller ) : base( controller )
	{
	}

	public override void Tick()
	{
		if ( !isJumping )
			return;

		if ( timeSinceStart >= duration )
		{
			Controller.Agent.SetAgentPosition( end );
			Controller.Agent.CompleteLinkTraversal();
			isJumping = false;
			return;
		}

		var t = timeSinceStart / duration;

		var newPosition = Vector3.Lerp( start, end, t );
		var yOffset = 4f * peakHeight * t * (1f - t);
		newPosition.z = MathX.Lerp( start.z, end.z, t ) + yOffset;

		Controller.Agent.SetAgentPosition( newPosition );
	}

	public void PhysicsJump( Vector3 TargetPosition )
	{
		isJumping = true;
		start = Controller.Agent.AgentPosition;
		end = TargetPosition;

		// Calculate peak height for the parabolic arc
		var heightDifference = end.z - start.z;
		peakHeight = MathF.Abs( heightDifference ) + 25f;

		var mid = (start + end) / 2f;

		// Estimate prabolic duration size using a triangle /\ between start, mid, end 
		var startToMid = mid.WithZ( peakHeight ) - start;
		var midToEnd = end - mid.WithZ( peakHeight );
		duration = (startToMid + midToEnd).Length / Controller.Agent.MaxSpeed;
		duration = MathF.Max( 0.75f, duration ); // Ensure minimum duration

		Controller.Agent.SetAgentPosition( end );
	}

}
