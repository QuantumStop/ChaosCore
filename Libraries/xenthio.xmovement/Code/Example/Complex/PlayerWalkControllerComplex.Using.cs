using Sandbox;
namespace XMovement;

public partial class PlayerWalkControllerComplex : Component
{
	[Property, FeatureEnabled( "Using" )] public bool EnableUse { get; set; } = false;
	/// <summary>
	/// The Input Action that noclipping is triggered by.
	/// </summary>
	[Property, InputAction, Feature( "Using" )] public string UseAction { get; set; } = "Use";
	[Property, Feature( "Using" )] public float UseDistance { get; set; } = 130;

	public Component Hovering { get; set; }
	public Component Using { get; set; }

	public void DoUsing()
	{
		if ( !EnableUse ) return;
		if ( IsProxy ) return;
		var evnt = new IPressable.Event() { Ray = AimRay, Source = this };
		UpdateHover( FindUsable() );

		if ( Input.Pressed( UseAction ) && CanUse( Hovering ) )
		{
			Using = Hovering;
			if ( Using is IPressable pressable ) pressable.Press( evnt );
		}
		if ( Input.Down( UseAction ) && CanUse( Using ) )
		{
			if ( Using is IPressable pressable ) pressable.Pressing( evnt );
		}
		if ( Using.IsValid() && (Input.Released( UseAction ) || !CanUse( Using )) )
		{
			if ( Using is IPressable pressable ) pressable.Release( evnt );
			Using = null;
		}
	}

	void UpdateHover( Component newhovering )
	{
		var evnt = new IPressable.Event() { Ray = AimRay, Source = this };

		if ( Hovering == newhovering )
		{
			if ( Hovering is IPressable hoveringPressable )
			{
				hoveringPressable.Look( evnt );
			}

			return;
		}

		if ( Hovering is IPressable stoppedHoveringPressable )
		{
			stoppedHoveringPressable.Blur( evnt );
		}

		Hovering = newhovering;

		if ( Hovering is IPressable startedHoveringPressable )
		{
			startedHoveringPressable.Hover( evnt );
			startedHoveringPressable.Look( evnt );
		}
	}

	public bool CanUse( Component component )
	{
		if ( !component.IsValid() ) return false;
		if ( component.WorldPosition.Distance( AimRay.Position ) > UseDistance ) return false;
		return true;
	}

	public Component FindUsable()
	{
		var tr = Scene.Trace.Ray( AimRay, UseDistance )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger" )
			.Run();

		if ( tr.GameObject.IsValid() )
		{
			// if we have multiple IPressable components on one thing, filter out the ones that can't press and do the first match of those who can
			foreach ( var press in tr.GameObject.Components.GetAll<IPressable>( FindMode.EverythingInSelfAndParent ) )
			{
				if ( press is not null && press.CanPress( new IPressable.Event() { Ray = AimRay, Source = this } ) )
					return press as Component;
			}
		}

		return null;
	}
}
