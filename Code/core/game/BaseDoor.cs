#if FMOD
using FMOD.Studio;
using FMODSbox;
#endif

using System;

namespace Core;

public class BaseDoor : BaseUsable
{
	protected override string GetEditorVis() => Data?.Model?.ResourcePath;
	protected override bool _canBeHeldAccessor => false;
	public override bool CanBeHeld => false;

	public override bool Press( IPressable.Event press )
	{
		if ( !press.Source.Components.TryGet<BasePlayer>( out var basePlayer ) || basePlayer?.LifeState == LifeState.Dead || !CanInteract ) return false;

		// Source of the Use should be a player, as this is specifically a player input press thing rather than general "anyone" (NPC) interaction
		OnUse?.Invoke( basePlayer );

		switch ( State )
		{
			case DoorState.Sleeping:
				Open( basePlayer.GetEyeForward() ); // i dont know why this works
				break;
			case DoorState.Ajar:
				Close( basePlayer.GetEyeForward() );
				break;
		}

		return true;
	}

	[Property] public DoorResource Data { get; set; }
	[Property, Feature( "Debug" ), ReadOnly] public DoorState State { get; protected set; }

	[Property, Feature( "Debug" ), ReadOnly] public SkinnedModelRenderer ModelRenderer { get; protected set; }
	[Property, Feature( "Debug" ), ReadOnly] public ModelCollider Collider { get; protected set; }
	[Property, Feature( "Debug" ), ReadOnly] public Rigidbody Rigidbody { get; protected set; }
	[Property, Feature( "Debug" ), ReadOnly] public HingeJoint Hinge { get; protected set; }
	[Property, Feature( "Debug" ), ReadOnly] public GameObject HingeObj { get; protected set; }
	[Property, Feature( "Debug" ), ReadOnly] public GameObject HandleObj { get; protected set; }

	/// <summary>The angle of the full arc for this door</summary>
	[Property, Range( 120, 270 )]
	public float ArcAngle
	{
		get;
		set
		{
			if ( field == value ) return;
			field = value;

			Hinge?.MaxAngle = value * 0.5f;
			Hinge?.MinAngle = -value * 0.5f;
		}
	} = 180f;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		GameObject.Tags.Add( "door" ); // for collision

		ModelRenderer ??= Components.GetOrCreate<SkinnedModelRenderer>();
		if ( !ModelRenderer.Model.IsValid() ) ModelRenderer?.Model = Data?.Model ?? null;
		ModelRenderer.CreateAttachments = true;

		HandleObj ??= ModelRenderer.GetAttachmentObject( Data?.HandleAttachment ) ?? null;

		Collider ??= Components.GetOrCreate<ModelCollider>();
		Rigidbody ??= Components.GetOrCreate<Rigidbody>();
		Rigidbody.MassOverride = 60;
		Rigidbody.AngularDamping = 8;

		if ( !string.IsNullOrWhiteSpace( Data?.HingeAttachment ) && !HingeObj.IsValid() ) HingeObj = ModelRenderer.GetAttachmentObject( Data?.HingeAttachment );
		Hinge ??= HingeObj?.Components.GetOrCreate<HingeJoint>();
		Hinge?.MaxAngle = ArcAngle * 0.5f;
		Hinge?.MinAngle = -ArcAngle * 0.5f;

		Rigidbody.MotionEnabled = false;
	}

	public bool IsMoving => Hinge.IsValid() && MathF.Abs( Hinge.Speed ) >= 0.5f;
	public override bool CanInteract => State == DoorState.Sleeping || State == DoorState.Ajar;

	protected override void OnFixedUpdate()
	{
		if ( !Hinge.IsValid() ) return;
		switch ( State )
		{
			case DoorState.Sleeping: return;
			case DoorState.Ajar:
				if ( IsMoving && IsClosed ) Latch();
				MoveSound();
				break;
			case DoorState.Opening:
				if ( MathF.Abs( Hinge.Angle ) >= MathF.Abs( Hinge.TargetAngle ) ) Ajar();
				break;
			case DoorState.Latching:
				if ( Hinge.Angle.AlmostEqual( 0, 0.1f ) ) Sleep();
				break;
		}
	}
	/// <summary>
	/// Start "teleporting" door to the closed position, play sounds etc
	/// </summary>
	protected virtual void Latch()
	{
		State = DoorState.Latching;
		FastMove( true );
#if FMOD
		if ( Data.Latch.IsValid() ) FMODSound.Play( Data.Latch, GameObject.GetBounds().Center );
#endif
	}
	/// <summary>This is one of the two only actual player input thats possible, every other action is physics based (walk next to the door to close it, etc)</summary>
	public virtual void Open( Vector3 rot = default )
	{
		if ( State != DoorState.Sleeping ) return;

		State = DoorState.Opening; // separate state so we don't trigger the latching zone
		Hinge?.Enabled = true;
		Rigidbody?.MotionEnabled = true;
		FastMove( true, false, GetOpeningAngle( rot, 4 ) ); // always away from the player
#if FMOD
		if ( Data.HandleOpen.IsValid() ) FMODSound.Play( Data.HandleOpen, GameObject.GetBounds().Center );
		if ( Data.Move.IsValid() )
		{
			_moveSND = FMODSound.Play( Data.Move, HingeObj, false );
			_moveSND.setVolume( 0 ); // dogshit hack sue me
		}
#endif
	}

	/// <summary>The other only possible action, except without a separate state so it can be interrupted</summary>
	public virtual void Close( Vector3 rot = default )
	{
		if ( State != DoorState.Ajar ) return;

		FastMove( true, false, GetClosingAngle() );
#if FMOD
		if ( Data.HandleOpen.IsValid() ) FMODSound.Play( Data.HandleOpen, GameObject.GetBounds().Center );
#endif
	}

	/// <summary>Send the door to sleep state where nothing happens and nothing is checked</summary>
	protected virtual void Sleep()
	{
		State = DoorState.Sleeping;
		Hinge?.Enabled = false;
		Rigidbody?.MotionEnabled = false;

#if FMOD
		FMODSound.Stop( _moveSND );
		FMODSound.Release( _moveSND );
#endif
	}

	protected virtual void Ajar()
	{
		State = DoorState.Ajar;
		FastMove( false );
	}

#if FMOD
	protected EventInstance _moveSND { get; set; }
#endif


	protected void MoveSound()
	{
#if FMOD
		if ( _moveSND.isValid() ) _moveSND.setVolume( MathF.Round( MathX.Remap( MathF.Abs( Hinge.Speed ), 0.2f, 3.0f, 0, 1 ), 4 ) ); // doesnt go higher than 1.5 ish somehow
#endif
	}


	private void FastMove( bool on, bool fast = true, float angle = 0f )
	{
		if ( fast ) // this is really only for teleporting the hinge back to 0 when latching, every other movement uses slower methods
		{
			Hinge?.Motor = on ? HingeJoint.MotorMode.TargetAngle : HingeJoint.MotorMode.Disabled;
			Hinge?.TargetAngle = angle;
			Hinge?.Frequency = 128;
		}
		else
		{
			Hinge?.TargetAngle = angle;
			Rigidbody?.ApplyImpulseAt( HandleObj.WorldPosition, (angle > 0 ? WorldRotation.Forward : WorldRotation.Backward) * Rigidbody.Mass * 0.75f * MathF.Abs( angle ) );

			if ( DebugDoorForce )
			{
				DebugOverlay.Line( HandleObj.WorldPosition, HandleObj.WorldPosition + (angle > 0 ? WorldRotation.Forward : WorldRotation.Backward) * 12, Color.White, 5, default, true );
				DebugOverlay.Box( HandleObj.WorldPosition + (angle > 0 ? WorldRotation.Forward : WorldRotation.Backward) * 12, 2, Color.White, 5, default, true );
			}
		}
	}

	[ConVar( "debug_door_force" )] public static bool DebugDoorForce { get; set; }

	protected float GetOpeningAngle( Vector3 rot, float targetAngle ) => Data.HingeOnRightSide == (rot.Dot( WorldRotation.Forward ) > 0f) ? MathF.Abs( targetAngle ) : -MathF.Abs( targetAngle );

	protected float GetClosingAngle() => Data.HingeOnRightSide == (Hinge.Angle > 0f) ? -ArcAngle * 0.5f : ArcAngle * 0.5f;

	/// <summary>Is this door locked and requires external machinations to be able to be opened or you live in the 1950s where no one locked their doors</summary>
	[Property] public virtual bool IsLocked { get; protected set; } = false;

	/// <summary>Unlock the door, so it can be opened</summary>
	public virtual void Unlock( bool force = false )
	{
		if ( (!IsLocked || !IsClosed) && !force ) return; // Can't unlock if it's not latched or locked to begin with

		IsLocked = false;
		Hinge?.Enabled = true;
	}

	/// <summary>Lock the door, preventing it to be open</summary>
	public virtual void Lock( bool force = false )
	{
		if ( (IsLocked || !IsClosed) && !force ) return; // Can't lock if it's not latched or unlocked to begin with

		IsLocked = true;
		Hinge?.Enabled = false;
	}

	/// <summary>Is the door producing no sounds when interacting</summary>
	[Property] public virtual bool IsSilent { get; set; } = false;

	/// <summary>Is the door currently closed?</summary>
	[Property, Feature( "Debug" ), ReadOnly]
	public bool IsClosed => !Hinge.IsValid() || MathF.Abs( Hinge.Angle ) <= 5f;

	public enum DoorState
	{
		/// <summary>Closed and stationary, nothing is calculated</summary>
		Sleeping,
		/// <summary>Half-open but not moving</summary>
		Ajar,
		/// <summary>Was interacted with, moving to the slightly ajar position (always away from the player)</summary>
		Opening,
		/// <summary>The latch zone was entered, moving to the closed position</summary>
		Latching,
	}

}

[AssetType( Category = "Game", Extension = "door", Flags = AssetTypeFlags.None, IconColor = "#e48f9d", Name = "Door" )]
public class DoorResource : GameResource
{
	[Property] public Model Model { get; set; }
	/// <summary>Attachment where the hinge will be placed, mandatory for the door to function correctly</summary>
	[AttachmentSelector] public string HingeAttachment { get; set; }
	/// <summary>Is the hinge located on the left side or on the right side</summary>
	[Property] public bool HingeOnRightSide { get; set; } = true;

	[AttachmentSelector] public string HandleAttachment { get; set; }

#if FMOD
	/// <summary>Sound when the handle was touched on an openable door</summary>
	[Property] public FMODEventResource HandleOpen { get; set; }
	/// <summary>Sound when the handle was touched on a locked door</summary>
	[Property] public FMODEventResource HandleLocked { get; set; }
	/// <summary>Sound when the door caught the latch and closed</summary>
	[Property] public FMODEventResource Latch { get; set; }
	/// <summary>The movement sound, the squeaks, the whooshes... </summary>
	[Property] public FMODEventResource Move { get; set; }
	/// <summary>Door has stopped moving because it reached the end of the allowed angle</summary>
	[Property] public FMODEventResource StopMove { get; set; }
#endif
	protected override Bitmap CreateAssetTypeIcon( int width, int height ) => CreateSimpleAssetTypeIcon( "door_front", width, height, "#e4909e", "#350d14" );
}
