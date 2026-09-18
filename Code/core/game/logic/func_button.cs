namespace Core;

using System;

public class func_button : BaseUsable, Component.IPressable
{
	public enum ButtonMode
	{
		Toggle,
		Continuous,
		Immediate
	}

	protected override string GetEditorVis() => null;
	protected override bool _canBeHeldAccessor => false;
	public override bool CanBeHeld => false;

	/// <summary>
	/// Sound to play when the button is pressed.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent OnSound { get; set; }

	/// <summary>
	/// Sound to play when the button is released.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent OffSound { get; set; }

	/// <summary>
	/// Called when the button is pressed. Receives the GameObject that pressed it.
	/// </summary>
	[Property, Group( "Events" ), ActionGraphIgnore, Doo.ArgumentHint<GameObject>( "user", Help = "The person using the button." )]
	public Doo OnPressed { get; set; }

	/// <summary>
	/// Called when the button is released. Receives the GameObject that released it.
	/// </summary>
	[Property, Group( "Events" ), ActionGraphIgnore, Doo.ArgumentHint<GameObject>( "user", Help = "The person has stopped using the button." )]
	public Doo OnReleased { get; set; }

	/// <summary>
	/// Called when the button turns on. Receives the GameObject that activated it.
	/// </summary>
	[Property, Group( "Events" ), ActionGraphIgnore, Doo.ArgumentHint<GameObject>( "user", Help = "The person who activated the button." )]
	public Doo OnTurnedOn { get; set; }

	/// <summary>
	/// Called when the button turns off.
	/// </summary>
	[Property, Group( "Events" ), ActionGraphIgnore, Doo.ArgumentHint<GameObject>( "user", Help = "The person who deactivated the button." )]
	public Doo OnTurnedOff { get; set; }

	[Property] public ButtonMode Mode { get; set; } = ButtonMode.Toggle;
	[Property, ShowIf( nameof( Mode ), ButtonMode.Toggle )] public bool AutoReset { get; set; } = true;
	[Property, ShowIf( nameof( AutoReset ), true )] public float ResetTime { get; set; } = 1.0f;

	[Property, Group( "Movement" ), Order( 0 )] public bool Move { get; set; }
	[Property, Group( "Movement" ), ShowIf( nameof( Move ), true )] public GameObject MoveTarget { get; set; }
	[Property, Group( "Movement" ), ShowIf( nameof( Move ), true )] public Vector3 MoveDelta { get; set; }

	/// <summary>
	/// Animation curve to use, X is the time between 0-1 and Y is how much the button is pressed from 0-1.
	/// </summary>
	[Property, Group( "Movement" ), ShowIf( nameof( Move ), true )] public Curve AnimationCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 1f, 1.0f ) );

	/// <summary>
	/// How long in seconds should it take to animate this button.
	/// </summary>
	[Property, Group( "Movement" ), ShowIf( nameof( Move ), true )] public float AnimationTime { get; set; } = 0.5f;

	[Property, Group( "Outputs" ), Order( 101 ), Title( "On Pressed" )] public ChaosOutput OnPressedAction { get; set; }
	[Property, Group( "Outputs" ), Order( 102 ), Title( "On Released" )] public ChaosOutput OnReleasedAction { get; set; }
	[Property, Group( "Outputs" ), Order( 103 ), Title( "On Turned On" )] public ChaosOutput OnTurnedOnAction { get; set; }
	[Property, Group( "Outputs" ), Order( 104 ), Title( "On Turned Off" )] public ChaosOutput OnTurnedOffAction { get; set; }

	[Hide, ActionGraphIgnore] public new ChaosOutput OnUse { get; set; }
	[Hide, ActionGraphIgnore] public new ChaosOutput OnHoldStart { get; set; }
	[Hide, ActionGraphIgnore] public new ChaosOutput OnHoldFixedUpdate { get; set; }
	[Hide, ActionGraphIgnore] public new ChaosOutput OnHoldUpdate { get; set; }
	[Hide, ActionGraphIgnore] public new ChaosOutput OnDropped { get; set; }

	[Property, Feature( "Tooltip" )]
	public string TooltipTitle { get; set; } = "Press";

	[Property, Feature( "Tooltip" ), IconName]
	public string TooltipIcon { get; set; } = "touch_app";

	[Property, Feature( "Tooltip" )]
	public string TooltipDescription { get; set; } = "";

	[Header( "Off State" )]
	[ShowIf( nameof( Mode ), ButtonMode.Toggle )]
	[Property, Feature( "Tooltip" )]
	public string TooltipTitleOff { get; set; } = "Press";

	[ShowIf( nameof( Mode ), ButtonMode.Toggle )]
	[Property, Feature( "Tooltip" ), IconName]
	public string TooltipIconOff { get; set; } = "touch_app";

	[ShowIf( nameof( Mode ), ButtonMode.Toggle )]
	[Property, Feature( "Tooltip" )]
	public string TooltipDescriptionOff { get; set; } = "";

	[Sync] private TimeSince LastUse { get; set; }
	[Sync] private bool _isOn { get; set; }

	private Transform _startTransform;
	private Vector3 _initialPosition;
	private bool _isBeingPressed;
	private bool _shouldTurnOffNextFrame;
	private GameObject _lastPresser;
	private BasePlayer _lastActivator;

	/// <summary>
	/// True if the button is currently on.
	/// </summary>
	public bool IsOn
	{
		get => _isOn;
		private set
		{
			if ( _isOn == value )
				return;

			_isOn = value;
			OnButtonStateChanged( value );
		}
	}

	/// <summary>
	/// True if the button is currently animating.
	/// </summary>
	[Property, Feature( "Debug" ), ReadOnly] public bool IsAnimating { get; private set; }

	protected override void OnStart()
	{
		base.OnStart();

		var moveTarget = MoveTarget.IsValid() ? MoveTarget : GameObject;
		_startTransform = moveTarget.LocalTransform;
		_initialPosition = _startTransform.Position;
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( !Gizmo.IsSelected || !Move )
			return;

		Gizmo.Transform = WorldTransform;

		var bbox = GameObject.GetLocalBounds();
		bbox += MoveDelta * MathF.Sin( RealTime.Now * 2.0f ).Remap( -1, 1 );

		Gizmo.Draw.Color = Color.Yellow;
		Gizmo.Draw.LineThickness = 3;
		Gizmo.Draw.LineBBox( bbox );
		Gizmo.Draw.IgnoreDepth = true;

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Draw.Color.WithAlpha( 0.3f );
		Gizmo.Draw.LineBBox( bbox );
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		return CanInteract && !IsAnimating;
	}

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		if ( Mode == ButtonMode.Toggle && IsOn )
		{
			if ( string.IsNullOrWhiteSpace( TooltipTitleOff ) && string.IsNullOrWhiteSpace( TooltipIconOff ) )
				return default;

			return new IPressable.Tooltip( TooltipTitleOff, TooltipIconOff, TooltipDescriptionOff );
		}

		if ( string.IsNullOrWhiteSpace( TooltipTitle ) && string.IsNullOrWhiteSpace( TooltipIcon ) )
			return default;

		return new IPressable.Tooltip( TooltipTitle, TooltipIcon, TooltipDescription );
	}

	public override bool Press( IPressable.Event press )
	{
		if ( !TryGetActivator( press, out _ ) )
			return false;

		Press( press.Source.GameObject );

		return true;
	}

	public override bool Pressing( IPressable.Event press )
	{
		if ( !TryGetActivator( press, out _ ) )
			return false;

		_isBeingPressed = true;
		return Mode == ButtonMode.Continuous;
	}

	public void Release( IPressable.Event press )
	{
		TryGetActivator( press, out _ );

		_isBeingPressed = false;
		Release( press.Source.GameObject );
	}

	/// <summary>
	/// Turns the button on. Does nothing if already on or animating.
	/// </summary>
	[Rpc.Host]
	public void TurnOn( GameObject presser = null )
	{
		if ( IsOn || IsAnimating )
			return;

		_lastPresser = presser;
		_lastActivator = TryGetActivator( presser, out var activator ) ? activator : null;
		LastUse = 0;
		IsAnimating = true;
		IsOn = true;

		if ( OnSound is not null )
			PlaySound( OnSound );

		if ( Mode == ButtonMode.Immediate )
			_shouldTurnOffNextFrame = true;
	}

	/// <summary>
	/// Turns the button off. Does nothing if already off or animating.
	/// </summary>
	[Rpc.Host]
	public void TurnOff( GameObject presser = null )
	{
		if ( !IsOn || IsAnimating )
			return;

		_lastPresser = presser;
		_lastActivator = TryGetActivator( presser, out var activator ) ? activator : null;
		LastUse = 0;
		IsAnimating = true;
		IsOn = false;

		if ( OffSound is not null )
			PlaySound( OffSound );
	}

	/// <summary>
	/// Toggles the button between on and off states.
	/// </summary>
	[Rpc.Host]
	public void Toggle( GameObject presser = null )
	{
		if ( !IsOn )
			TurnOn( presser );
		else
			TurnOff( presser );
	}

	[Rpc.Host]
	private void Press( GameObject presser )
	{
		if ( IsAnimating )
			return;

		_lastPresser = presser;
		TryGetActivator( presser, out var activator );
		_lastActivator = activator;

		RunDoo( OnPressed, c => c.SetArgument( "user", _lastPresser ) );

		// Source of the Use should be a player, as this is specifically a player input press thing rather than general "anyone" (NPC) interaction
		OnUse?.Invoke( activator );
		OnPressedAction?.Invoke( activator );

		switch ( Mode )
		{
			case ButtonMode.Toggle:
				if ( IsOn && !AutoReset )
					TurnOff( presser );
				else
					TurnOn( presser );
				break;
			case ButtonMode.Continuous:
			case ButtonMode.Immediate:
				TurnOn( presser );
				break;
		}
	}

	[Rpc.Host]
	private void Release( GameObject presser )
	{
		_lastPresser = presser;
		TryGetActivator( presser, out var activator );
		_lastActivator = activator;

		RunDoo( OnReleased, c => c.SetArgument( "user", _lastPresser ) );
		OnReleasedAction?.Invoke( activator );

		if ( Mode == ButtonMode.Continuous && IsOn )
			TurnOff( presser );
	}

	[Rpc.Broadcast]
	private void PlaySound( SoundEvent sound )
	{
		GameObject.PlaySound( sound );
	}

	protected override void OnFixedUpdate()
	{
		if ( Mode == ButtonMode.Immediate && _shouldTurnOffNextFrame && IsOn && !IsAnimating )
		{
			_shouldTurnOffNextFrame = false;
			TurnOff( _lastPresser );
			return;
		}

		if ( Mode == ButtonMode.Toggle && !IsAnimating && IsOn && AutoReset && ResetTime >= 0.0f && LastUse >= ResetTime )
		{
			TurnOff( _lastPresser );
			return;
		}

		if ( Mode == ButtonMode.Continuous && IsOn && !IsAnimating && !_isBeingPressed )
		{
			TurnOff( _lastPresser );
			return;
		}

		if ( Mode == ButtonMode.Continuous )
			_isBeingPressed = false;

		if ( !IsAnimating )
			return;

		var time = LastUse.Relative.Remap( 0.0f, AnimationTime, 0.0f, 1.0f );
		var curve = AnimationCurve.Evaluate( time );

		if ( !IsOn )
			curve = 1.0f - curve;

		if ( Move )
		{
			var target = MoveTarget.IsValid() ? MoveTarget : GameObject;
			var targetPosition = _initialPosition + target.LocalTransform.Rotation * (MoveDelta * curve);
			target.LocalTransform = _startTransform.WithPosition( targetPosition );
		}

		if ( time < 1f )
			return;

		IsAnimating = false;
	}

	private void OnButtonStateChanged( bool isOn )
	{
		if ( isOn )
		{
			RunDoo( OnTurnedOn, c => c.SetArgument( "user", _lastPresser ) );
			OnTurnedOnAction?.Invoke( _lastActivator );
		}
		else
		{
			RunDoo( OnTurnedOff, c => c.SetArgument( "user", _lastPresser ) );
			OnTurnedOffAction?.Invoke( _lastActivator );
		}
	}

	private static bool TryGetActivator( IPressable.Event press, out BasePlayer activator )
	{
		activator = null;

		if ( !press.Source.Components.TryGet<BasePlayer>( out activator ) || activator?.LifeState == LifeState.Dead )
			return false;

		return true;
	}

	private static bool TryGetActivator( GameObject source, out BasePlayer activator )
	{
		activator = null;

		if ( !source.IsValid() )
			return false;

		if ( !source.Components.TryGet<BasePlayer>( out activator ) || activator?.LifeState == LifeState.Dead )
			return false;

		return true;
	}
}
