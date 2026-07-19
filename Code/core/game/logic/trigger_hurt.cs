using Sandbox.Diagnostics;

namespace Core;

[Icon( "crisis_alert" )]
public class trigger_hurt : BaseTrigger
{
	/// <summary>
	/// When expected to damage more than 100 props at the same time as physics calculation at that
	/// range is unrealistic with this entity trying to hurt as many of those at the same time otherwise.
	/// </summary>
	[Property, Title( "Expensive Debris Cleanup" )] public bool b_OptimizePhys { get; set; } = false;

	/// <summary>
	/// The amount of damage done to entities that touch this trigger. The damage is done every half-second. 
	/// See also 'Damage Model' for extra details on how damage can be dealt.
	/// </summary>
	[Property, Order( 3 )] public float Damage { get; set; } = 10f;
	private const float newDamageRate = 2;

	/// <summary>
	/// Maximum damage dealt per second. This field is only used if you select the Doubling w/Forgiveness damage model, via the spawnflag.
	/// </summary>
	[Property, Order( 4 )] public float DamageCap { get; set; } = 20f;

	/// <summary>
	/// Default is equivalent to Source 1. By default hurts the player using DefaulRate
	/// If we tick AlwaysThinkEveryFrame (expensive!), we hurt the player evey frame in specific scenarios 
	/// </summary>
	public float Rate => AlwaysThinkEveryFrame ? ExpensiveRate : DefaultRate;
	public IEnumerable<Prop> CleanUp;


	private const float DefaultRate = 0.5f;
	private const float ExpensiveRate = 0.016f;

	private float RealTimer { get; set; } = 0;
	private bool isRealTimer { get; set; } = true;

	private float cleanupTimer = 0f;
	private const float CleanupInterval = 5f; // every 5 seconds

	private float cleanupCooldown = 0f;
	private const float CleanupRate = 0.25f; // once every 0.25 seconds

	public override bool IsTouchable => true;

	private Dictionary<GameObject, float> lastDamageTime = new();
	private Dictionary<GameObject, float> damagePerTarget = new();

	public enum DamageModel
	{
		Normal,
		DoublingWithForgiveness
	}

	[Property, Order( 5 )] public DamageTagSet _DamageType { get; set; }

	/// <summary>
	/// How damage is dealt. Normal always does the specified amount of damage each half second. 
	/// Doubling starts with the specified amount and doubles it each time it hurts the toucher. 
	/// Forgiveness means that if the toucher gets out of the trigger the damage will reset to the specified value. 
	/// Good for making triggers that are deadly over time without having to cause massive damage on each touch.
	/// </summary>
	[Property, Order( 6 )] public DamageModel _DamageModel { get; set; }

	/// <summary>
	/// When doubling with forgiveness, how many seconds the trigger must go without hurting anything before the damage is reset.
	/// </summary>
	[Property, Order( 7 )] public float ForgivenessDelay { get; set; } = 3;

	/// <summary>
	/// Should the damaged entity receive no physics force from this trigger.
	/// </summary>
	[Property, Order( 8 )] public bool ZeroDamageForce { get; set; } = false;

	[Property, Order( 9 )] public Vector3 DamageForceOverride { get; set; }

	/// <summary>
	/// Normally triggers think every half second, in some cases you may need to request it to damage every frame. This is expensive!
	/// </summary>
	[Property, Order( 10 )] public bool AlwaysThinkEveryFrame { get; set; } = false;
#if IGNIS
	[DebugExpose]
#endif
	[ShowIf( nameof( isDebug ), false ), Feature( "Debug" ), Title( "RealTime has passed: " ), Property] public float f_RealTimer => isDebug ? RealTimer : 0;

	[ShowIf( nameof( isDebug ), false ), Feature( "Debug" ), Title( "TimeSince last damaged: " ), Property] public bool b_RealTimer => isDebug && isRealTimer;
#if IGNIS
	[DebugExpose]
#endif
	[ShowIf( nameof( isDebug ), false ), Feature( "Debug" ), Title( "Last Damage Dealt:" ), Property, ReadOnly] public float lastDamageValue = 0f;


	[ShowIf( nameof( isDebug ), false ), Feature( "Debug" ), Property, ReadOnly] public int Mintime;
	[ShowIf( nameof( isDebug ), false ), Feature( "Debug" ), Property, ReadOnly] public int MaxTime;

	protected override void OnStart()
	{
		base.OnStart();

		isRealTimer = false;
	}

	protected override void OnFixedUpdate()
	{
		if ( !isEnabled || !isRealTimer )
			return;

		RealTimer += Time.Delta;

		// Run damage logic only on proper tick rate
		if ( RealTimer >= Rate )
		{
			RealTimer = 0;
			TryHurt();
		}

		// Periodic cleanup of damage tracking dictionaries
		cleanupTimer += Time.Delta;
		if ( cleanupTimer >= CleanupInterval )
		{
			cleanupTimer = 0f;
			CleanupExpiredEntries();
		}

		// Additional optimization pass for physics prop cleanup
		if ( b_OptimizePhys )
		{
			cleanupCooldown += Time.Delta;
			if ( cleanupCooldown >= CleanupRate )
			{
				cleanupCooldown = 0f;
				TryCleanUp();
			}
		}
	}

	protected override void OnTriggerIn()
	{
		base.OnTriggerIn();

		isRealTimer = true;
	}

	protected override void OnTriggerOut()
	{
		base.OnTriggerOut();

		isRealTimer = false;

		foreach ( var obj in GetTrackedItems() )
		{
			lastDamageTime.Remove( obj );
			damagePerTarget.Remove( obj );
		}
	}


	protected async override void OnItemsEmpty()
	{
		base.OnItemsEmpty();

		RealTimer = 0;
		isRealTimer = false;

		if ( _DamageModel == DamageModel.Normal )
			return;

		if ( _DamageModel == DamageModel.DoublingWithForgiveness )
		{
			var expectedTime = Time.Now + ForgivenessDelay;

			while ( Time.Now < expectedTime )
			{
				await Task.Delay( 50 );

				// If any new items re-entered, abort reset
				if ( GetTrackedItems().Any( go => go.IsValid() ) )
					return;
			}

			// Reset all per-target damage + timers
			damagePerTarget.Clear();
			lastDamageTime.Clear();
		}
	}

	private List<IDamageable> _tryHurtList { get; set; } = [];

	private void TryHurt()
	{
		var collider = GameObject.GetComponent<Collider>();
		if ( !collider.IsValid() || collider.Touching is null )
			return;

		foreach ( var touching in collider.Touching )
		{
			if ( !touching.IsValid() )
				continue;

			var damageables = touching.GetComponentsInParent<IDamageable>();
			if ( damageables is null )
				continue;

			foreach ( var target in damageables )
			{
				if ( target is null )
					continue;

				if ( _tryHurtList.Contains( target ) )
					continue;

				var targetObject = (target as Component)?.GameObject;
				if ( !targetObject.IsValid() )
					continue;

				bool isDebris = targetObject.Tags.Has( "debris" );

				// Exclude debris unless PhysicsDebris is enabled
				if ( (spawnFlags & SpawnFlags.PhysicsDebris) == 0 && isDebris )
					continue;

				_tryHurtList.Add( target );
			}
		}

		if ( _tryHurtList is null )
			return;

		var processedObjects = new HashSet<GameObject>();
		var now = Time.Now;

		foreach ( var target in _tryHurtList.Take( 15 ) )
		{
			var targetObject = (target as Component)?.GameObject;
			if ( !targetObject.IsValid() || !processedObjects.Add( targetObject ) )
				continue;

			// Rate limit per target
			if ( lastDamageTime.TryGetValue( targetObject, out var lastTime ) && now - lastTime < Rate )
				continue;

			lastDamageTime[targetObject] = now;

			float damageToApply = Damage;

			if ( _DamageModel == DamageModel.DoublingWithForgiveness )
			{
				if ( !damagePerTarget.TryGetValue( targetObject, out var current ) )
					current = Damage;
				else
					current = MathX.Clamp( current * newDamageRate, Damage, DamageCap );

				damagePerTarget[targetObject] = current;
				damageToApply = current;
			}

			if ( isDebug )
				lastDamageValue = damageToApply;

			var damageInfo = new DamageInfo
			{
				Attacker = GameObject,
				Damage = damageToApply,
				Tags = { _DamageType }
			};

			if ( ShouldDamageTarget( target ) )
				target.OnDamage( damageInfo );
		}
	}

	private bool ShouldDamageTarget( IDamageable target )
	{
		return target switch
		{
			BasePlayer _ when (spawnFlags & SpawnFlags.Clients) != 0 => true,
			GameProp _ when (spawnFlags & SpawnFlags.PhysicsObjects) != 0 => true,
			Prop _ when (spawnFlags & SpawnFlags.PhysicsObjects) != 0 => true,
			Gib _ when (spawnFlags & SpawnFlags.PhysicsDebris) != 0 => true,
			_ when spawnFlags == SpawnFlags.Everything => true,
			_ => false,
		};
	}

	private void TryCleanUp()
	{
		var count = GetTrackedItems().Count();

		if ( count >= 60 ) { Mintime = 50; MaxTime = 700; }
		if ( count >= 150 ) { Mintime = 200; MaxTime = 600; }

		CleanUp = this.GameObject.GetComponent<MeshComponent>().Touching
			.SelectMany( x => x.GetComponents<Prop>().Distinct() )
			.Where( target =>
			{
				var targetObject = (target as Component)?.GameObject;
				return targetObject.IsValid();
			} );

		foreach ( var targetObject in CleanUp )
		{
			if ( targetObject.Health.AlmostEqual( 0f, 0.01f ) )
			{
				var gibs = targetObject?.GetComponentInChildren<Gib>();
				var rigidgibs = targetObject?.GetComponent<Rigidbody>();

				if ( isDebug ) gibs.Tint = Color.Red;
				var randomDelay = Game.Random.Int( Mintime, MaxTime );

				if ( PerformanceStats.FrameTime > 0.009 )
				{
					// gibs.Kill(); // disabled for now
				}

				rigidgibs.MotionEnabled = false;
			}
		}
	}

	private void CleanupExpiredEntries()
	{
		var now = Time.Now;
		var expired = lastDamageTime
			.Where( kvp => now - kvp.Value > ForgivenessDelay + 1 )
			.Select( kvp => kvp.Key )
			.ToList();

		foreach ( var key in expired )
		{
			lastDamageTime.Remove( key );
			damagePerTarget.Remove( key );
		}
	}

}
