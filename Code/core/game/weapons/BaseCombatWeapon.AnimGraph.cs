namespace Core;

public partial class BaseCombatWeapon
{
	public enum AnimGraphAction
	{
		action_idle,
		action_deploy,
		action_attack,
		action_reload,
		action_holster,
		action_lower
	}

	public enum AnimGraphWeaponAttackType
	{
		attack_primary,
		attack_secondary,
		attack_special
	}

	public enum AnimGraphWeaponAttackState
	{
		attack_normal,
		attack_last_round,
		attack_dry
	}

	public enum AnimGraphLocomoteType
	{
		locomote_locked,
		locomote_grounded,
		locomote_airborne,
		locomote_swimming
	}

	public enum AnimGraphWeaponReloadStage
	{
		reload_stage_ready,
		reload_stage_1,
		reload_stage_2,
		reload_stage_3,
		reload_stage_4,
		reload_stage_5
	}

	public enum AnimGraphGroundedType
	{
		grounded_null,
		grounded_walk,
		grounded_stroll,
		grounded_run
	}

	public enum AnimGraphAirborneType
	{
		airborne_null,
		airborne_jump,
		airborne_fall,
		airborne_levitate
	}

	public enum AnimGraphSwimType
	{
		swim_null,
		swim_forward,
		swim_side,
		swim_updown,
		swim_drown
	}

	private AnimGraphAction _viewmodelAnimGraphAction = AnimGraphAction.action_idle;
	private AnimGraphWeaponAttackType _viewmodelAnimGraphWeaponAttackType = AnimGraphWeaponAttackType.attack_primary;
	private AnimGraphWeaponAttackState _viewmodelAnimGraphWeaponAttackState = AnimGraphWeaponAttackState.attack_normal;
	private AnimGraphLocomoteType _viewmodelAnimGraphLocomoteType = AnimGraphLocomoteType.locomote_locked;
	private AnimGraphWeaponReloadStage _viewmodelAnimGraphWeaponReloadStage = AnimGraphWeaponReloadStage.reload_stage_ready;
	private AnimGraphGroundedType _viewmodelAnimGraphGroundedType = AnimGraphGroundedType.grounded_null;
	private AnimGraphAirborneType _viewmodelAnimGraphAirborneType = AnimGraphAirborneType.airborne_null;
	private AnimGraphSwimType _viewmodelAnimGraphSwimType = AnimGraphSwimType.swim_null;
	private float _viewmodelAnimGraphActionSpeedScale = 1f;

	private bool _viewmodelAnimGraphActionReset;
	private bool _viewmodelAnimGraphActionResetClearPending;
	private bool _viewmodelAnimGraphAttackClearPending;
	private bool _viewmodelAnimGraphActionLocked;
	private bool _viewmodelAnimGraphAttackVariation;
	private float _viewmodelAnimGraphWishRun;

	protected virtual float ViewmodelAnimGraphWishRunMoveSpeed => 4f;

	[Property, ReadOnly, Feature( "Debug" )]
	protected AnimGraphAction ViewmodelAnimGraphAction
	{
		get => _viewmodelAnimGraphAction;
		set
		{
			if ( _viewmodelAnimGraphAction == value ) return;

			_viewmodelAnimGraphAction = value;

			SetAnimGraph( "action", value );

			OnViewmodelAnimGraphActionChanged( value );
		}
	}

	[Property, ReadOnly, Feature( "Debug" )]
	protected AnimGraphWeaponAttackType ViewmodelAnimGraphWeaponAttackType
	{
		get => _viewmodelAnimGraphWeaponAttackType;
		set => SetSyncedAnimGraphValue( ref _viewmodelAnimGraphWeaponAttackType, value, "weapon_attack_type" );
	}

	[Property, ReadOnly, Feature( "Debug" )]
	protected AnimGraphLocomoteType ViewmodelAnimGraphLocomoteType
	{
		get => _viewmodelAnimGraphLocomoteType;
		set => SetSyncedAnimGraphValue( ref _viewmodelAnimGraphLocomoteType, value, "locomote_type" );
	}

	[Property, ReadOnly, Feature( "Debug" )]
	protected AnimGraphWeaponReloadStage ViewmodelAnimGraphWeaponReloadStage
	{
		get => _viewmodelAnimGraphWeaponReloadStage;
		set => SetSyncedAnimGraphValue( ref _viewmodelAnimGraphWeaponReloadStage, value, "weapon_reload_stage" );
	}

	[Property, ReadOnly, Feature( "Debug" )]
	protected AnimGraphGroundedType ViewmodelAnimGraphGroundedType
	{
		get => _viewmodelAnimGraphGroundedType;
		set => SetSyncedAnimGraphValue( ref _viewmodelAnimGraphGroundedType, value, "grounded_type" );
	}

	[Property, ReadOnly, Feature( "Debug" )]
	protected AnimGraphAirborneType ViewmodelAnimGraphAirborneType
	{
		get => _viewmodelAnimGraphAirborneType;
		set => SetSyncedAnimGraphValue( ref _viewmodelAnimGraphAirborneType, value, "airborne_type" );
	}

	[Property, ReadOnly, Feature( "Debug" )]
	protected AnimGraphSwimType ViewmodelAnimGraphSwimType
	{
		get => _viewmodelAnimGraphSwimType;
		set => SetSyncedAnimGraphValue( ref _viewmodelAnimGraphSwimType, value, "swim_type" );
	}

	[Property, ReadOnly, Feature( "Debug" )]
	protected float ViewmodelAnimGraphActionSpeedScale
	{
		get => _viewmodelAnimGraphActionSpeedScale;
		set => SetSyncedAnimGraphValue( ref _viewmodelAnimGraphActionSpeedScale, value, "action_speedscale" );
	}
	protected void SyncViewmodelAnimGraphState()
	{
		SetAnimGraph( "action", ViewmodelAnimGraphAction );
		SetAnimGraph( "weapon_attack_type", ViewmodelAnimGraphWeaponAttackType );
		SetAnimGraph( "weapon_attack_state", _viewmodelAnimGraphWeaponAttackState );
		SetAnimGraph( "locomote_type", ViewmodelAnimGraphLocomoteType );
		SetAnimGraph( "weapon_reload_stage", ViewmodelAnimGraphWeaponReloadStage );
		SetAnimGraph( "grounded_type", ViewmodelAnimGraphGroundedType );
		SetAnimGraph( "airborne_type", ViewmodelAnimGraphAirborneType );
		SetAnimGraph( "swim_type", ViewmodelAnimGraphSwimType );
		SetAnimGraph( "action_speedscale", ViewmodelAnimGraphActionSpeedScale );
		SetAnimGraph( "action_reset", _viewmodelAnimGraphActionReset );
		SetAnimGraph( "action_locked", _viewmodelAnimGraphActionLocked );
		SetAnimGraph( "weapon_attack_variation", GetViewmodelAnimGraphAttackVariation() );
		SyncViewmodelAnimGraphAmmo();
	}

	protected void StartViewmodelAnimGraphAttack( AnimGraphWeaponAttackType attackType )
	{
		_viewmodelAnimGraphWeaponAttackState = PrimaryAmmoLoaded switch
		{
			<= 0 => AnimGraphWeaponAttackState.attack_dry,
			1 => AnimGraphWeaponAttackState.attack_last_round,
			_ => AnimGraphWeaponAttackState.attack_normal
		};
		_viewmodelAnimGraphAttackVariation = !_viewmodelAnimGraphAttackVariation;

		ViewmodelAnimGraphWeaponAttackType = attackType;
		SetAnimGraph( "weapon_attack_variation", GetViewmodelAnimGraphAttackVariation() );
		SetAnimGraph( "weapon_attack_state", _viewmodelAnimGraphWeaponAttackState );

		if ( ViewmodelAnimGraphAction == AnimGraphAction.action_attack )
			PulseViewmodelAnimGraphActionReset();
		else
			ViewmodelAnimGraphAction = AnimGraphAction.action_attack;
		_viewmodelAnimGraphAttackClearPending = true;
	}

	protected void TickViewmodelAnimGraphActionState()
	{
		ClearViewmodelAnimGraphActionReset();
		ClearViewmodelAnimGraphAttackAction();
	}

	protected void FinishViewmodelAnimGraphAttack()
	{
		if ( ViewmodelAnimGraphAction == AnimGraphAction.action_attack )
			ViewmodelAnimGraphAction = AnimGraphAction.action_idle;

		SyncViewmodelAnimGraphAmmo();
	}

	private void OnViewmodelAnimGraphActionChanged( AnimGraphAction current )
	{
		SetViewmodelAnimGraphActionLocked( current != AnimGraphAction.action_idle && current != AnimGraphAction.action_attack );

		if ( current == AnimGraphAction.action_attack )
			PulseViewmodelAnimGraphActionReset();
	}

	private void PulseViewmodelAnimGraphActionReset()
	{
		_viewmodelAnimGraphActionReset = true;
		_viewmodelAnimGraphActionResetClearPending = true;
		SetAnimGraph( "action_reset", true );
	}

	private void ClearViewmodelAnimGraphActionReset()
	{
		if ( !_viewmodelAnimGraphActionResetClearPending )
			return;

		_viewmodelAnimGraphActionReset = false;
		_viewmodelAnimGraphActionResetClearPending = false;
		SetAnimGraph( "action_reset", false );
	}

	private void ClearViewmodelAnimGraphAttackAction()
	{
		if ( !_viewmodelAnimGraphAttackClearPending )
			return;

		// Clear the request. The graph finishes the animation.
		_viewmodelAnimGraphAttackClearPending = false;

		if ( ViewmodelAnimGraphAction == AnimGraphAction.action_attack )
			ViewmodelAnimGraphAction = AnimGraphAction.action_idle;
	}

	protected void SetViewmodelAnimGraphActionLocked( bool locked )
	{
		if ( _viewmodelAnimGraphActionLocked == locked )
			return;

		_viewmodelAnimGraphActionLocked = locked;
		SetAnimGraph( "action_locked", locked );
	}

	public void SyncViewmodelAnimGraphPlayerState( BasePlayer player )
	{
		if ( !player.IsValid() || !player.Controller.IsValid() )
			return;

		var controller = player.Controller;
		var moveSpeed = player.Movement.Velocity.WithZ( 0 ).Length;
		var wishSpeed = player.Movement.WishVelocity.WithZ( 0 ).Length;
		var wishMove = wishSpeed.Remap( 0f, controller.RunSpeed, 0f, 1f, true );
		var wishRun = controller.IsRunning ? 1f : 0f;
		var wishWalk = controller.IsRunning ? 0f : wishMove;

		_viewmodelAnimGraphWishRun = MathX.Approach(
			_viewmodelAnimGraphWishRun,
			wishRun,
			WorldTime.Delta * ViewmodelAnimGraphWishRunMoveSpeed
		);

		ViewmodelAnimGraphLocomoteType = GetViewmodelAnimGraphLocomoteType( player );
		ViewmodelAnimGraphGroundedType = GetViewmodelAnimGraphGroundedType( controller as PlayerController, moveSpeed );
		ViewmodelAnimGraphAirborneType = controller.Controller.IsOnGround ? AnimGraphAirborneType.airborne_null : AnimGraphAirborneType.airborne_fall;
		ViewmodelAnimGraphSwimType = player.IsUnderwater ? AnimGraphSwimType.swim_forward : AnimGraphSwimType.swim_null;

		SetAnimGraph( "view_pose", player.ViewmodelVisible );
		SetAnimGraph( "grounded_wish_walk", wishWalk );
		SetAnimGraph( "grounded_wish_run", _viewmodelAnimGraphWishRun );
	}

	protected void SyncViewmodelAnimGraphAmmo()
	{
		SetAnimGraph( "weapon_ammo_loaded", PrimaryAmmoLoaded );
		SetAnimGraph( "weapon_ammo_max", GetViewmodelAnimGraphAmmoMax() );
		SetAnimGraph( "weapon_ammo_reserve", GetViewmodelAnimGraphAmmoReserve() );
	}

	private static AnimGraphLocomoteType GetViewmodelAnimGraphLocomoteType( BasePlayer player )
	{
		if ( player.IsUnderwater )
			return AnimGraphLocomoteType.locomote_swimming;

		if ( !player.Controller.Controller.IsOnGround )
			return AnimGraphLocomoteType.locomote_airborne;

		return AnimGraphLocomoteType.locomote_grounded;
	}

	private static AnimGraphGroundedType GetViewmodelAnimGraphGroundedType( PlayerController controller, float moveSpeed )
	{
		if ( !controller.Controller.IsOnGround || moveSpeed <= 1f )
			return AnimGraphGroundedType.grounded_null;

		return controller.IsRunning ? AnimGraphGroundedType.grounded_run : AnimGraphGroundedType.grounded_walk;
	}

	private float GetViewmodelAnimGraphAttackVariation() => _viewmodelAnimGraphAttackVariation ? 1f : 0f;

	private int GetViewmodelAnimGraphAmmoMax() => GetPrimaryCapacity() + (PrimaryAmmoInChamber > 0 ? 1 : 0);

	private int GetViewmodelAnimGraphAmmoReserve()
	{
		if ( BasePlayer.InfiniteAmmo )
			return GetPrimaryCapacity() + 5;

		if ( !Owner.Player.IsValid() || !WeaponData.PrimaryAmmoType.IsValid() )
			return 0;

		return Owner.Player.GetReserveAmmo( GetPrimaryAmmoType() );
	}

	private void SetSyncedAnimGraphValue<T>( ref T current, T value, string parameterName ) where T : struct, System.Enum
	{
		if ( current.Equals( value ) )
			return;

		current = value;

		SetAnimGraph( parameterName, value );
	}

	private void SetSyncedAnimGraphValue( ref float current, float value, string parameterName )
	{
		if ( current == value )
			return;

		current = value;
		SetAnimGraph( parameterName, value );
	}

	private void SetAnimGraph<T>( string parameterName, T value ) where T : struct, System.Enum =>
		Owner.Player?.SetAllAnimgraphParams( parameterName, value );

	private void SetAnimGraph( string parameterName, bool value ) =>
		Owner.Player?.SetAllAnimgraphParams( parameterName, value );

	private void SetAnimGraph( string parameterName, float value ) =>
		Owner.Player?.SetAllAnimgraphParams( parameterName, value );
		
	/// <summary>
	/// Returns the equipped viewmodel's animation state.
	/// </summary>
	public DebugTextBlock GetDebugText()
	{
		if ( !_debugAnimEvents || !Owner.Player.IsValid() || Owner.Player.CurrentWeapon != this )
			return null;

		var viewmodel = Owner.Player.ViewmodelWeapon;
		if ( !viewmodel.IsValid() )
			return null;

		var block = new DebugTextBlock();
		block.Header( "VIEWMODEL ANIMGRAPH DEBUG" );
		block.Line( "Weapon", GetType().Name, Color.White );
		block.Line( "Graph", viewmodel.Animation.Graph is { } graph ? System.IO.Path.GetFileName( graph.ResourcePath ) : "None", Color.White );

		var parameters = viewmodel.Animation.Parameters;
		if ( !parameters.TryGetString( "action", out var action ) )
		{
			block.Line( "Status", "No action parameter", Color.Orange );
			return block;
		}

		block.Line( "Action", action, Color.Green );
		block.Line( "Reset / Locked", $"{Bool( "action_reset" )} / {Bool( "action_locked" )}", Color.Green );
		block.Line( "Speed scale", Float( "action_speedscale" ), Color.Green );
		block.Line( "Attack type", Id( "weapon_attack_type" ), Color.Cyan );
		block.Line( "Attack state", Id( "weapon_attack_state" ), Color.Cyan );
		block.Line( "Attack variation", Float( "weapon_attack_variation" ), Color.Cyan );
		block.Line( "Ammo / Graph", $"{PrimaryAmmoLoaded} / {Float( "weapon_ammo_loaded" )}", Color.Orange );
		block.Line( "Ammo max / Reserve", $"{Float( "weapon_ammo_max" )} / {Float( "weapon_ammo_reserve" )}", Color.Orange );
		block.Line( "Reload stage", Id( "weapon_reload_stage" ), Color.Orange );
		block.Line( "Ready / Holstered", $"{ReadyToFire} / {IsHolstered}", Color.White );
		block.Line( "Holster owner", $"{Owner.Player.HolsterOwner}", Color.White );
		block.Line( "Pending weapon", Owner.Player.WeaponToEquip.IsValid() ? Owner.Player.WeaponToEquip.GetType().Name : "None", Color.White );
		block.Line( "Locomotion", Id( "locomote_type" ), Color.Cyan );
		block.Line( "Grounded", Id( "grounded_type" ), Color.Cyan );
		block.Line( "Wish walk / Run", $"{Float( "grounded_wish_walk" )} / {Float( "grounded_wish_run" )}", Color.Cyan );
		return block;

		string Id( string name ) => parameters.TryGetString( name, out var value ) ? value : "-";
		string Bool( string name ) => parameters.TryGetBool( name, out var value ) ? $"{value}" : "-";
		string Float( string name ) => parameters.TryGetFloat( name, out var value ) ? $"{value:0.##}" : "-";
	}

}

