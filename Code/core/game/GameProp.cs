using Sandbox.ModelEditor.Nodes;
using System;

namespace Core;

/// <summary>
/// The new and cool prop component which you should use instead of the old one
/// </summary>
[EditorHandle( "" )]
[Title( "Game Prop" )]
[Category( "Game" )]
[Icon( "inventory" )]
public partial class GameProp : BaseUsable, Component.ExecuteInEditor, Component.IDamageable
{
	protected override string GetEditorVis()
	{
#if IGNIS
		if ( !Model.IsValid() ) return "models/editor/axis_helper2.vmdl";
#else
		if ( !Model.IsValid() ) return "models/editor/axis_helper_thick.vmdl";
#endif
		else return null;
	}

	readonly ComponentFlags _procFlags = ComponentFlags.NotSaved | ComponentFlags.NotCloned | ComponentFlags.Hidden;

	public override bool CanInteract => !BasePlayer.DebugNoMass && _rigidbody?.Mass < 35;

	/// <summary>
	/// Adds the component flags to all procedural components
	/// </summary>
	public void ApplyVisibilityFlags()
	{
		if ( _proceduralComponents is null )
			return;

		foreach ( var c in _proceduralComponents )
		{
			c.Flags = _procFlags;
		}
	}

	/// <summary>
	/// Make procedural components visible and editable in the editor.
	/// </summary>
	[Property, Title( "Show Components" ), Order( 20 ), Feature( "Debug" )]
	public bool ShowProceduralComponents
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				UpdateProceduralVisibility();
			}
		}
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();
		ClearProcedurals();
		UpdateComponents();
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		ClearProcedurals();
	}

	private void UpdateProceduralVisibility()
	{
		if ( _proceduralComponents is null ) return;

		foreach ( var comp in _proceduralComponents )
		{
			if ( !comp.IsValid() ) continue;

			if ( ShowProceduralComponents )
				comp.Flags &= ~ComponentFlags.Hidden;
			else
				comp.Flags |= ComponentFlags.Hidden;
		}
	}

	/// <summary>
	/// Create the gibs for this prop breaking, over the network. This causes clients to spawn the gibs too.
	/// </summary>
	[Rpc.Broadcast( NetFlags.OwnerOnly )]
	public void NetworkCreateGibs( bool wasImpact = false, Vector3 damageOrigin = default, float scatterForceScale = 1f )
	=> CreateGibs( wasImpact, damageOrigin, scatterForceScale );

	public List<GameGib> CreateGibs( bool wasImpact = false, Vector3 damageOrigin = default, float scatterForceScale = 1f )
	{
		var gibs = new List<GameGib>();

		if ( Model is null )
			return gibs;

		var spawnServerGibs = !Network.IsProxy;
		var spawnClientGibs = !Application.IsDedicatedServer;

		var breaklist = Model.GetData<ModelBreakPiece[]>();
		if ( breaklist is null || breaklist.Length <= 0 )
			return gibs;

		var rb = Components.Get<Rigidbody>();
		var mr = Components.Get<ModelRenderer>();

		gibs.EnsureCapacity( breaklist.Length );

		// Batch anything we're spawning here
		using ( Scene.BatchGroup() )
		{
			foreach ( var breakModel in breaklist )
			{
				var model = Model.Load( breakModel.Model );
				if ( model is null || model.IsError )
					continue;

				// Skip gibs we shouldn't spawn
				if ( !GameManagerSystem.Rules.IsSinglePlayer && !spawnServerGibs && !breakModel.IsClientOnly ) continue;
				if ( GameManagerSystem.Rules.IsSinglePlayer && !spawnClientGibs && breakModel.IsClientOnly ) continue;

				var gib = new GameObject( false, $"{GameObject.Name} (gib)" );

				var offset = breakModel.Offset;
				var placementOrigin = model.Attachments.GetTransform( "placementOrigin" );
				if ( placementOrigin.HasValue )
					offset = placementOrigin.Value.PointToLocal( offset );

				gib.WorldPosition = WorldTransform.PointToWorld( offset );
				gib.WorldRotation = WorldRotation;
				gib.WorldScale = WorldScale;

				foreach ( var tag in breakModel.CollisionTags.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
				{
					gib.Tags.Add( tag );
				}

				var c = gib.Components.Create<GameGib>( false );
				c.FadeTime = breakModel.FadeTime;
				c.Model = model;
				c.Enabled = true;
				c.Tint = mr?.Tint ?? c.Tint;
				c.MaterialGroup = mr?.MaterialGroup ?? c.MaterialGroup;

				gibs.Add( c );

				if ( GameManagerSystem.Rules.IsSinglePlayer && breakModel.IsClientOnly )
				{
					gib.Tags.Add( "debris", "clientside" ); // no physics interactions
				}
				else if ( !IsProxy )
				{
					// Spawn on the network
					gib.NetworkSpawn( true, null );
				}

				gib.Parent = DebrisManager.Instance.GameObject;
				gib.Enabled = true;
			}
		}

		// Transfer velocity from us to the gibs.
		if ( rb.IsValid() )
		{
			// If the prop was thrown on the floor or a wall when broken, we want the gibs to inherit the velocity from before that impact
			// that way they crash into the floor/wall nicely and stuff.
			// HOWEVER, we don't want this for anything else
			// else we'd be stomping whatever changes people might be wanting to make to the velocity themselves.
#if IGNIS
			var linVel = wasImpact ? rb.PreVelocity : rb.Velocity;
			var angVel = wasImpact ? rb.PreAngularVelocity : rb.AngularVelocity;
#else
			var linVel = rb.Velocity;
			var angVel = rb.AngularVelocity;
#endif
			foreach ( var gib in gibs )
			{
				var phys = gib.Components.Get<Rigidbody>( true );
				if ( !phys.IsValid() ) continue;

				// Compute linear velocity at the gibs spawn point.
				var velocity = linVel + Vector3.Cross( angVel, phys.MassCenter - rb.MassCenter );

				if ( wasImpact )
				{
					// Apply 50% energy loss from surface impact.
					velocity *= 0.5f;
				}

				phys.Velocity = velocity;
				phys.AngularVelocity = angVel;
			}
		}

		if ( damageOrigin != default && !IsProxy && scatterForceScale > 0 )
		{
			const float BaseScatterForce = 500f;
			const float ScatterRadius = 512f;
			const float TorqueScale = 0.1f;

			foreach ( var gib in gibs )
			{
				var phys = gib.Components.Get<Rigidbody>( true );
				if ( !phys.IsValid() ) continue;
				if ( !phys.PhysicsBody.IsValid() ) continue;

				var toGib = gib.WorldPosition - damageOrigin;
				var dist = toGib.Length;
				if ( dist < 1f )
				{
					toGib = Vector3.Random.Normal;
					dist = 1f;
				}

				var falloff = MathX.Clamp( 1f - dist / ScatterRadius, 0f, 1f );
				var impulse = toGib.Normal * BaseScatterForce * scatterForceScale * falloff * phys.PhysicsBody.Mass;

				phys.ApplyImpulse( impulse );
				phys.ApplyTorque( Vector3.Random * impulse.Length * TorqueScale );
			}
		}

		// If this prop was on fire, ignite the gibs so the fire carries over.
		if ( IsOnFire )
		{
			foreach ( var gib in gibs )
			{
				gib.Ignite();
			}
		}

		return gibs;
	}

}
