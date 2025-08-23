using System;
namespace Core;
using Microsoft.VisualBasic;
using SliderJoint = Sandbox.SliderJoint;

[Description( "A constraint that constrains an entity along a line segment." )]
[Icon( "desk" )]
public class phys_slideconstraint : BaseEntity, Component.ExecuteInEditor
{
	public static phys_slideconstraint StaticRef { get; set; }
	[Property, ReadOnly, RequireComponent] public SliderJoint _joint { get; set; }
	[Property, MakeDirty, Title( "Parent:" )] public GameObject ParentGameObject { get; set; }
	[Property, MakeDirty] public bool ShowCreatedComponents { get; set; }
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
	[Property, MakeDirty] public SlideDirection _slideDirection { get; set; }

	/// <summary>
	/// This will dictate on what axis our object should slide, relative to parent.
	/// Influences local rotation of the joint on start of the scene to do so.
	/// </summary>
	[Property, MakeDirty] public SlideAxis _slideAxis { get; set; }

	[Property, MakeDirty] public bool EnableCollission { get; set; }
	[Property, MakeDirty] public float MinLength { get; set; }
	[Property, MakeDirty] public float MaxLength { get; set; }
	[Property, MakeDirty] public float Friction { get; set; }



	[Group( "Breaking" )][Property, MakeDirty,] public bool StartBroken { get; set; }
	[Group( "Breaking" )][Property, MakeDirty] public float BreakForce { get; set; }
	[Group( "Breaking" )][Property, MakeDirty] public float BreakTorque { get; set; }
	[Group( "Breaking" )][Property, MakeDirty] public Action OnBreak { get; set; }


	[Feature( "Debug" )]
	[Property, ReadOnly] public float Speed;
	private Vector3 startLocation;


	// TODO: Add to debug in fixed update

	// [Group("Breaking")] [Property, MakeDirty] public float LinearStress  { get; set; }
	// [Group("Breaking")] [Property, MakeDirty] public float AngularStress { get; set; }

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
		UpdateSlideDirection( _slideAxis );
	}

	public void UpdateSlideDirection( SlideAxis SlideDir )
	{
		switch ( SlideDir )
		{
			case SlideAxis.X:
				if ( _slideDirection == SlideDirection.Forward ) _joint.GameObject.LocalRotation = new Angles( 0, 0, 0 );
				else _joint.GameObject.LocalRotation = new Angles( 00, 00, 00 );
				break;
			case SlideAxis.Y:
				if ( _slideDirection == SlideDirection.Forward ) _joint.GameObject.LocalRotation = new Angles( -90, 00, 00 );
				else _joint.GameObject.LocalRotation = new Angles( 90, 00, 00 );
				break;
			case SlideAxis.Z:
				if ( _slideDirection == SlideDirection.Forward ) _joint.GameObject.LocalRotation = new Angles( 0, 0, 0 );
				else _joint.GameObject.LocalRotation = new Angles( 00, 00, 00 );
				break;
		}
	}

	protected override void OnDirty()
	{
		base.OnDirty();

		_joint.EnableCollision = EnableCollission;
		_joint.Body = ParentGameObject;
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
		ProceduralComponents ??= new();

		p.Flags |= ComponentFlags.Hidden | ComponentFlags.NotSaved;

		if ( !ProceduralComponents.Contains( p ) ) { ProceduralComponents.Add( p ); }
	}

	void OnObjChanged()
	{
		if ( ParentGameObject is null ) return;

		if ( Active )
		{
			UpdateComponents();
		}
	}

	void UpdateComponents()
	{
		if ( _joint is null ) return;

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
