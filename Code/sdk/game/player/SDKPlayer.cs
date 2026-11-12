namespace SDK;

using Core;
using Core.AI;

[Title( "SDK Example Player" )]
[Category( "SDK" )]
public partial class Player : BasePlayer
{
	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		// This should only work in first person mode, as it will break aiming otherwise!
		if ( Halo2Crosshair && Controller.CameraMode == XMovement.PlayerWalkControllerComplex.CameraModes.FirstPerson ) Local.Controller.Camera.GameObject.LocalRotation *= new Angles( -9, 0, 0 );
	}

	[Property, ReadOnly, Feature( "Debug" )] public override bool HasSuit => true;
	private const float WalkFootstepRadius = 220f;
	private const float RunFootstepRadius = 420f;
	
	protected override void OnStart()
	{
		base.OnStart();

		WorldInput ??= Controller.Camera.Components.GetOrCreate<WorldInput>();
		WorldInput?.LeftMouseAction = "use";
	}

	protected override void OnDisabled() => WorldInput?.Enabled = false;

	protected override void OnUpdate()
	{
		if ( LifeState == LifeState.Alive ) AnimatePlayerModel();

		base.OnUpdate();

		CalculateBob();
	}

	public override void PlayFootstepSound( Vector3 worldPosition, float volume, int foot )
	{
		base.PlayFootstepSound( worldPosition, volume, foot );

		if ( AIController.NoTarget || Controller.IsCrouching || !Controller.Controller.IsOnGround
			|| Controller.Controller.Velocity.WithZ( 0 ).Length <= 10f )
			return;

		NpcSoundManager.AddSound( NpcSoundManager.SoundType.SOUND_FOOTSTEP, worldPosition, GameObject,
			Controller.IsRunning ? RunFootstepRadius : WalkFootstepRadius );
	}

	protected override void OnObjectPunted( GrabState state, Vector3 velocity )
	{
		const float radius = 128f;
		var start = state.Body.WorldPosition;
		var direction = velocity.Normal;
		var trace = Scene.Trace.Ray( start, start + velocity * 0.5f )
			.IgnoreGameObjectHierarchy( state.GameObject )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		// Warn along the prop's path, stopping at the first obstruction.
		// Similar to how Source handles it.
		float distance = start.Distance( trace.EndPosition );
		
		for ( float step = radius; step < distance; step += radius )
			NpcSoundManager.AddSound( NpcSoundManager.SoundType.SOUND_PHYSICS_DANGER, 
			start + direction * step, state.GameObject, radius );
	}

	public override void OnDeath( DamageInfo dmginfo )
	{
		base.OnDeath( dmginfo );

		// TODO: Better SDK example how to do this, take more from 2K in regards to structure/handling.

		if ( CurrentWeapon.IsValid() ) DropWeapon( CurrentWeapon, false );

		CurrentWeapon = null;
	}

}
