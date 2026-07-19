using Core.AI;
using System;
using System.Collections.Generic;
using System.Text;

namespace Core.AI;

public class AICorpse : AIModule
{

	public SkinnedModelRenderer BodyModel;
	public ModelPhysics Ragdoll;
	public ScentEmitter scentEmitter;

	public bool shouldBleed;
	public Color bloodColor;

	public float bleedInterval;
	public float bleedDuration;

	public float lifetime = 0;
	public float ScentSpreadRate = 0.1f; // rate for which a scent grows
	public float MaxScentRadius = 1024f; // scent cloud size at its largest

	public override void Init( AIController owner )
	{

		base.Init( owner );
	}

	public override void Tick()
	{
		if ( scentEmitter.Radius < MaxScentRadius )
			scentEmitter.Radius += ScentSpreadRate;
		if ( AIManager.AIDebugCorpses )
		{
			Log.Info( $"scent radius: {scentEmitter.Radius}" );
			Gizmo.Draw.LineSphere( scentEmitter.Position, scentEmitter.Radius );
		}

		base.Tick();
	}

	public void CreateCorpse( SkinnedModelRenderer model, ModelPhysics ragdoll )
	{
		BodyModel = model;
		Ragdoll = ragdoll;
		scentEmitter = new ScentEmitter();
		scentEmitter.Radius = 64; //  grows over time
		scentEmitter.Intensity = 1;
		//scentEmitter.SourceEnt = // i need a fucking modelphysics wrapper

	}

}
