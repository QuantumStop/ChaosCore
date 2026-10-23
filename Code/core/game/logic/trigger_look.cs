namespace Core;

/// <summary>Trigger volume for detecting player's gaze</summary>
[Icon( "looks" )]
public class trigger_look : BaseTrigger
{
	public new delegate void ChaosOutput( trigger_look activator );

	[Group( "SpawnFlags" )]
	[ActionGraphIgnore]
	[Property] public bool FireOnce { get; set; } = false;


	/// <summary>he GameObject to be looked at.</summary>
	[Property, Order( 3 )] public GameObject LookTarget { get; set; }

	/// <summary>
	/// The time, in seconds, that the player must look the target before firing the output. 
	/// Resets if player leaves trigger, or looks outside the Field of View threshold.
	/// </summary>
	[Property, Order( 3 )] public float LookTime { get; set; }

	/// <summary>
	/// How close the player has to be looking at the target. 
	/// 1.0 = straight ahead, 0.0 = ~90 degrees, -1.0 = all directions.
	/// </summary>
	[Property, Order( 3 ), Range( -1f, 1f )] public float FieldOfView { get; set; }

	/// <summary>
	/// The time, in seconds, to wait after player enters the trigger 
	/// before firing the OnTimeout output if the player doesn't look at the target, 
	/// 0 = never. 
	/// </summary>
	[Property, Order( 3 )] public float Timeout { get; set; }


	[ShowIf( "isDebug", true ), ReadOnly]
	[Feature( "Debug" ), Title( "Player looking at:" ), Property] public GameObject PlayerLookTarget { get; set; }

	public override bool IsTouchable => true;

	public bool ContainsBasePlayer = false;
	private bool _hasBasePlayer = false;
	private bool _hasTimedOut = false;
	private bool _triggerFired = false;

	private float _elapsedLookTime = 0f;
	private float _elapsedTimeInTrigger = 0f;


	/// <summary>
	/// Fired after the timeout interval expires if the player never looked at the target.
	/// </summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnTimeout { get; set; }


	protected override void OnTriggerIn() => CheckPresence();

	protected override void OnTriggerOut() => CheckPresence();

	protected override void OnItemsEmpty()
	{
		base.OnItemsEmpty();

		OnTriggerItemsEmpty?.Invoke( this );
	}

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( FireOnce && _triggerFired )
			return;

		if ( _hasBasePlayer )
		{
			_elapsedTimeInTrigger += Time.Delta;

			if ( IsTimeOut() && !_hasTimedOut )
			{
				_hasTimedOut = true;
				OnTimeout?.Invoke( this );
			}
		}

		if ( !_triggerFired && PlayerSawTarget() )
		{
			_triggerFired = true;
			OnTrigger?.Invoke( BasePlayer.Local );
		}

		// Debug view update
		if ( IsDebug && _hasBasePlayer && BasePlayer.Local?.LifeState == LifeState.Alive )
		{
			IsPlayerLookingAtTarget( LookTarget );
		}
	}

	public bool PlayerSawTarget()
	{
		if ( !LookTarget.IsValid() || !ContainsBasePlayer || _triggerFired )
			return false;

		bool isLooking = IsPlayerLookingAtTarget( LookTarget );

		if ( !isLooking )
		{
			_elapsedLookTime = 0f;
			return false;
		}

		_elapsedLookTime += Time.Delta;
		return _elapsedLookTime >= LookTime;
	}


	private bool IsPlayerLookingAtTarget( GameObject target )
	{
		if ( BasePlayer.Local?.LifeState == LifeState.Dead || !target.IsValid() )
		{
			if ( IsDebug ) PlayerLookTarget = null;
			return false;
		}

		Ray ray = BasePlayer.Local.Controller.AimRay;

		Vector3 forward = ray.Forward.Normal;
		Vector3 toTarget = (target.WorldPosition - ray.Position).Normal;

		const float dotTolerance = 0.05f;
		float dot = Vector3.Dot( forward, toTarget );

		if ( dot + dotTolerance < FieldOfView )
		{
			if ( IsDebug ) PlayerLookTarget = null;
			return false;
		}

		var trace = Scene.Trace.Ray( ray.Position, target.WorldPosition )
			.WithAnyTags( "solid" )
			.UseHitboxes( false )
			.UsePhysicsWorld( true )
			.WithoutTags( "player" )
			.WithoutTags( "trigger" )
			.Run();

		bool hasLineOfSight = !(trace.Hit && trace.GameObject != target);

		if ( IsDebug )
		{
			PlayerLookTarget = hasLineOfSight ? target : null;
		}

		return hasLineOfSight;
	}

	private void CheckPresence()
	{
		lock ( TrackedItems )
		{
			ContainsBasePlayer = TrackedItems.Keys.Any( go => go.GetComponentInParent<BasePlayer>().IsValid() );
		}

		if ( ContainsBasePlayer != _hasBasePlayer )
		{
			_hasBasePlayer = ContainsBasePlayer;
			StopTimeout();
		}
	}

	protected void StopTimeout()
	{
		if ( Timeout <= 0 )
			return;

		_elapsedTimeInTrigger = 0f;
		_elapsedLookTime = 0f;

		if ( !FireOnce )
			_triggerFired = false;

		_hasTimedOut = false;
	}

	private bool IsTimeOut()
	{
		if ( Timeout <= 0 || _elapsedTimeInTrigger < Timeout || _hasTimedOut )
			return false;

		return true;
	}

}
