#if FMOD && IGNIS
using System;
using FMOD.Studio;
using FMODSbox;
using Sandbox.Diagnostics;

namespace Core;

public partial class CollisionSoundSystem
{
	private const float _fadeRate = 10.0f;
	private const float _scrapeMinPower = 75.0f;
	private const float _scrapeStartVolume = 0.1f;
	private const float _slipEpsilon = 1.0f;
	private const float _scrapePowerScale = 1.0f / 15500.0f;
	private const float _silenceThreshold = 1.0f / 128.0f;
	private const int _maxContacts = 8;
	private const int _maxScrapeVoices = 4;

	private readonly record struct ScrapeKey( GameObject Owner, PhysicsBody Body );

	private class Voice
	{
		public EventInstance Instance;
		public string MaterialName;
		public FMODEventResource ScrapePath;
		public FMODEventResource PlayingScrapePath;
		public string PlayingMaterialName;
		public float BaseVolume;
		public float Volume;
		public float TargetVolume;
		public Vector3 Position;
		public double NextStartTime;
		public bool PlaybackFailed;
		public PhysicsBody Body;
	}

	private readonly Dictionary<ScrapeKey, Voice> _scrapeVoices = [];
	private readonly List<ScrapeKey> _finished = [];
	private double _lastScrapeStep = double.NegativeInfinity;

	void IScenePhysicsEvents.PrePhysicsStep()
	{
		if ( Application.IsHeadless || Time.Delta <= 0.0f ) return;

		using var _ = PerformanceStats.Timings.Audio.Scope();
		_lastScrapeStep = Time.NowDouble;

		foreach ( var voice in _scrapeVoices.Values ) voice.TargetVolume = 0.0f;
	}

	internal void OnBodyActive( PhysicsBody3d body, in Vector3 linearVelocity, in Vector3 angularVelocity )
	{
		if ( Application.IsHeadless || Time.Delta <= 0.0f || !body.IsValid || !body.EnableCollisionSounds )
			return;

		Span<PhysicsBody3d.ScrapeContact> contacts = stackalloc PhysicsBody3d.ScrapeContact[_maxContacts];
		Evaluate( body, contacts, linearVelocity, angularVelocity );
	}

	private void UpdateFriction()
	{
		if ( Application.IsHeadless || Time.Delta <= 0.0f || _scrapeVoices.Count == 0 ) return;

		using var _ = PerformanceStats.Timings.Audio.Scope();
		UpdateVoices( Time.Delta );
	}

	private void Evaluate( PhysicsBody3d body3d, Span<PhysicsBody3d.ScrapeContact> contacts, in Vector3 linearVelocity, in Vector3 angularVelocity )
	{
		var count = body3d.GetScrapeContacts( contacts );
		if ( count == 0 )
			return;
		var body = body3d.Owner;
		GameObject owner = null;
		var rotating = angularVelocity.LengthSquared > 1e-8f;
		var initialized = false;
		var scrapePower = 0.0f;
		var dominantPower = 0.0f;
		var scrapePos = Vector3.Zero;
		FMODEventResource scrapeSound = null;
		string scrapeMat = null;

		for ( int i = 0; i < count; i++ )
		{
			ref readonly var contact = ref contacts[i];
			if ( contact.FrictionForce <= 0.0f ) continue;

			var surface = contact.Surface;

			if ( !surface.IsValid() || !surface.HasScrapeSounds ) continue;

			if ( !initialized )
			{
				owner = BodySource( body );
				initialized = true;
			}

			var other = contact.OtherBody;
			if ( other.IsValid() && (!other.EnableCollisionSounds || (owner is not null && BodySource( other ) == owner)) ) continue;

			var otherVelocity = other.IsValid() ? other.GetVelocityAtPoint( contact.Point ) : Vector3.Zero;
			var bodyVelAtPoint = rotating ? body.GetVelocityAtPoint( contact.Point ) : linearVelocity;
			var surfaceVelocity = body3d.GetSurfaceVelocity( contact.SelfSurfaceIndex );

			if ( other.IsValid() && other._body is PhysicsBody3d otherBody3d ) surfaceVelocity -= otherBody3d.GetSurfaceVelocity( contact.OtherSurfaceIndex );

			var relative = bodyVelAtPoint - otherVelocity - surfaceVelocity;
			var slipSquared = (relative - contact.Normal * Vector3.Dot( relative, contact.Normal )).LengthSquared;

			if ( slipSquared <= _slipEpsilon * _slipEpsilon ) continue;

			var sound = GetScrapePath( contact.OtherSurfaceIndex );
			var mat = GetScrapeMaterial( contact.OtherSurfaceIndex );
			if ( string.IsNullOrWhiteSpace( sound ) ) continue;
			if ( string.IsNullOrWhiteSpace( mat ) ) continue;

			var power = contact.FrictionForce * MathF.Sqrt( slipSquared );
			scrapePower += power;

			if ( power <= dominantPower ) continue;

			dominantPower = power;
			scrapeSound = sound;
			scrapeMat = mat;
			scrapePos = contact.Point;
		}

		if ( !scrapeSound.IsValid() ) return;
		if ( string.IsNullOrWhiteSpace( scrapeMat ) ) return;
		var mass = body.Mass;
		var powerPerMass = mass > 0.0f ? scrapePower / mass : scrapePower;

		if ( powerPerMass < _scrapeMinPower ) return;

		var scaled = powerPerMass * _scrapePowerScale;
		SetTarget( new( owner, owner is null ? body : null ), body, scrapeSound, scrapePos, (scaled * scaled).Clamp( 0.0f, 1.0f ), scrapeMat );
	}

	private static GameObject BodySource( PhysicsBody body ) => body.GameObject;

	private void SetTarget( ScrapeKey key, PhysicsBody body, FMODEventResource sound, Vector3 position, float volume, string material )
	{
		if ( volume <= _silenceThreshold ) return;

		if ( !_scrapeVoices.TryGetValue( key, out var voice ) )
		{
			if ( volume <= _scrapeStartVolume ) return;

			_scrapeVoices[key] = voice = new Voice();
		}
		else if ( volume <= voice.TargetVolume ) return;

		voice.ScrapePath = sound;
		voice.MaterialName = material;
		voice.Body = body;
		voice.TargetVolume = volume;
		voice.Position = position;
	}

	private void UpdateVoices( float dt )
	{
		_finished.Clear();
		var playing = 0;
		foreach ( var voice in _scrapeVoices.Values )
		{
			if ( voice.Instance.isValid() && FMODSound.IsPlaying( voice.Instance ) ) playing++;
		}
		foreach ( var (key, voice) in _scrapeVoices )
		{
			if ( (key.Owner is not null && !key.Owner.IsValid())
			|| !voice.Body.IsValid()
			|| !voice.Body.EnableCollisionSounds
			|| Time.NowDouble - _lastScrapeStep > 0.1
			|| string.IsNullOrWhiteSpace( voice.MaterialName ) )
				voice.TargetVolume = 0.0f;

			if ( voice.PlayingMaterialName != voice.MaterialName && voice.PlayingScrapePath != voice.ScrapePath )
			{
				if ( voice.Instance.isValid() && !FMODSound.IsPlaying( voice.Instance ) ) playing--;

				FMODSound.Stop( voice.Instance );
				voice.PlayingMaterialName = voice.MaterialName;
				voice.NextStartTime = 0.0;
				voice.PlaybackFailed = false;
			}

			voice.Volume = voice.Volume.Approach( voice.TargetVolume, _fadeRate * dt );
			var starting = false;

			if ( playing < _maxScrapeVoices
				&& voice.TargetVolume > _scrapeStartVolume && Time.NowDouble >= voice.NextStartTime
				&& (!voice.Instance.isValid() || !FMODSound.IsPlaying( voice.Instance )) )
			{
				voice.NextStartTime = Time.NowDouble + 1.0;

				voice.Instance = FMODSound.Create( voice.ScrapePath );
				if ( !string.IsNullOrWhiteSpace( voice.MaterialName ) ) FMODSound.SetParameter( voice.Instance, "parameter:/Physics/MaterialType", voice.MaterialName );

				FMODSound.Play( voice.Instance, voice.Body.GameObject );

				if ( !voice.Instance.isValid() )
				{
					if ( !voice.PlaybackFailed ) Log.Warning( $"Couldn't play scrape sound {voice.MaterialName}" );
					voice.PlaybackFailed = true;
				}
				else if ( voice.BaseVolume * voice.TargetVolume <= _scrapeStartVolume )
				{
					FMODSound.Stop( voice.Instance );
				}
				else
				{
					voice.PlaybackFailed = false;
					voice.NextStartTime = 0.0;
					starting = true;
					playing++;
				}
			}
			if ( voice.Instance.isValid() )
			{
				voice.Instance.setVolume( voice.BaseVolume );

				if ( !starting )
				{
					voice.Instance.getPitch( out float prepitch, out float pitch );
					voice.Instance.setPitch( ((1.5f * voice.TargetVolume) - prepitch) * (_fadeRate * dt).Clamp( 0.0f, 1.0f ) );
				}
			}
			if ( voice.TargetVolume <= _silenceThreshold && voice.Volume <= _silenceThreshold )
			{
				if ( voice.Instance.isValid() && !FMODSound.IsPlaying( voice.Instance ) ) playing--;
				FMODSound.Stop( voice.Instance );
				_finished.Add( key );
			}
		}
		foreach ( var key in _finished ) _scrapeVoices.Remove( key );
	}

	private void DisposeFriction()
	{
		foreach ( var voice in _scrapeVoices.Values ) FMODSound.Stop( voice.Instance );
		_scrapeVoices.Clear();
		_finished.Clear();
	}

	private static string GetScrapeMaterial( int otherSurfaceIndex ) => BasePlayer.SolveSurfaceParameter( Surface.All.GetValueOrDefault( otherSurfaceIndex ) );
	private static FMODEventResource GetScrapePath( int otherSurfaceIndex ) => GetScrapePath( Surface.All.GetValueOrDefault( otherSurfaceIndex ) );
	private static FMODEventResource GetScrapePath( Surface other ) => SolveSurfaceScrapeSoftness( other, other.ScrapeEffects.RoughnessFactor < other.ScrapeEffects.RoughThreshold );

	/// <summary>Get the working impact event path, depending on the surface inheritance chain and the intended softness</summary>
	/// <param name="surface">Surface</param>
	/// <param name="smooth">Are we looking for smooth or rough</param>
	/// <returns>Event path or null</returns>
	private static FMODEventResource SolveSurfaceScrapeSoftness( Surface surface, bool smooth ) => smooth ? SurfaceExtension.FindForResourceOrDefault( surface ).SoundCollection.ScrapeSmooth : SurfaceExtension.FindForResourceOrDefault( surface ).SoundCollection.ScrapeRough;
}
#endif
