namespace Core.AI;

public class Blink : AIAbility
{
	public float blinkDuration = 0.5f;
	public float minBlinkInterval = 2.5f;
	public float maxBlinkInterval = 6.0f;

	public string openMorph = "Open";
	public string closeMorph = "Close";

	float _nextBlinkTime;
	float _blinkStartTime;
	bool _isBlinking;
	public bool _controlledByAI = false; // if the open and closed-ness is currently being controlled by AI. houndeyes sleeping is an example (infact the only example right now)

	public Blink( AIController controller ) : base( controller )
	{
		ScheduleNextBlink();
	}

	public override void Tick()
	{
		if ( _controlledByAI ) // skip tick since this is controlled by ai
			return;

		float now = Time.Now;

		if ( !_isBlinking )
		{
			SetMorphs( 1f, 0f );
		}

		if ( _isBlinking )
		{
			UpdateBlink( now );
			return;
		}

		if ( now >= _nextBlinkTime )
		{
			StartBlink( now );
		}
	}

	public override void OnOwnerDamaged( DamageInfo dmg )
	{
		StartBlink( Time.Now );
	}
	public override void OnOwnerTouched()
	{
		StartBlink( Time.Now );
	}
	void StartBlink( float now )
	{
		_isBlinking = true;
		_blinkStartTime = now;
	}

	void UpdateBlink( float now )
	{
		float t = (now - _blinkStartTime) / blinkDuration;

		if ( t >= 1f )
		{
			// eye full open
			SetOpen( 1f );
			_isBlinking = false;
			ScheduleNextBlink();
			return;
		}

		if ( t < 0.5f )
		{
			float k = t / 0.5f;

			SetOpen( 1f - k );
			SetClose( k );
		}
		else
		{
			float k = (t - 0.5f) / 0.5f;

			SetClose( 1f - k );
			SetOpen( k );
		}
	}

	public void SetOpen( float v )
	{
		var morphs = Controller.BodyModel.Morphs;
		morphs.Set( "Open", v );
		morphs.Set( "Close", 0f );
	}

	public void SetClose( float v )
	{
		var morphs = Controller.BodyModel.Morphs;
		morphs.Set( "Close", v );
		morphs.Set( "Open", 0f );
	}

	void ScheduleNextBlink()
	{
		float interval = Game.Random.Float( minBlinkInterval, maxBlinkInterval );
		_nextBlinkTime = Time.Now + interval;
	}

	void SetMorphs( float open, float close )
	{
		var model = Controller?.BodyModel;
		if ( !model.IsValid() )
			return;

		var morphs = model.Morphs;
		morphs.Set( openMorph, open );
		morphs.Set( closeMorph, close );
	}
}
