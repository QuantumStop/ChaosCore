namespace Core.AI;

using Sandbox;
using System;
/// <summary>
/// This is unfinished. do not use it
/// </summary>
[Obsolete( "ScatterAction is unfinished and broken!" )]
public class PanicScatterAction : MoveToAction
{
	private float panicDuration = 4.0f;
	private float panicStartTime;

	private float repathInterval = 0.4f;
	private float repathTimer;

	private float scatterRadius = 600f;

	public PanicScatterAction( AIController owner )
	{
		Owner = owner;

		Preconditions.Add( new WorldFact( "squadLeaderAlive", false ) );

		Effects.Add( new WorldFact( "isPanicking", true ) );

		Cost = 0.2f;
	}

	public override void OnEnter( AIController owner )
	{

		panicStartTime = Time.Now;
		PickNewScatterPoint();
	}

	public override void Perform( AIController owner )
	{
		repathTimer += Time.Delta;
		if ( repathTimer >= repathInterval )
		{
			repathTimer = 0f;
			PickNewScatterPoint();
		}
	}

	public override bool IsDone()
	{
		if ( !hasStarted )
			return false;

		// Panic expires
		if ( Time.Now - panicStartTime >= panicDuration )
			return true;

		// if a new squad leader is chosen, we chill
		if ( Owner.WorldState.Get( "squadLeaderAlive" ) )
			return true;

		return false;
	}

	public override bool CheckProceduralPrecondition( AIController agent )
	{
		return true;
	}

	private void PickNewScatterPoint()
	{
		Vector3 origin = Owner.WorldPosition;

		Vector3 randomDir = Vector3.Random.Normal;
		Vector3 candidate = origin + randomDir * Random.Shared.Float( 200f, scatterRadius );

		targetPosition = candidate;
	}
}
