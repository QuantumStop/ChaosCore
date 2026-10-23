namespace Core;

using System;

/// <summary>An entity that fires a timer event at regular, or random, intervals. It can also be set to oscillate betweena high and low end, in which case it will fire alternating high/low outputs each time it fires.</summary>
[Icon( "timer" )]
public class logic_timer : BaseEntity
{
	public new delegate void ChaosOutput( logic_timer activator );

	[Property] public bool StartDisabled { get; set; }

	/// <summary>Fired when the timer expires.</summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnTimer { get; set; }

	/// <summary>Fired every other time for an oscillating timer.</summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnTimerHigh { get; set; }

	/// <summary>Fired every other time for an oscillating timer.</summary>
	[Property, Group( "Outputs" )] public ChaosOutput OnTimerLow { get; set; }

	[Property] public bool UseRandomTime { get; set; }

	/// <summary>If 'Use Random Time' is set, this is the minimum time between timer fires. The time will be a random number between this and the 'Maximum Random Interval'.</summary>
	[Property, ShowIf( "UseRandomTime", true )] public float MinimumRandomInterval { get; set; }

	/// <summary>If 'Use Random Time' is set, this is the maximum time between timer fires. The time will be a random number between the 'Minimum Random Interval' and this.</summary>	
	[Property, ShowIf( "UseRandomTime", true )] public float MaximumRandomInterval { get; set; }

	/// <summary>If 'Use Random Time' isn't set, this is the time between timer fires, in seconds.</summary>
	[Property] public float RefireInterval { get; set; }

	/// <summary>
	/// Time to wait after the timer is enabled before firing the first time. Negative values can be used to avoid waiting the lower random bound on spawn, or positive values to postpone the first event.
	/// </summary>
	[Property, Title( "Delay before first fire" )] public float InitialDelay { get; set; }

	/// <summary>
	/// Alternates between OnTimerHigh and OnTimerLow outputs
	/// </summary>
	[Group( "SpawnFlags" ), Property, Title( "Oscillator" ), Order( 2 )] public bool IsOscillator { get; set; } = false;
#if IGNIS
	[DebugExpose( "firing in", order: 2, Format = "0.00 sec" )]
#endif
	[Property, Feature( "Debug" ), ReadOnly, ActionGraphIgnore] public float CurrentTime { get; set; }
#if IGNIS
	[DebugExpose( "refire interval", order: 1, Format = "0.00 sec" )]
#endif
	[Property, Feature( "Debug" ), ReadOnly, ActionGraphIgnore] public float CurrentRefireInterval;

	private float _currentMaxInterval;
	private int _oscillatorState = 0;
	private bool _isEnabled;

	protected override void OnStart()
	{
		base.OnStart();

		_isEnabled = !StartDisabled;

		if ( UseRandomTime )
			CalculateInterval();
		else
		{
			CurrentRefireInterval = RefireInterval;
			CurrentTime = CurrentRefireInterval;
		}

		// Apply InitialDelay: add or subtract time from the countdown
		CurrentTime = MathF.Max( 0f, CurrentTime + InitialDelay );
	}

	protected override void OnUpdate()
	{
		if ( !_isEnabled )
			return;

		HandleTimer();
	}

	private void HandleTimer()
	{
		if ( CurrentTime > 0f )
		{
			CurrentTime = MathF.Max( 0f, CurrentTime - Time.Delta );
		}
		else
		{
			if ( IsOscillator )
			{
				bool isEven = ((int)_oscillatorState++) % 2 == 0;
				if ( isEven )
					OnTimerHigh?.Invoke( this );
				else
					OnTimerLow?.Invoke( this );
			}

			Fire();
		}
	}

	public virtual void Fire()
	{
		OnTimer?.Invoke( this );

		if ( UseRandomTime )
			CalculateInterval();
		else
			CurrentTime = CurrentRefireInterval;
	}

	private void CalculateInterval()
	{
		CurrentRefireInterval = Game.Random.Float( MinimumRandomInterval, MaximumRandomInterval );
		CurrentTime = CurrentRefireInterval;
		_currentMaxInterval = CurrentRefireInterval;
	}


	//	============= INPUTS ============= //

	#region Inputs Block

	/// <summary>
	/// Set a new Refire Interval.
	/// </summary>
	public BaseEntity RefireTime( BaseEntity activator = null, float value = 0f )
	{
		CurrentRefireInterval += value;

		return activator ?? null;
	}


	/// <summary>
	/// Reset the timer. It will fire after the Refire Interval expires.
	/// </summary>
	public BaseEntity ResetTimer( BaseEntity activator = null )
	{
		CurrentTime = CurrentRefireInterval;

		return activator ?? null;
	}


	/// <summary>
	/// Force the timer to fire immediately.
	/// </summary>
	public BaseEntity FireTimer( BaseEntity activator = null )
	{
		Fire();

		return activator ?? null;
	}

	/// <summary>
	/// Toggle the timer on/off.
	/// </summary>
	public override BaseEntity Toggle( BaseEntity activator = null )
	{
		_isEnabled ^= true;

		CurrentTime = 0f;

		CurrentRefireInterval = UseRandomTime
		? Game.Random.Float( MinimumRandomInterval, MaximumRandomInterval )
		: RefireInterval;

		if ( !Enabled )
			GameObject.Enabled = true;

		Enabled = !Enabled;

		return activator ?? null;
	}


	/// <summary>
	/// Set a new Minimum Random Interval.
	/// </summary>
	public BaseEntity LowerRandomBound( BaseEntity activator = null, float value = 0f )
	{
		MinimumRandomInterval += value;

		return activator ?? null;
	}


	/// <summary>
	/// Set a new Maximum Random Interval.
	/// </summary>
	public BaseEntity UpperRandomBound( BaseEntity activator = null, float value = 0f )
	{
		MaximumRandomInterval += value;

		return activator ?? null;
	}


	/// <summary>
	/// Add time to the timer if it is currently enabled. Does not change the Refire Interval.
	/// </summary>
	public BaseEntity AddToTimer( BaseEntity activator = null, float value = 0f )
	{
		CurrentTime += value;

		return activator ?? null;
	}


	/// <summary>
	/// Subtract time from the timer if it is currently enabled. Does not change the Refire Interval.
	/// </summary>
	public BaseEntity SubtractFromTimer( BaseEntity activator = null, float value = 0f )
	{
		CurrentTime -= value;

		return activator ?? null;
	}


	/// <summary>
	/// Pauses the timer, maintaining its current remaining time.
	/// </summary>
	public BaseEntity PauseTimer( BaseEntity activator = null, float value = 0f )
	{
		_isEnabled = true;

		return activator ?? null;
	}


	/// <summary>
	/// Unpauses the timer, continuing from where it left off when frozen.
	/// </summary>
	public BaseEntity UnpauseTimer( BaseEntity activator = null, float value = 0f )
	{
		_isEnabled = false;

		return activator ?? null;
	}

	#endregion

}
