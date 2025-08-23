using Sandbox.Diagnostics;

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

	[DebugExpose]
	[HideIf( "isDebug", false )][Feature( "Debug" ), Title( "RealTime has passed: " ), Property] public float f_RealTimer => isDebug ? RealTimer : 0;

	[HideIf( "isDebug", false )][Feature( "Debug" ), Title( "TimeSince last damaged: " ), Property] public bool b_RealTimer => isDebug && isRealTimer;

	[DebugExpose]
	[HideIf( "isDebug", false )][Feature("Debug"), Title("Last Damage Dealt:"), Property, ReadOnly] public float lastDamageValue = 0f;


	[HideIf( "isDebug", false )][Feature( "Debug" ), Property, ReadOnly] public int Mintime;
	[HideIf( "isDebug", false )][Feature( "Debug" ), Property, ReadOnly] public int MaxTime;

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

	private void TryHurt()
	{
		var targets = GameObject.GetComponent<Collider>()?.Touching?
			.SelectMany( x => x?.GetComponentsInParent<IDamageable>()?.Distinct() ?? Enumerable.Empty<IDamageable>() )
			.Where( target =>
			{
				var targetObject = (target as Component)?.GameObject;
				return targetObject != null && (!b_PhysicsDebris || !targetObject.Tags.Has( "debris" ));
			} );

		if ( targets == null )
			return;

		var processedObjects = new HashSet<GameObject>();
		var now = Time.Now;

		foreach ( var target in targets.Take( 15 ) )
		{
			var targetObject = (target as Component)?.GameObject;
			if ( targetObject == null || !processedObjects.Add( targetObject ) )
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
		// Let's control what will get damage based on our spawnflag choices.
		//		var targetObject = (target as Component)?.GameObject;

		switch ( target )
		{
			case BasePlayer _ when b_Clients:
				return true;
			case Core.GameProp _ when b_PhysicsObjects:
				return true;
			case Prop _ when b_PhysicsObjects:
				return true;
			case Gib _ when b_PhysicsDebris:
				return true;
			default:
				return b_Everything;
		}
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
				return targetObject != null;
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
