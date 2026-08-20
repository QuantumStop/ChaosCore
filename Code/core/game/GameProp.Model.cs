using Sandbox.Diagnostics;
using Sandbox.ModelEditor.Nodes;
using System;

namespace Core;

public partial class GameProp
{
#if IGNIS
	[DebugExpose( DisplayMember = "ResourcePath" )]
#endif
	[Property, Order( 1 )]
	public Model Model
	{
		get;
		set
		{
			if ( field == value ) return;

			field = value;

			if ( !GameObject.Flags.Contains( GameObjectFlags.Deserializing ) )
			{
				BodyGroups = ulong.MaxValue;
				MaterialGroup = null;
			}

			OnModelChanged();
		}
	}

	private void OnModelChanged()
	{
		if ( Model.IsValid() )
		{
			if ( Model.TryGetData<ModelPropData>( out var data ) )
			{
				if ( !OverrideHealth )
					Health = (data.Health > 0f) ? data.Health : Health;
			}

			if ( Active )
			{
				ClearProcedurals();
				UpdateComponents();
			}
		}
		else
		{
			if ( _modelRenderer.IsValid() )
				_modelRenderer.Model = Model.Load( GetEditorVis() ); // this clears the model back to the gizmo when you clear the property
		}
	}
#if IGNIS
	[DebugExpose]
#endif
	private ModelRenderer _modelRenderer { get; set; }
	/*
		[Property, ReadOnly, Feature( "Debug" ), Title( "Procedural Components" ), Order( 50 )]
		private List<Component> _proceduralComponentsDebug { get; set; } = [];
	*/
	List<Component> _proceduralComponents { get; set; }
	private bool _hasRigidbody => Components.Get<Rigidbody>().IsValid();
	private Rigidbody _rigidbody { get; set; }

	[Rpc.Broadcast]
	public void ClearProcedurals()
	{
		if ( _proceduralComponents is null ) return;

		foreach ( var p in _proceduralComponents.ToArray() )
		{
			if ( !p.IsValid() )
				continue;

			try
			{
				p.Destroy();
			}
			catch ( Exception e )
			{
				Log.Warning( $"[GameProp] Failed to destroy procedural component: {e.Message}" );
			}
		}

		_proceduralComponents.Clear();
		_modelRenderer = null;
	}

	public void AddProcedural( Component p )
	{
		Assert.AreNotEqual( p, this );

		_proceduralComponents ??= [];

		p.Flags |= _procFlags;

		if ( !_proceduralComponents.Contains( p ) ) _proceduralComponents?.Add( p );
	}

	[Rpc.Broadcast]
	private void UpdateComponents()
	{
		if ( Model.IsValid() )
		{
			bool skinned = Model.BoneCount > 0;

			CreateModelComponent( skinned );
			CreatePhysicsComponent();
			CreateTintComponent();

			ApplyVisibilityFlags();
		}
	}

	void CreateModelComponent( bool skinned )
	{
		_modelRenderer = skinned ? Components.GetOrCreate<SkinnedModelRenderer>() : Components.GetOrCreate<ModelRenderer>();

		_modelRenderer.Model = Model;
		_modelRenderer.BodyGroups = BodyGroups;
		_modelRenderer.MaterialGroup = MaterialGroup;
		_modelRenderer.RenderType = ShadowRenderType;

		AddProcedural( _modelRenderer );
	}
}
