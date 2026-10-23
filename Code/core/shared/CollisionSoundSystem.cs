#if FMOD
namespace Core;

using FMODSbox;
using System;

/// <summary>
/// This system exists to collect pending collision sounds and filter them into a unique set, to avoid
/// unnesssary sounds playing, when they're going to be making the same sound anyway.
/// </summary>
public partial class CollisionSoundSystem : GameObjectSystem<CollisionSoundSystem>, ISceneCollisionEvents, IScenePhysicsEvents
{
	private record struct PendingSound( Surface Surface, Vector3 Position, float Speed, bool Networked );

	private readonly List<PendingSound> _pending = [];

	public CollisionSoundSystem( Scene scene ) : base( scene )
	{
		Listen( Stage.FinishUpdate, 100, ProcessQueue, "CollisionSoundSystem Queue" );
#if IGNIS
		Listen( Stage.FinishUpdate, 110, UpdateFriction, "CollisionSoundSystem Friction" );
#endif
	}

	/// <summary>
	/// Register this physics collision with the sound system
	/// </summary>
	public void RegisterCollision( in Collision collision )
	{
		var self = collision.Self;
		var other = collision.Other;

		if ( !self.Body.EnableCollisionSounds ) return;
		if ( !other.Body.EnableCollisionSounds ) return;

		// Assume networked if colliding with an object replicated to clients
		var networkRoot = other.GameObject?.NetworkRoot;
		var networked = networkRoot is { NetworkMode: NetworkMode.Object } && networkRoot.Network?.IsProxy == false;

		AddShapeCollision( other.Shape, other.Surface, collision.Contact, networked );
		AddShapeCollision( self.Shape, self.Surface, collision.Contact, networked );
	}

	void ISceneCollisionEvents.OnCollisionHit( Collision collision )
	{
		RegisterCollision( collision );
	}

	/// <summary>
	/// Add a collision sound for this shape
	/// </summary>
	public void AddShapeCollision( PhysicsShape shape, Surface surface, in Vector3 position, float speed, bool networked )
	{
		if ( !shape.IsValid() ) return;
		if ( !shape.Body.IsValid() ) return;
		if ( !shape.Body.EnableCollisionSounds ) return;
		if ( speed < 100.0f ) return;
		if ( surface is null ) return;

		// If we have more than 4, remove any that are slower/less significant
		if ( _pending.Count > 4 && _pending.RemoveAll( x => x.Speed < speed ) == 0 )
			return;

		// check for redundancies
		for ( int i = 0; i < _pending.Count; i++ )
		{
			if ( _pending[i].Surface.Index != surface.Index ) continue;
			if ( _pending[i].Speed >= speed ) return;

			_pending[i] = new PendingSound( surface, position, speed, networked );
			return;
		}

		_pending.Add( new PendingSound( surface, position, speed, networked ) );
	}

	public void AddShapeCollision( PhysicsShape shape, Surface surface, in PhysicsContact contact, bool networked )
	{
		AddShapeCollision( shape, surface, contact.Point, MathF.Abs( contact.NormalSpeed ), networked );
	}

	private RealTimeSince _lastRan = 0;

	/// <summary>Create the pending sounds</summary>
	private void ProcessQueue()
	{
		if ( _lastRan < 0.05f ) return;
		_lastRan = 0;

		foreach ( var pending in _pending )
		{
			if ( pending.Surface is null ) continue;

			if ( pending.Networked )
			{
				PlayCollisionSound( (ushort)pending.Surface.Index, pending.Position, pending.Speed );
			}
			else
			{
				PlayCollisionSound( pending.Surface, pending.Position, pending.Speed );
			}
		}

		_pending.Clear();
	}

	[Rpc.Broadcast( NetFlags.Unreliable )]
	private static void PlayCollisionSound( ushort index, Vector3 pos, float speed )
	{
		var surface = Surface.FindByIndex( index );
		if ( !surface.IsValid() ) return;

		PlayCollisionSound( surface, pos, speed );
	}

	/// <summary>Play a collision sound based on this shape's surface. Can return null if sound is invalid, or too quiet to play.</summary>
	static private FMOD.Studio.EventInstance PlayCollisionSound( Surface surface, Vector3 position, float speed = 320.0f )
	{
		float volume = speed / 1000.0f;
		if ( volume > 1.0f ) volume = 1.0f;
		if ( volume < 0.001f ) return FMODSound.Null();

		var sound = SolveSurfaceImpactSoftness( surface, speed < 200f );

		var s = FMODSound.Play( sound, position );
		s.setVolume( 0.5f );

		var surf = BasePlayer.SolveSurfaceParameter( surface );

		if ( !string.IsNullOrWhiteSpace( surf ) ) FMODSound.SetParameter( s, "parameter:/Physics/MaterialType", surf );

		return s;
	}

	/// <summary>Get the working impact event path, depending on the surface inheritance chain and the intended softness</summary>
	/// <param name="surface">Surface</param>
	/// <param name="soft">Are we looking for soft or rough</param>
	/// <returns>Event path or null</returns>
	private static FMODEventResource SolveSurfaceImpactSoftness( Surface surface, bool soft ) => soft ? surface.FindInParents( static s => SurfaceExtension.FindForResourceOrDefault( s ).SoundCollection.ImpactSoft ) : surface.FindInParents( static s => SurfaceExtension.FindForResourceOrDefault( s ).SoundCollection.ImpactHard );
}
#endif
