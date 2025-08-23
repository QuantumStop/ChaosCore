// A centralized system from which we can call specific npcs without iterating over bullshit or ugly calls

namespace Core;

public class AIManager : GameObjectSystem
{
	public AIManager( Scene scene ) : base( scene )
	{
		if (Game.IsPlaying)
		Start();
	}

	public int sceneAICount { get; set; }

	public int maxAICount { get; set; } = 1024; // No more than 1024 npcs in scene. TODO:: Evaulate what a reasonable figure is, this is an arbitrary guess :)

	public List<BaseNpc> currentNPCsInScene { get; set; }

	public void Start()
	{
		Log.Info( "AIManager::Start() Running.." );
		currentNPCsInScene = new List<BaseNpc>( maxAICount );
		//currentNPCsInScene.EnsureCapacity( maxAICount );
	}

	public int NumAIs() { return sceneAICount; }

	public void AddAI( BaseNpc NPC )
	{
		Log.Info( $"AIManager::AddAI() {NPC.TargetName} added to manager." );

		sceneAICount++;
		currentNPCsInScene.Add(NPC);
		
	}

	public void RemoveAI( BaseNpc NPC )
	{
		Log.Info( $"AIManager::RemoveAI() {NPC.TargetName} removed from manager." );

		sceneAICount--;
		currentNPCsInScene.Remove( NPC );

	}


}

public class AIThinkScheduler : GameObjectSystem
{
	//========================================
	// Stolen from Source, this makes it so
	// even NPCs spawned at the same time will not
	// think at the same time. Reduces spikes from
	// All that code running at the same time
	//
	// TODO: Fix =^)
	//========================================

	public AIThinkScheduler( Scene scene ) : base( scene )
	{
		
	}

	private static int spawnedThisFrame = 0;
	private static TimeSince timeSinceLastFrame = 0;

	private static readonly float[] ThinkOffsets = new float[]
	{
		0.0f, 0.150f, 0.075f, 0.225f, 0.030f, 0.180f, 0.120f, 0.270f,
		0.045f, 0.210f, 0.105f, 0.255f, 0.015f, 0.165f, 0.090f, 0.240f,
		0.135f, 0.060f, 0.195f, 0.285f
	};

	public float GetNextThinkTime()
	{

		if ( timeSinceLastFrame > Time.Delta ) // new frame
		{
			spawnedThisFrame = 0;
			timeSinceLastFrame = 0;
		}

		float offset = ThinkOffsets[spawnedThisFrame % ThinkOffsets.Length];
		spawnedThisFrame++;
		return Time.Now + offset;
	}
}
