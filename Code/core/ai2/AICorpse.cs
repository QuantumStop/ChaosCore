namespace Core.AI;

public class AICorpse : AIModule
{

	public SkinnedModelRenderer BodyModel;
	public ModelPhysics Ragdoll;
	public ScentEmitter ScentEmitter;

	public bool ShouldBleed;
	public Color BloodColor;

	public float BleedInterval;
	public float BleedDuration;

	public float Lifetime = 0;
	public float ScentSpreadRate = 0.1f; // rate for which a scent grows
	public float MaxScentRadius = 1024f; // scent cloud size at its largest

	public override void Init( AIController owner )
	{
		base.Init( owner );
	}

	public override void Tick()
	{
		if ( ScentEmitter.Radius < MaxScentRadius )
			ScentEmitter.Radius += ScentSpreadRate;
		if ( AIManager.AIDebugCorpses )
		{
			Log.Info( $"scent radius: {ScentEmitter.Radius}" );
			Gizmo.Draw.LineSphere( ScentEmitter.Position, ScentEmitter.Radius );
		}

		base.Tick();
	}

	public void CreateCorpse( SkinnedModelRenderer model, ModelPhysics ragdoll )
	{
		BodyModel = model;
		Ragdoll = ragdoll;
		ScentEmitter = new ScentEmitter
		{
			Radius = 64, //  grows over time
			Intensity = 1
		};

		//scentEmitter.SourceEnt = // i need a fucking modelphysics wrapper

	}

}
