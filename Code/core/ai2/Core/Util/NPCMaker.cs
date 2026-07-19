using System;

namespace Core.AI
{
	public class NPCMaker : BaseEntity
	{
		/// <summary>
		/// The NPC to be spawned, according to the definition
		/// </summary>
		[Property] public NpcDefinition NpcToSpawn { get; set; }
		/// <summary>
		/// Amount to spawn
		/// </summary>
		[Property] public int amountToSpawn { get; set; }
		/// <summary>
		/// Radius in units of which to spawn the NPCs
		/// </summary>
		[Property] public float spawnRadius { get; set; }
		/// <summary>
		/// Maximum amount of NPCs to spawn.
		/// </summary>
		[Property, ShowIf( nameof( infinite ), false )] public int maxLiveNpcs { get; set; }
		/// <summary>
		/// Delay in seconds between spawning NPCs. -1 spawns one when the last created one dies.
		/// </summary>
		[Property] public float delayBetweenSpawn { get; set; }
		/// <summary>
		/// Targetname given to this NPC
		/// </summary>
		[Property] public string spawnedTargetName { get; set; }

		/// <summary>
		/// Squadname given to NPCs created by this entity
		/// </summary>
		[Property] public string spawnedSquadName { get; set; }
		[Property] public bool infinite { get; set; }
		// If set, npc spawns yawing in a random direction. otherwise faces the direction of the maker gameobject
		[Property] public bool faceRandomDirection { get; set; }

		bool isEnabled = false;
		int spawnedNPCs = 0;
		public TimeSince lastTimeSpawnedNPC;

		static Material ghost => Material.Load( "materials/dev/ghost.vmat" );
		protected override string GetEditorVis()
		{
			string className = GetType().Name.ToLower();
			return $"materials/editor/npc_maker.vtex";
		}

		protected override void EntityDefaultGizmo( string editorVis, bool isModel )
		{
			if ( string.IsNullOrEmpty( GetEditorVis() ) ) return;
			base.EntityDefaultGizmo( GetEditorVis(), isModel );

			if ( Gizmo.IsSelected )
			{
				DrawGizmoModels();

				Gizmo.Draw.Color = Color.Yellow;
				Gizmo.Draw.LineSphere( Vector3.Zero, spawnRadius );

			}
			else if ( Gizmo.IsHovered )
			{
				Gizmo.Draw.Color = Color.White.WithAlpha( (((float)Math.Sin( Time.Now * 20f )) * 0.3f) + 0.7f );
				Gizmo.Draw.LineSphere( Vector3.Zero, spawnRadius );
			}


		}

		public void DrawGizmoModels()
		{
			if ( !NpcToSpawn.IsValid() ) return;

			Model vmdl = Model.Load( NpcToSpawn.Models.First().Name );
			Gizmo.Draw.Color = NpcToSpawn.IsValid() ? Color.Magenta : Color.White;

			Gizmo.Hitbox.Model( vmdl );

#if IGNIS
			Gizmo.Draw.Model( vmdl, ghost.IsValid() && NpcToSpawn.IsValid() ? ghost : null );
#else
		Gizmo.Draw.Model( vmdl );
#endif

		}

		[Button]
		public void TestSpawner()
		{
			EnableSpawner();
		}

		public void EnableSpawner()
		{
			isEnabled = true;
		}

		protected override void OnFixedUpdate()
		{
			if ( !isEnabled ) return;
			if ( spawnedNPCs == amountToSpawn ) return;

			if ( infinite )
			{
				HandleSpawnInfinite();
			}
			else
			{
				HandleSpawnFinite();
			}
		}

		/// <summary>
		/// Amount to spawn. If delay is zero, we just feed it the amount to spawn... unless infinite is checked, in which case we spew a warning and slam delay to 2 seconds
		/// </summary>
		/// <param name="amountToSpawn"></param>
		/// <param name="inf"></param>
		private void SpawnInRadius( bool inf, int amountToSpawn = 1 )
		{
			if ( inf && delayBetweenSpawn <= 0f )
			{
				Log.Warning( "NPCMaker: NPC spawn delay set to zero with infinite flag checked, slamming to 2 second delay" );
				delayBetweenSpawn = 2f;
			}

			var point = Scene.NavMesh.GetRandomPoint( WorldPosition, spawnRadius );
			if ( !point.HasValue )
			{
				Log.Warning( $"No NavMesh surface found within spawn radius of {this}. Attempting to grab a position on the closest NavMesh." );
				var test = Scene.NavMesh.GetClosestPoint( WorldPosition );
				var resample = Scene.NavMesh.GetRandomPoint( test.Value, spawnRadius );
				if ( !resample.HasValue )
				{
					Log.Warning( $"NavMesh resample failed for {this}. Did you forget to build the NavMesh?" );
					return;
				}
				point = resample;
			}

			var npc = Scene.CreateObject().Components.Create<AIController>( false );
			npc.Definition = NpcToSpawn;
			npc.GameObject.WorldPosition = point.Value;
			npc.GameObject.WorldRotation = faceRandomDirection ? Rotation.FromYaw( Game.Random.Float( 0, 360 ) ) : WorldRotation;
			npc.GameObject.Name = spawnedTargetName;
			npc.squadName = spawnedSquadName;
			npc.Enabled = true;
			//	npc.Spawn();
			lastTimeSpawnedNPC = 0;
			spawnedNPCs++;
		}

		private void HandleSpawnInfinite()
		{
			if ( lastTimeSpawnedNPC >= delayBetweenSpawn )
			{
				SpawnInRadius( true, amountToSpawn );
			}
		}

		private void HandleSpawnFinite()
		{
			if ( delayBetweenSpawn <= 0f || lastTimeSpawnedNPC >= delayBetweenSpawn )
			{
				SpawnInRadius( false );
				if ( spawnedNPCs >= amountToSpawn )
					isEnabled = false;
			}
		}
	}
}
