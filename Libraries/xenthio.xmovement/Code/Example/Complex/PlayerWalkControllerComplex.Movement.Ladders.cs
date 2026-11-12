using Sandbox;
using System;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	[Property, FeatureEnabled( "Ladders" )] public bool EnableLadders { get; set; } = true;
	[Property, Feature( "Ladders" )] public float ClimbSpeed { get; set; } = 100.0f;
	[Property, Feature( "Ladders" )] public string LadderTag { get; set; } = "ladder";
	protected bool _isTouchingLadder = false;
	protected Vector3 _ladderNormal;

	protected Vector3 _lastNonZeroWishLadderVelocity;
	public virtual void CheckLadder()
	{
		if ( !EnableLadders ) { _isTouchingLadder = false; return; }
		var wishvel = new Vector3( WishMove.x.Clamp( -1f, 1f ), WishMove.y.Clamp( -1f, 1f ), 0 );

		// this is for latching
		if ( wishvel.Length > 0 ) _lastNonZeroWishLadderVelocity = wishvel;
		if ( _tryLatchNextTickCounter > 0 ) wishvel = _lastNonZeroWishLadderVelocity * -1;

		wishvel *= EyeAngles.WithPitch( 0 ).ToRotation();
		wishvel = wishvel.Normal;

		if ( _isTouchingLadder )
		{
			if ( Input.Pressed( "jump" ) )
			{
				var sidem = (Math.Abs( Head.WorldRotation.Forward.Abs().z - 1 ) * 3).Clamp( 0, 1 );
				var upm = Head.WorldRotation.Forward.z;

				var Eject = new Vector3
				{
					x = _ladderNormal.x * sidem,
					y = _ladderNormal.y * sidem,
					z = (3 * upm).Clamp( 0, 1 )
				};

				Controller.Velocity += (Eject * 180.0f) * WorldScale;

				_isTouchingLadder = false;

				return;

			}
			else if ( Controller.GroundObject.IsValid() && _ladderNormal.Dot( wishvel ) > 0 )
			{
				_isTouchingLadder = false;

				return;
			}
		}

		const float ladderDistance = 1.0f;
		var start = WorldPosition;
		Vector3 end = start + (_isTouchingLadder ? (_ladderNormal * -1.0f) : wishvel) * ladderDistance;

		var pm = Scene.Trace.Ray( start, end )
					.Size( Controller.BoundingBox.Mins, Controller.BoundingBox.Maxs )
					.WithTag( LadderTag )
					.IgnoreGameObjectHierarchy( GameObject )
					.Run();

		// Gizmo.Draw.LineBBox( cc.BoundingBox.Translate( end ) );

		_isTouchingLadder = false;

		if ( pm.Hit )
		{
			_isTouchingLadder = true;
			_ladderNormal = pm.Normal;
		}
	}

	public virtual void LadderMove()
	{
		Controller.IsOnGround = false;

		var velocity = Controller.WishVelocity;
		float normalDot = velocity.Dot( _ladderNormal );
		var cross = _ladderNormal * normalDot;
		Controller.Velocity = (velocity - cross) + (-normalDot * _ladderNormal.Cross( Vector3.Up.Cross( _ladderNormal ).Normal ));

		Controller.Move( withWishVelocity: false, withGravity: false );
	}

	[ConVar( "debug_movement_ladderlatching" )]
	public static bool LatchDebug { get; set; } = false;

	protected int _tryLatchNextTickCounter = 0;
	protected Vector3 _lastNonZeroWishVelocity;
	protected void LadderLatchCheck()
	{
		if ( !EnableLadders ) return;

		if ( Input.Down( JumpAction ) || Input.Released( JumpAction ) )
		{
			_tryLatchNextTickCounter = 0;
			return;
		}

		if ( !Controller.WishVelocity.Normal.IsNearlyZero( 0.001f ) )
		{
			_lastNonZeroWishVelocity = Controller.WishVelocity;
		}

		if ( _tryLatchNextTickCounter > 0 )
		{
			Controller.Velocity = (_lastNonZeroWishVelocity.Normal * -100).WithZ( Controller.Velocity.z );
			_tryLatchNextTickCounter--;
		}

		if ( Controller.GroundObject.IsValid() ) return;
		if ( Controller.PreviousGroundObject == null ) return;

		// Trace downwards and behind the way we are or was walking

		var start = WorldPosition + (Vector3.Down * 16);
		var end = start - (_lastNonZeroWishVelocity.Normal * (Controller.Radius / 2));

		var pm = Scene.Trace.Ray( start, end )
					.Size( Controller.BoundingBox.Mins, Controller.BoundingBox.Maxs )
					.WithTag( LadderTag )
					.IgnoreGameObjectHierarchy( GameObject )
					.Run();

		if ( pm.Hit )
		{
			Controller.Velocity = Vector3.Zero.WithZ( Controller.Velocity.z );
			_tryLatchNextTickCounter = 10;
		}

		if ( LatchDebug )
		{
			DebugOverlay.Line( WorldPosition, start, Color.Yellow, 10 );
			DebugOverlay.Line( pm.StartPosition, pm.EndPosition, Color.Red, 10 );
		}
	}
}
