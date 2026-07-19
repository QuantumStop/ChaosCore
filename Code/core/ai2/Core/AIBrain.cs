using System;
using static Core.AI.AIController;

namespace Core.AI;

public class AIBrain : AIModule
{
	public enum AILod
	{
		/// <summary>
		/// Highest level of detail, full AI thinking ( least performant ). 
		/// </summary>
		AI_LOD0,
		AI_LOD1,
		AI_LOD2,
		AI_LOD3,
		/// <summary>
		/// Lowest level of detail, least AI thinking ( most performant ).
		/// </summary>
		AI_LOD4,
	}

	public AI_BehaviorState aiState;


	private static readonly float[] ThinkRateMultipliers = [1f, 2f, 4f, 6f, 8f]; // maybe these can be exposed to the resource or something
	private static readonly float[] LODDistanceChecks = [512f, 1024f, 1520f, 2048f, 3092f];

	public AILod _aiLOD;

	public bool _shouldUseLOD => Owner.Definition.UseAILOD;

	/// <summary>
	/// The original AI think rate set at spawn. This is grabbed from the NPC Definition resource
	/// </summary>
	public float _baseThinkRate;

	/// <summary>
	/// The current AI think rate. This may differ from the base think rate due to LODs.
	/// </summary>
	public float _currentThinkRate;

	/// <summary>
	/// The last time the npc called its think. Used in timing
	/// </summary>
	public float _lastThinkTime;
	/*
	public struct AIRebalanceInfo
	{
		AIController _NPC;
		int _nextThinkTick;
		bool _inPVS;
		float _dotPlayer;
		float _distPlayer;
	}

	bool CanRebalanceThink()
	{
		if ( !Controller.Definition.UseAILOD )
		{
			return false;
		}

		return true;
	}
	*/

	public override void Init( AIController controller )
	{
		Owner = controller;
		_baseThinkRate = controller.Definition.DefaultThinkRate;
		_currentThinkRate = controller.Definition.DefaultThinkRate;
	}

	public AILod DetermineAILOD()
	{
		float playerDist = Vector3.DistanceBetween( Owner.WorldPosition, Owner.Blackboard.playerReference.WorldPosition ); // should probably be put somewhere upstream tbh
		Vector3 toPlayer = (Owner.Blackboard.playerReference.WorldPosition - Owner.WorldPosition).Normal;
		float playerDot = Vector3.Dot( Owner.WorldRotation.Forward, toPlayer );// Zapagoogas writes worst code ever, asked to leave Chaos Theory

		if ( playerDot > 0.5 && playerDist >= LODDistanceChecks[0] && playerDist <= LODDistanceChecks[1] )
			return AILod.AI_LOD1;
		else if ( playerDot > 0.5 && playerDist >= LODDistanceChecks[1] && playerDist <= LODDistanceChecks[2] )
			return AILod.AI_LOD2;
		else if ( playerDot < 0.5 || (playerDist >= LODDistanceChecks[2] && playerDist <= LODDistanceChecks[3]) )
			return AILod.AI_LOD3;
		else if ( playerDot < 0.5 || playerDist >= LODDistanceChecks[3] && playerDist <= LODDistanceChecks[4] )
			return AILod.AI_LOD4;
		else
			return AILod.AI_LOD0; // Full throttle!

	}

	public float DetermineThinkRate()
	{
		if ( !_shouldUseLOD )
			return _baseThinkRate;

		int index = (int)_aiLOD;

		index = Math.Clamp( index, 0, ThinkRateMultipliers.Length - 1 );
		//	Log.Info($"Determining think rate using baserate:{_baseThinkRate} * thinkmultiplier:{ThinkRateMultipliers[index]}");
		return _baseThinkRate * ThinkRateMultipliers[index];
	}
}
