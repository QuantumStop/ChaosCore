// A centralized system from which we can call specific npcs without iterating over bullshit or ugly calls

using System;
using static Core.AI.AIController;

namespace Core.AI;

public class AIManager : GameObjectSystem<AIManager>
{
	public AIManager( Scene scene ) : base( scene )
	{

		Listen( Stage.SceneLoaded, 5, Start, "AIManager OnStart" );
		Listen( Stage.StartUpdate, 1, UpdateNavigation, "AIManager OnFixedUpdate" );
		Listen( Stage.PhysicsStep, 0, TickAISystem, "AIManager OnFixedUpdate" );

	}

	public bool _Init;

	public int sceneAICount { get; set; }

	[Property, Feature( "Debug" ), ReadOnly] public int maxAICount { get; set; } = 1024; // No more than 1024 npcs in scene. TODO:: Evaulate what a reasonable figure is, this is an arbitrary guess :)

	public struct NavRequest
	{
		public AIController Controller;
		public Vector3 TargetPosition;
	}

	public Queue<NavRequest> navQueue = new(); // stores requests to be processed 
	public Dictionary<AIController, NavRequest> pendingNavRequests = []; // having the controller is a bit redundant

	public List<AIController> currentNPCsInScene { get; set; }

	[ConVar( "ai_max_nav_calls_pertick" )] public static int MaxNavCallsPerTick { get; set; } = 4;
	[ConVar( "ai_max_phase_per_tick" )] public static int MaxPhasesPerTick { get; set; } = 15;
	[ConVar( "ai_debug_actions" )] public static bool AIDebugActions { get; set; } = false;
	[ConVar( "ai_debug_corpses" )] public static bool AIDebugCorpses { get; set; } = false;
	[ConVar( "ai_disable" )] public static bool AIDisable { get; set; } = false;
	int navBudget;


	[ConCmd( "ai_force_navigation_reset" )]
	public static void AIForceNavigationReset()
	{
		foreach ( var npc in Current?.currentNPCsInScene )
		{
			if ( npc.Agent.IsNavigating || npc.Agent.GetPath().IsValid )
			{
				Log.Info( $"Movement stopped on {npc.TargetName}" );
				npc.Navigation.NavigationStopMovement();
				npc.Agent.Stop();
			}
		}
	}

	// John: I think we should have proper class where we could handle creations
	[ConCmd( "npc_create" )]
	public static void NpcCreate( string npcname = "" )
	{
		var scene = Game.ActiveScene;
		if ( !scene.IsValid() )
		{
			Log.Warning( "[npc_create] ActiveScene is null." );
			return;
		}

		var definition = ResourceLibrary.GetAll<NpcDefinition>().FirstOrDefault( definition =>
			definition.ResourceName.Equals( npcname, StringComparison.OrdinalIgnoreCase ) );

		if ( !definition.IsValid() )
		{
			if ( !string.IsNullOrWhiteSpace( npcname ) )
				Log.Warning( $"[npc_create] Unknown NPC definition '{npcname}'." );

			foreach ( var npcdef in ResourceLibrary.GetAll<NpcDefinition>() )
			{
				Log.Info( npcdef.ResourceName );
			}

			return;
		}

		var camera = scene.Camera;

#if IGNIS
		if ( GameManagerSystem.IsGameEjected )
			camera = Application.Editor.Camera;
#endif

		if ( !camera.IsValid() )
		{
			Log.Warning( "[npc_create] No active scene camera." );
			return;
		}

		var tr = scene.Trace.Ray( new Ray( camera.WorldPosition, camera.WorldRotation.Forward ), 4096f ).WithoutTags( "player", "npc" ).Run();
		if ( !tr.Hit )
		{
			Log.Warning( "[npc_create] No surface found under the crosshair." );
			return;
		}

		var npc = scene.CreateObject().Components.Create<AIController>( false );
		npc.Definition = definition;
		npc.GameObject.WorldPosition = tr.EndPosition;
		npc.GameObject.WorldRotation = Rotation.FromYaw( camera.WorldRotation.Yaw() );
		npc.GameObject.Name = definition.ResourceName;
		npc.Enabled = true;
	}

	public List<AIController> selectedNPCs = new();

	public void SelectNPC( AIController npc )
	{
		if ( !npc.IsValid() || npc.Scene != Scene )
			return;

		if ( selectedNPCs.Contains( npc ) )
		{
			DeselectNPC( npc );
			return;
		}

		selectedNPCs.Add( npc );
#if IGNIS
		npc.DebugOverlay.ScreenTextOverlay( npc );
#endif
	}

	public void DeselectNPC( AIController npc )
	{
		selectedNPCs.Remove( npc );
#if IGNIS
		DebugOverlaySystem.Current?.RemoveWhere( so =>
			so is DebugTextSceneObject d && d.component == npc
		);
#endif
	}

	[ConCmd( "npc_select" )]
	public static void NpcSelect( string cmd = "!picker" )
	{
		var scene = Game.ActiveScene;
		var manager = Current;

		if ( !scene.IsValid() || manager is null || manager.Scene != scene )
		{
			Log.Warning( "[npc_select] AIManager not available." );
			return;
		}

		if ( string.IsNullOrWhiteSpace( cmd ) )
			cmd = "!picker";

		if ( cmd.Equals( "!clearall", StringComparison.OrdinalIgnoreCase ) )
		{
			foreach ( var npc in manager.selectedNPCs.ToArray() )
			{
				manager.DeselectNPC( npc );
			}

			Log.Info( "[npc_select] Cleared NPC selection." );
			return;
		}

		//
		// Picker Mode
		//
		if ( cmd.Equals( "!picker", StringComparison.OrdinalIgnoreCase ) )
		{
			var camera = scene.Camera;

#if IGNIS
			if ( GameManagerSystem.IsGameEjected )
				camera = Application.Editor.Camera;

#endif
			if ( !camera.IsValid() )
			{
				Log.Warning( "[npc_select] No active scene camera." );
				return;
			}

			const float maxDistance = 2048f;

			var start = camera.WorldPosition;
			var end = start + camera.WorldRotation.Forward * maxDistance;

			var trace = scene.Trace.Ray( start, end )
				.WithAnyTags( "npc" )
				.UseHitboxes( true )
				.UsePhysicsWorld( true )
				.WithoutTags( "player" )
				.Run();

			if ( !trace.Hit || !trace.GameObject.IsValid() )
				trace = scene.Trace.Sphere( 25f, start, end )
					.WithAnyTags( "npc" )
					.UsePhysicsWorld( true )
					.WithoutTags( "player" )
					.Run();

			var npc = trace.GameObject.IsValid()
				? trace.GameObject.GetComponentInParent<AIController>()
				: null;

			if ( !trace.Hit || !npc.IsValid() )
			{
				Log.Warning( "[npc_select] Picker trace failed to pickup any npc." );
				return;
			}

			manager.SelectNPC( npc );
			return;
		}

		int changedCount = 0;

		foreach ( var npc in scene.GetAllComponents<AIController>() )
		{
			if ( !npc.IsValid() || !(npc.GetType().Name.Contains( cmd, StringComparison.OrdinalIgnoreCase ) ||
			npc.GameObject.Name.Contains( cmd, StringComparison.OrdinalIgnoreCase )) )
				continue;

			manager.SelectNPC( npc );
			changedCount++;
		}

		Log.Info( $"[npc_select] Toggled selection for {changedCount} NPCs." );
	}

	[ConCmd( "npc_kill" )]
	public static void NpcKill( string cmd = "!picker" )
	{
		var scene = Game.ActiveScene;
		var manager = Current;

		if ( !scene.IsValid() || manager is null || manager.Scene != scene )
		{
			Log.Warning( "[npc_kill] AIManager not available." );
			return;
		}

		if ( string.IsNullOrWhiteSpace( cmd ) )
			cmd = "!picker";

		//
		// Picker Mode
		//
		if ( cmd.Equals( "!picker", StringComparison.OrdinalIgnoreCase ) )
		{
			var camera = scene.Camera;
#if IGNIS
			if ( GameManagerSystem.IsGameEjected )
				camera = Application.Editor.Camera;
#endif
			if ( !camera.IsValid() )
			{
				Log.Warning( "[npc_kill] No active scene camera." );
				return;
			}

			const float maxDistance = 4096f;
			var start = camera.WorldPosition;
			var end = start + camera.WorldRotation.Forward * maxDistance;
			var trace = scene.Trace.Ray( start, end )
				.WithAnyTags( "npc" )
				.UseHitboxes( true )
				.UsePhysicsWorld( true )
				.WithoutTags( "player" )
				.Run();

			if ( !trace.Hit || !trace.GameObject.IsValid() )
				trace = scene.Trace.Sphere( 25f, start, end )
					.WithAnyTags( "npc" )
					.UsePhysicsWorld( true )
					.WithoutTags( "player" )
					.Run();

			var npc = trace.GameObject.IsValid()
				? trace.GameObject.GetComponentInParent<AIController>()
				: null;

			if ( !trace.Hit || !npc.IsValid() || !npc.IsAlive )
			{
				Log.Warning( "[npc_kill] No NPC found under the crosshair." );
				return;
			}

			npc.OnDamage( new DamageInfo( npc.CurHealth, npc.GameObject, npc.GameObject ) );
			return;
		}

		int changedCount = 0;

		foreach ( var npc in scene.GetAllComponents<AIController>() )
		{
			if ( !npc.IsValid() || !npc.IsAlive ||
				!(npc.GetType().Name.Contains( cmd, StringComparison.OrdinalIgnoreCase ) ||
				  npc.GameObject.Name.Contains( cmd, StringComparison.OrdinalIgnoreCase )) )
				continue;

			npc.OnDamage( new DamageInfo( npc.CurHealth, npc.GameObject, npc.GameObject ) );
			changedCount++;
		}

		Log.Info( $"[npc_kill] Applied lethal damage to {changedCount} NPCs." );
	}

	[ConCmd( "npc_go" )]
	public static void NPCGo()
	{
		var scene = Game.ActiveScene;
		var system = DebugOverlaySystem.Current;
		var camera = scene.Camera;
#if IGNIS
		if ( GameManagerSystem.IsGameEjected )
			camera = Application.Editor.Camera;
#endif
		if ( !camera.IsValid() )
		{
			Log.Warning( "[npc_select] No active scene camera." );
			return;
		}

		const float maxDistance = 2048f;

		var start = camera.WorldPosition;
		var end = start + camera.WorldRotation.Forward * maxDistance;

		var trace = scene.Trace.Ray( start, end )
			.UseHitboxes( true )
			.UsePhysicsWorld( true )
			.WithoutTags( "player" )
			.Run();

		foreach ( var npc in Current.selectedNPCs )
		{
			Vector3? movepos = trace.EndPosition;

			var pointOnNav = scene.NavMesh.GetClosestPoint( movepos.Value );

			if ( pointOnNav.HasValue )
			{

				npc.SetAIState( AI_BehaviorState.BEHAVIORSTATE_SCRIPTED );
				npc.SetForcedMovePosition( pointOnNav );
				npc.ScriptContext = ScriptingContext.SCRIPT_FORCED_MOVE;
				npc.InCine = true;



			}

		}
	}

	public void ProcessNavQueue()
	{
		if ( navQueue.Count == 0 )
			return;

		int processed = 0;
		int attempts = navQueue.Count;

		while ( processed < MaxNavCallsPerTick && attempts-- > 0 )
		{
			var req = navQueue.Dequeue();

			if ( !req.Controller.IsValid() || !req.Controller.IsAlive )
				continue;

			if ( !req.Controller.Agent.IsValid() )
			{
				navQueue.Enqueue( req );
				continue;
			}

			if ( navBudget <= 0 )
				break;

			req.Controller.NavStatus = NavigationStatus.NAVIGATION_STARTED;
			req.Controller.Agent.MoveTo( req.TargetPosition );
			navBudget--;
			processed++;

		}
	}

	public void RequestMove( NavData data )
	{
		pendingNavRequests[data.Controller] = new NavRequest
		{
			Controller = data.Controller,
			TargetPosition = data.Position
		};
	}

	protected virtual void Start()
	{
		//	playerReference = Current.Scene.GetComponents<BasePlayer>().FirstOrDefault();

		if ( _Init )
			return;

		if ( DebugAIManager ) Log.Info( "AIManager::Start() Running.." );

		currentNPCsInScene = new List<AIController>( maxAICount );
		_Init = true;
	}

	public bool TryConsumeNavBudget()
	{
		if ( navBudget <= 0 )
			return false;
		navBudget--;
		return true;
	}
	public float GetNavPressureFactor()
	{
		// 1.0 = normal, higher than 1 = slow down
		if ( NumAIs() <= 20 ) return 1.0f;
		if ( NumAIs() <= 50 ) return 1.5f;
		if ( NumAIs() <= 100 ) return 3.0f;
		return 3.0f;
	}
	protected virtual void UpdateNavigation()
	{
		if ( !_Init )
			return;

		navBudget = MaxNavCallsPerTick;

		foreach ( var req in pendingNavRequests.Values )
		{
			navQueue.Enqueue( req );
		}
		pendingNavRequests.Clear();

		ProcessNavQueue();
	}

	/// <summary>
	/// Draws a simple gizmo for a selected npc and where it's going.
	/// </summary>
	public void DrawSelectionDebug( AIController npc, Vector3? destination )
	{
		if ( !selectedNPCs.Contains( npc ) )
			return;

		const float halfWidth = 16f;
		float height = npc.Agent.IsValid() ? npc.Agent.Height
			: npc.BodyModel.IsValid() && npc.BodyModel.Model.IsValid() ? npc.BodyModel.Model.Bounds.Size.z
			: 72f;

		BBox bounds = new( new Vector3( -halfWidth, -halfWidth, 0f ), new Vector3( halfWidth, halfWidth, height ) );
		Transform transform = new( npc.WorldPosition );

		using ( Gizmo.Scope( "NPC selection", transform ) )
		{
			Gizmo.Draw.IgnoreDepth = false;
			Gizmo.Draw.Color = Color.Red.WithAlpha( 20f / 255f );
			Gizmo.Draw.SolidBox( bounds );
			Gizmo.Draw.Color = Color.Red;
			Gizmo.Draw.LineBBox( bounds );
		}

		if ( AIMotor.DebugLevel == 2 || destination is not Vector3 goal )
			return;

		using ( Gizmo.Scope( "NPC forced movement", new Transform( Vector3.Zero ) ) )
		{
			Gizmo.Draw.IgnoreDepth = true;
			Gizmo.Draw.Color = Color.Magenta;
			Gizmo.Draw.LineBBox( new BBox( goal - 5f, goal + 5f ) );

			var start = npc.WorldPosition;
			var delta = (goal - start).WithZ( 0f );
			var forward = delta.Length > 0.001f ? delta.Normal : npc.WorldRotation.Forward.WithZ( 0f ).Normal;
			var side = Vector3.Cross( forward, Vector3.Up ) * 8f;
			var neck = goal - forward * 32f;

			if ( delta.Length < 32f )
				start = neck;

			Gizmo.Draw.Line( start + side, neck + side );
			Gizmo.Draw.Line( neck + side, neck + side * 2f );
			Gizmo.Draw.Line( neck + side * 2f, goal );
			Gizmo.Draw.Line( goal, neck - side * 2f );
			Gizmo.Draw.Line( neck - side * 2f, neck - side );
			Gizmo.Draw.Line( neck - side, start - side );
		}
	}

	protected virtual void TickAISystem()
	{
		if ( !_Init || AIDisable )
			return;


#if IGNIS
		using ( var _ = PerformanceStats.Timings.AI.Scope() )
		{
#endif
		int senseCount = 0, decideCount = 0, executeCount = 0;

		foreach ( var agent in currentNPCsInScene )
		{
			if ( !agent.Active ) continue;


			if ( WorldTime.Now < agent.AIBrain._lastThinkTime + agent.AIBrain._currentThinkRate ) continue; // respect think rate

			switch ( agent.CurrentPhase )
			{
				case ThinkPhase.Sense:
					if ( senseCount++ >= MaxPhasesPerTick ) continue;
					if ( agent.CurrentSleepState == SleepState.SLEEPSTATE_WAIT_FOR_INPUT ) continue;
					agent.TickSense( Time.Delta );
					break;
				case ThinkPhase.Decide:
					if ( decideCount++ >= MaxPhasesPerTick ) continue;
					if ( agent.CurrentSleepState == SleepState.SLEEPSTATE_WAIT_FOR_INPUT ) continue;
					agent.TickDecide( Time.Delta );
					break;
				case ThinkPhase.Execute:
					if ( executeCount++ >= MaxPhasesPerTick ) continue;
					if ( agent.CurrentSleepState == SleepState.SLEEPSTATE_WAIT_FOR_INPUT ) continue;
					agent.TickExecute( Time.Delta );
					break;
			}
		}

		if ( WorldTime.Now >= _nextPhysicsResolve )
		{
			_nextPhysicsResolve = WorldTime.Now + physicsCheckRate;

			foreach ( var npc in physImpacts.Keys )
				ResolvePhysics( npc );
		}
#if IGNIS
		}
#endif
	}

	float physicsCheckRate = 1f;
	float _nextPhysicsResolve = 0f;
	int maxCallsPerInterval = 1;
	float minSignificantDamage = 15f;

	Dictionary<AIController, Queue<DamageInfo>> physImpacts = new();
	Dictionary<AIController, float> _lastPhysicsCheck = new();

	public void QueuePhysicsImpact( DamageInfo dmginfo, AIController npc )
	{
		if ( dmginfo.Damage <= minSignificantDamage )
			return;

		if ( !physImpacts.ContainsKey( npc ) )
			physImpacts[npc] = new Queue<DamageInfo>();

		physImpacts[npc].Enqueue( dmginfo );
	}

	protected virtual void ResolvePhysics( AIController caller )
	{
		if ( !_Init ) return;
		if ( !physImpacts.TryGetValue( caller, out var queue ) ) return;

		int callsThisInterval = 0;
		float damageToDeal = 0f;

		while ( queue.Count > 0 )
		{
			var hit = queue.Dequeue();
			if ( hit.Damage <= minSignificantDamage )
				continue;

			callsThisInterval++;

			if ( callsThisInterval > maxCallsPerInterval )
			{
				damageToDeal += hit.Damage;
			}
			else
			{
				var resolved = new DamageInfo
				{
					Damage = hit.Damage,
					Attacker = hit.Attacker,
					Position = hit.Position,
					Origin = hit.Origin,
				};
				caller.OnDamage( resolved );
			}
		}

		if ( damageToDeal > 0f )
		{
			int burstCount = callsThisInterval - maxCallsPerInterval;
			float penalty = 1f / (1f + burstCount * 0.5f); // more hits more reduction

			var burst = new DamageInfo
			{
				Damage = damageToDeal * penalty,
				Attacker = null

			};
			caller.OnDamage( burst );
		}
	}


	public int NumAIs() { return sceneAICount; }

	// AI::Spawn() should eventually just make a deferred call here, so manager retains full control of each instance from start to end
	public void CreateNPCInstance( AIController NPC, bool spawnNPC = true )
	{
		if ( DebugAIManager ) Log.Info( $"AIManager::CreateNPCInstance() Creating npc {NPC}." );
		AddAI( NPC );
		if ( spawnNPC )
		{
			NPC.Spawn();
		}
	}

	public void AddAI( AIController NPC )
	{
		if ( DebugAIManager ) Log.Info( $"AIManager::AddAI() {NPC} added to manager." );
		NPC.CurrentPhase = (ThinkPhase)(currentNPCsInScene.Count % 3);
		sceneAICount++;
		currentNPCsInScene.Add( NPC );

	}

	public void RemoveAI( AIController NPC )
	{
		if ( DebugAIManager ) Log.Info( $"AIManager::RemoveAI() {NPC.TargetName} removed from manager." );

		if ( NPC.AISquad is not null )
		{
			NPC.AISquad.RemoveMember( NPC );
		}

		sceneAICount--;
		currentNPCsInScene.Remove( NPC );
		DeselectNPC( NPC );
		NPC.Destroy();

	}

	[ConVar( "ai_debug_manager" )] public static bool DebugAIManager { get; set; } = false;

	[ConVar( "ai_debug_vision_sensing" )] public static bool AIDebugVisionSensing { get; set; } = false;

	[ConVar( "ai_debug_log_planning" )] public static bool AIDebugLogPlanning { get; set; } = false;
}

public class AIThinkScheduler : GameObjectSystem<AIThinkScheduler>
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

	private static readonly float[] ThinkOffsets =
	[
	0.0f, 0.150f, 0.075f, 0.225f, 0.030f, 0.180f, 0.120f, 0.270f,
	0.045f, 0.210f, 0.105f, 0.255f, 0.015f, 0.165f, 0.090f, 0.240f,
	0.135f, 0.060f, 0.195f, 0.285f
	];

	public static float GetNextThinkTime()
	{
		if ( timeSinceLastFrame > Time.Delta ) // new frame
		{
			spawnedThisFrame = 0;
			timeSinceLastFrame = 0;
		}

		float offset = ThinkOffsets[spawnedThisFrame % ThinkOffsets.Length];
		spawnedThisFrame++;
		return WorldTime.Now + offset;
	}
}
