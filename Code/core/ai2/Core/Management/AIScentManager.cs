using System;
using System.Collections.Generic;
using System.Text;

namespace Core.AI;

public enum ScentCategory
{
	Food,
	Creature,
	Blood,
	Corpse,
	Player,
	Pheromone
}

// small class that handles emitting a smell. can live on an entity (like an npc) or be a oneshot that decays over time
public class ScentEmitter
{
#if IGNIS
	[SaveRestore] 
#endif
	public ScentCategory Category { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public Vector3 Position { get; set; }
#if IGNIS
	[SaveRestore] 
#endif
	public BaseEntity? SourceEnt { get; set; }

#if IGNIS
	[SaveRestore] 
#endif
	public float Intensity { get; set; } = 1f;     // source strength at the emitter itself
#if IGNIS
	[SaveRestore] 
#endif
	public float Radius { get; set; } = 512f;      // max distance the scent can travel
#if IGNIS
	[SaveRestore] 
#endif
	public float DecayRate { get; set; } = 0f;     // 0 = persistent (a corpse), >0 = fades out (like a pheromone or blood spill)

	public void Tick()
	{
		if ( SourceEnt.IsValid() )
		{
			Position = SourceEnt.WorldPosition;
		}

		if ( DecayRate > 0f )
			Intensity = MathF.Max( 0f, Intensity - DecayRate * Time.Delta );
	}

	public bool IsExpired => DecayRate > 0f && Intensity <= 0f;
}

public class AIScentManager : GameObjectSystem<AIScentManager>
{
	public AIScentManager( Scene scene ) : base( scene )
	{

		Listen( Stage.PhysicsStep, 0, TickAll, "AIScentManager TickAll" );

	}
	private readonly List<ScentEmitter> _emitters = new();
	public IReadOnlyList<ScentEmitter> All => _emitters;

	public Vector3 Wind = Vector3.Zero; // heheheh maybe one day???

	public void Register( ScentEmitter e ) => _emitters.Add( e );
	public void Unregister( ScentEmitter e ) => _emitters.Remove( e );

	public void TickAll()
	{
		for ( int i = _emitters.Count - 1; i >= 0; i-- )
		{
			_emitters[i].Tick();
			if ( _emitters[i].IsExpired )
				_emitters.RemoveAt( i );
		}
	}
}
