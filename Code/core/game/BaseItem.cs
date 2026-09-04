#if FMOD
using FMODSbox;
#endif
namespace Core;


[Hide]
public class BaseItem : BaseUsable
{
	[Property, Group( "Outputs" ), Order( 100 ), Title( "On Pickup" )] public ChaosOutput OnPickupOutput { get; set; }
#if IGNIS
	[DebugExpose( group: "BaseItem", DisplayMember = "Model.ResourcePath" )]
#endif
	[Property, ReadOnly, Feature( "Debug" )] public ModelRenderer Mesh { get; protected set; }
#if IGNIS
	[DebugExpose( group: "BaseItem", DisplayMember = "Model.ResourcePath" )]
#endif
	[Property, ReadOnly, Feature( "Debug" )] public ModelCollider Collider { get; protected set; }
	[Property, ReadOnly, Feature( "Debug" )] public Rigidbody Physics { get; protected set; }
	[Property, Hide] public bool PickUp;
	protected virtual string GetModel() => "models/dev/error.vmdl";
	/// <summary>
	/// Enable physics and movement for this object?
	/// </summary>
#if IGNIS
	[DebugExpose( group: "BaseItem" )]
#endif
	[Property, Order( 45 ), Group( "Physics Properties" )]
	public bool MotionEnabled
	{
		get;
		set
		{
			field = value;
			_canBeHeldAccessor = value;
		}
	} = true;

	protected virtual bool IsStatic() => !MotionEnabled;
	public virtual bool AllowTouchPickup() => true;
	protected override string GetEditorVis() => GetModel();
#if FMOD
	protected virtual string GetPickupSound() => "event:/Common/AmmoPickup";
#else
	protected virtual string GetPickupSound() => "ammo_pickup";
	protected virtual int SoundStealChannel() => 0;
#endif

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

		AddEffects();
	}

	/// <summary>
	/// Used to add additional effects so we don't need to use prefabs for it
	/// </summary>
	protected virtual void AddEffects() { }

	/// <summary>Who was the last one to touch it</summary>
	[Property, Feature( "Debug" ), ReadOnly]
	public BasePlayer LastOwner
	{
		get;
		set
		{
			if ( field == value ) return;
			field = value;
			OwnerChanged();
		}
	}

	protected virtual void OwnerChanged() { }

	protected override void OnFixedUpdate()
	{
		base.OnFixedUpdate();

		if ( PickUp && LastOwner.IsValid() )
			OnPickup( LastOwner );
	}

	/// <summary>
	/// When picked up (succefully)
	/// </summary>
	/// <param name="Activator">The player who picked it up</param>
	public virtual void OnPickup( BasePlayer Activator = null )
	{
		PickUp = false;

		if ( !PickupCheck() ) return;

		if ( Activator.IsValid() ) OnPickupOutput?.Invoke( Activator );
#if FMOD
		FMODSound.Play( GetPickupSound() );
#else
		BasePlayer.Local.PlayPickupSteal( GetPickupSound(), SoundStealChannel(), WorldPosition );
#endif
	}
	/// <summary>
	/// Used to know if OnPickup() should even be fired, per entity
	/// </summary>
	/// <returns></returns>
	protected virtual bool PickupCheck() => true;
	/// <summary>
	/// When item is forcefully removed (not when killed or spent)
	/// </summary>
	public virtual void OnRemove( BasePlayer Activator = null ) { }
	/// <summary>
	/// Properly destroy the item with extra stuff we want (instead of just calling GameObject.Destroy())
	/// </summary>
	public void DestroyItem()
	{
		if ( GameObject.IsValid() ) Kill();
	}

	/// <summary>
	/// We started the pickup FixedUpdate, because we cant just trigger it once, it has to be occasionally checked
	/// </summary>
	/// <param name="Activator"></param>
	public void StartPickingUp( BasePlayer Activator )
	{
		LastOwner = Activator;
		PickUp = true;
	}

	public void StopPickingUp( BasePlayer Activator )
	{
		if ( Activator == LastOwner ) PickUp = false;
	}
}
