using System;
namespace Core;

using SliderJoint = Sandbox.SliderJoint;

[Description( "A constraint that constrains an entity along a line segment." )]
[Icon( "desk" )]
public class phys_slideconstraint : BaseEntity, Component.ExecuteInEditor
{
	public static phys_slideconstraint Instance { get; set; }
	[Property, ReadOnly, RequireComponent] public SliderJoint _joint { get; set; }

	[Property, Title( "Parent:" )]
	public GameObject ParentGameObject
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				_joint.Body = value;
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
	List<Component> ProceduralComponents { get; set; }

	public enum SlideDirection
	{
		Forward = 0,
		Reverse = 1
	}

	public enum SlideAxis
	{
		[Description( "Slide the object on the X axis" )]
		X,
		[Description( "Slide the object on the Y axis" )]
		Y,
		[Description( "Slide the object on the Z axis" )]
		Z
	}

	/// <summary>
	/// This will dictate in what direction within a selected axis the object should slide.
	/// Example: Selecting Y axis and picking reverse option, will make it slide on -Y axis relative to parented object.
	/// </summary>

	[Header( "Joint Options" )]
	[Property]
	public SlideDirection slideDirection
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
				_joint.EnableCollision = value;
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
				_joint.MinLength = value;
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
				_joint.MaxLength = value;
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
				_joint.Friction = value;
			}
		}
	}



	[Group( "Breaking" ), Property] public bool StartBroken { get; set; }
	[Group( "Breaking" ), Property] public float BreakForce { get; set; }
	[Group( "Breaking" ), Property] public float BreakTorque { get; set; }
	[Group( "Breaking" ), Property] public Action OnBreak { get; set; }


	[Feature( "Debug" )]
	[Property, ReadOnly] public float Speed;
	private Vector3 startLocation;


	// TODO: Add to debug in fixed update

	// [Group("Breaking")] [Property] public float LinearStress  { get; set; }
	// [Group("Breaking")] [Property] public float AngularStress { get; set; }

	protected override void DrawGizmos()
	{
		var editorvis = this.GetEditorVis();
		editorvis = null;
	}

	protected override void OnEnabled()
	{
		UpdateComponents();
	}

	protected override void OnStart()
	{
		startLocation = _joint.GameObject.WorldPosition;
		UpdateSlideDirection( slideAxis );
	}

	public void UpdateSlideDirection( SlideAxis SlideDir )
	{
		switch ( SlideDir )
		{
			case SlideAxis.X:
				if ( slideDirection == SlideDirection.Forward ) _joint.GameObject.LocalRotation = new Angles( 0, 0, 0 );
				else _joint.GameObject.LocalRotation = new Angles( 00, 00, 00 );
				break;
			case SlideAxis.Y:
				if ( slideDirection == SlideDirection.Forward ) _joint.GameObject.LocalRotation = new Angles( -90, 00, 00 );
				else _joint.GameObject.LocalRotation = new Angles( 90, 00, 00 );
				break;
			case SlideAxis.Z:
				if ( slideDirection == SlideDirection.Forward ) _joint.GameObject.LocalRotation = new Angles( 0, 0, 0 );
				else _joint.GameObject.LocalRotation = new Angles( 00, 00, 00 );
				break;
		}
	}

	private void Dirty()
	{
		_joint.EnableCollision = EnableCollision;
		_joint.MaxLength = MaxLength;
		_joint.MinLength = MinLength;
		_joint.Friction = Friction;

		_joint.StartBroken = StartBroken;
		_joint.BreakForce = BreakForce;
		_joint.BreakTorque = BreakTorque;
		_joint.OnBreak = OnBreak;

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
		ProceduralComponents ??= [];

		p.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;

		if ( !ProceduralComponents.Contains( p ) ) { ProceduralComponents.Add( p ); }
	}

	void OnObjChanged()
	{
		if ( !ParentGameObject.IsValid() ) return;

		if ( Active ) UpdateComponents();
	}

	void UpdateComponents()
	{
		if ( !_joint.IsValid() ) return;

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
		if ( ProceduralComponents is null )
			return;

		foreach ( var c in ProceduralComponents )
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
