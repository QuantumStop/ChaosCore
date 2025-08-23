[Hide]
public class BaseItem : BaseUsable
{
	[Property, Group( "Outputs" ), Order( 100 ), Title( "On Pickup" )] public ChaosOutput OnPickupOutput { get; set; }
	[DebugExpose( group: "BaseItem", DisplayMember = "Model.ResourcePath" ), Property, ReadOnly, Feature( "Debug" )] protected ModelRenderer Mesh;
	[DebugExpose( group: "BaseItem", DisplayMember = "Model.ResourcePath" ), Property, ReadOnly, Feature( "Debug" )] protected ModelCollider Collider;
	[Property, ReadOnly, Feature( "Debug" )] protected Rigidbody Physics;
	[Property, Hide] public bool PickUp;
	protected virtual string GetModel() { return "models/weapons/w_glock.vmdl"; }
	/// <summary>
	/// Enable physics and movement for this object?
	/// </summary>
	[DebugExpose( group: "BaseItem" ), Property, Order( 10 )]
	public bool MotionEnabled
	{
		get => _motionEnabled;
		set
		{
			_motionEnabled = value;
			CanBeHeldAccessor = value;
		}
	}

	private bool _motionEnabled = true;
	protected virtual bool IsStatic() { return !MotionEnabled; }
	public virtual bool AllowTouchPickup() { return true; }
	protected override string GetEditorVis() { return GetModel(); }
	protected virtual string GetPickupSound() { return "ammo_pickup"; }
	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( PickUp )
			return;

		Mesh = Components.GetOrCreate<ModelRenderer>();
		Mesh.Model = Model.Load( GetModel() );

		Collider = Components.GetOrCreate<ModelCollider>();
		Collider.Model = Mesh.Model;

		Physics = Components.GetOrCreate<Rigidbody>();
		Physics.Tags.Add( "item" );

		if ( IsStatic() )
			Physics.MotionEnabled = false;
	}
	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( PickUp )
			OnPickup();
	}

	//	public override bool Press( IPressable.Event e ) { base.Press( e ); OnPickup(); return true; }
	/// <summary>
	/// When picked up (succefully)
	/// </summary>
	/// <param name="Activator">The player who picked it up</param>
	public virtual void OnPickup( BasePlayer Activator = null ) { PickUp = false; if ( !PickupCheck() ) return; OnPickupOutput?.Invoke( Activator ); Sound.Play( GetPickupSound() ).ListenLocal = true; }
	/// <summary>
	/// Used to know if OnPickup() should even be fired, per entity
	/// </summary>
	/// <returns></returns>
	protected virtual bool PickupCheck() { return true; }
	/// <summary>
	/// When item is forcefully removed (not when killed or spent)
	/// </summary>
	public virtual void OnRemove() { }
	/// <summary>
	/// Properly destroy the item with extra stuff we want (instead of just calling GameObject.Destroy())
	/// </summary>
	public void DestroyItem()
	{
		// i am not sure why would you do this besides having an item on a gameobject with something else, which never happens or should happen
		/*
			Mesh?.Destroy();
			Collider?.Destroy();
			Physics?.Destroy();
			Destroy();
		*/

		if ( !GameObject.IsValid() )
			return;

		Kill();
	}
}
