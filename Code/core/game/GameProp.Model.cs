using Sandbox;
using Sandbox.Diagnostics;
using Sandbox.ModelEditor.Nodes;
using System;

namespace Core;

public partial class GameProp
{
	private Model _model;

	[DebugExpose( DisplayMember = "ResourcePath" )]
	[Property, Order( 1 )]
	public Model Model
	{
		get => _model;
		set
		{
			if ( _model == value ) return;

			_model = value;

			if ( !base.GameObject.Flags.Contains( GameObjectFlags.Deserializing ) )
			{
				_bodyGroups = ulong.MaxValue;
				_materialGroup = null;
			}

			OnModelChanged();
		}
	}

	private void OnModelChanged()
	{
		if ( Model != null )
		{
			if ( Model.TryGetData<ModelPropData>( out var data ) )
			{
				if ( !OverrideHealth )
					Health = ((data.Health > 0f) ? data.Health : Health);
			}

			if ( Active )
			{
				ClearProcedurals();
				UpdateComponents();
			}
		}
		else
		{
			ModelRenderer.Model = Model.Load( GetEditorVis() ); // this clears the model back to the gizmo when you clear the property
		}
	}

	[DebugExpose]
	private ModelRenderer ModelRenderer { get; set; }
	[Property, ReadOnly, Feature( "Debug" ), Title( "Procedural Components:" ), Order( 50 ), Space]
	List<Component> ProceduralComponents { get; set; }
	public void ClearProcedurals()
	{
		{
			if ( ProceduralComponents != null )
			{
				foreach ( var p in ProceduralComponents )
				{
					if ( p != null )
					{
						try
						{
							p.Destroy();
						}
						catch ( Exception e )
						{
							Log.Warning( $"[GameProp] Failed to destroy procedural component: {e.Message}" );
						}
					}
				}

				ProceduralComponents.Clear();
			}

			// Check before nulling out ModelRenderer — it might still be in use by the scene
			if ( ModelRenderer != null )
			{
				try
				{
					ModelRenderer.Destroy();
				}
				catch ( Exception e )
				{
					Log.Warning( $"[GameProp] Failed to destroy ModelRenderer: {e.Message}" );
				}


				ModelRenderer = null;
			}
		}
	}

	public void AddProcedural( Component p )
	{
		Assert.AreNotEqual( p, this );

		ProceduralComponents ??= new();

		p.Flags |= procFlags;

		if ( !ProceduralComponents.Contains( p ) ) { ProceduralComponents?.Add( p ); }
	}

	private void UpdateComponents()
	{
		if ( Model.IsValid() )
		{
			bool skinned = Model?.BoneCount > 0;

			CreateModelComponent( skinned );
			CreatePhysicsComponent();
			CreateTintComponent();

			ApplyVisibilityFlags();
		}
	}

	void CreateModelComponent( bool skinned )
	{
		ModelRenderer = skinned ? Components.GetOrCreate<SkinnedModelRenderer>() : Components.GetOrCreate<ModelRenderer>();

		ModelRenderer.Model = Model;
		ModelRenderer.BodyGroups = BodyGroups;
		ModelRenderer.MaterialGroup = MaterialGroup;
		ModelRenderer.RenderType = shadowRenderType;

		AddProcedural( ModelRenderer );
	}
}
