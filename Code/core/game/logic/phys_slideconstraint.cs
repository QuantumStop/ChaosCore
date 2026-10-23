using System;
namespace Core;

using SliderJoint = Sandbox.SliderJoint;

/// <summary>A constraint that constrains an entity along a line segment.</summary>
[Icon( "desk" )]
public class phys_slideconstraint : BaseEntity, Component.ExecuteInEditor
{
	protected override string GetEditorVis() => null;

	public static phys_slideconstraint Instance { get; set; }
	[Property, ReadOnly, RequireComponent] public SliderJoint Joint { get; set; }

	[Property, Title( "Parent:" )]
	public GameObject ParentGameObject
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Joint.Body = value;
				OnObjChanged();
			}
		}
	}
	[Property]
	public bool ShowCreatedComponents
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				ApplyVisibilityFlags();
			}
		}
	}
	private List<Component> _proceduralComponents { get; set; }

	public enum SlideDirection
	{
		Forward = 0,
		Reverse = 1
	}

	public enum SlideAxis
	{
		/// <summary>Slide the object on the X axis</summary>
		X,
		/// <summary>Slide the object on the Y axis</summary>
		Y,
		/// <summary>Slide the object on the Z axis</summary>
		Z
	}

	/// <summary>
	/// This will dictate in what direction within a selected axis the object should slide.
	/// Example: Selecting Y axis and picking reverse option, will make it slide on -Y axis relative to parented object.
	/// </summary>

	[Header( "Joint Options" )]
	[Property]
	public SlideDirection SlideDir
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Dirty();
			}
		}
	}

	/// <summary>
	/// This will dictate on what axis our object should slide, relative to parent.
	/// Influences local rotation of the joint on start of the scene to do so.
	/// </summary>
	[Property]
	public SlideAxis slideAxis
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Dirty();
			}
		}
	}

	[Property]
	public bool EnableCollision
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Joint.EnableCollision = value;
			}
		}
	}
	[Property]
	public float MinLength
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Joint.MinLength = value;
			}
		}
	}
	[Property]
	public float MaxLength
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Joint.MaxLength = value;
			}
		}
	}
	[Property]
	public float Friction
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				Joint.Friction = value;
			}
		}
	}



	[Group( "Breaking" ), Property] public bool StartBroken { get; set; }
	[Group( "Breaking" ), Property] public float BreakForce { get; set; }
	[Group( "Breaking" ), Property] public float BreakTorque { get; set; }
	[Group( "Breaking" ), Property] public Action OnBreak { get; set; }


	[Feature( "Debug" )]
	[Property, ReadOnly] public float Speed;
	private Vector3 _startLocation;


	// TODO: Add to debug in fixed update

	// [Group("Breaking")] [Property] public float LinearStress  { get; set; }
	// [Group("Breaking")] [Property] public float AngularStress { get; set; }

	protected override void OnEnabled() => UpdateComponents();

	protected override void OnStart()
	{
		_startLocation = Joint.GameObject.WorldPosition;
		UpdateSlideDirection( slideAxis );
	}

	public void UpdateSlideDirection( SlideAxis SlideDir )
	{
		switch ( SlideDir )
		{
			case SlideAxis.X:
				if ( this.SlideDir == SlideDirection.Forward ) Joint.GameObject.LocalRotation = new Angles( 0, 0, 0 );
				else Joint.GameObject.LocalRotation = new Angles( 00, 00, 00 );
				break;
			case SlideAxis.Y:
				if ( this.SlideDir == SlideDirection.Forward ) Joint.GameObject.LocalRotation = new Angles( -90, 00, 00 );
				else Joint.GameObject.LocalRotation = new Angles( 90, 00, 00 );
				break;
			case SlideAxis.Z:
				if ( this.SlideDir == SlideDirection.Forward ) Joint.GameObject.LocalRotation = new Angles( 0, 0, 0 );
				else Joint.GameObject.LocalRotation = new Angles( 00, 00, 00 );
				break;
		}
	}

	private void Dirty()
	{
		Joint.EnableCollision = EnableCollision;
		Joint.MaxLength = MaxLength;
		Joint.MinLength = MinLength;
		Joint.Friction = Friction;

		Joint.StartBroken = StartBroken;
		Joint.BreakForce = BreakForce;
		Joint.BreakTorque = BreakTorque;
		Joint.OnBreak = OnBreak;

		OnObjChanged();
	}

	protected override void OnFixedUpdate()
	{
		// TODO: (distance / time) * vector
		//var endPosition   = _joint.



		//	var distance = 
		//	var time     =


		//	_joint?.GetSpeed();

	}

	public void AddProcedural( Component p )
	{
		_proceduralComponents ??= [];

		p.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;

		if ( !_proceduralComponents.Contains( p ) ) { _proceduralComponents.Add( p ); }
	}

	void OnObjChanged()
	{
		if ( !ParentGameObject.IsValid() ) return;

		if ( Active ) UpdateComponents();
	}

	void UpdateComponents()
	{
		if ( !Joint.IsValid() ) return;

		CreatePhysComponent();
		ApplyVisibilityFlags();
	}

	void CreatePhysComponent()
	{
		SliderJoint slider;

		slider = Components.GetOrCreate<SliderJoint>();
		slider.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;

		AddProcedural( slider );
	}

	void ApplyVisibilityFlags()
	{
		if ( _proceduralComponents is null )
			return;

		foreach ( var c in _proceduralComponents )
		{
			if ( ShowCreatedComponents )
			{
				c.Flags = ComponentFlags.NotSaved;
			}
			else
			{
				c.Flags = ComponentFlags.Hidden | ComponentFlags.NotSaved;
			}
		}
	}


}
