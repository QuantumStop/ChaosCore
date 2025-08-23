namespace Core;
public class NpcCarcass : Component
{
	public BaseNpc Owner { get; set; }
	[Property, ReadOnly] public bool StenchEnabled { get; set; }
	[Property, ReadOnly] public bool FliesEnabled { get; set; }
	[Property, Hide] public float TimeTillStench { get; set; } = 5f;
	[Property, Hide] public float TimeTillFlies { get; set; } = 45f;

	protected override void OnFixedUpdate()
	{
		if ( !StenchEnabled )
		{
			TimeTillStench -= Time.Delta;
			if ( TimeTillStench < 0 )
				StartStench();
		}
		if ( !FliesEnabled )
		{
			TimeTillFlies -= Time.Delta;
			if ( TimeTillFlies < 0 )
				StartFlies();
		}
		base.OnFixedUpdate();
	}

	public void StartStench()
	{
		Log.Info( "Corpse emitting stench.." );
		//NpcSoundManager.AddSound( NpcSoundManager.SoundType.STENCH_CARCASS, Owner.BodyModel.GetAttachment( Owner.NpcDef.ModelInfo.CorpseCenter ).Value.Position, GameObject );
		StenchEnabled = true;
	}

	public void StartFlies()
	{
		//		TODO: replace with Scene Particles 
		//		var particle = Scene.CreateObject().Components.Create<LegacyParticleSystem>();
		//		particle.Particles = ParticleSystem.Load( "particles/env/fly_swarm_01a.vpcf" );
		//		particle.GameObject.SetParent( GameObject );
		//		particle.GameObject.Name = "carcass_flies";
		//		particle.WorldPosition = Owner.BodyModel.GetAttachment( Owner.NpcDef.ModelInfo.CorpseCenter ).Value.Position;
		FliesEnabled = true;
	}
}
