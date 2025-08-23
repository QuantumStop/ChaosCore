namespace Core;

// DONT put this into "editor" subfolder anywhere it thinks this is an editor project

[Category( "Rendering" )]
[Icon( "light_mode" )]
public sealed class TintComponent : Component
{
	/// <summary> A basic single Tint A, secretly a model's vertex color. </summary>
	[Property, Feature( "Tint A" ), MakeDirty] public Color TintA { get; set; } = new Color( 1f, 1f, 1f, 1f );
	/// <summary> A second Tint B. </summary>	
	[Property, Feature( "Tint B" ), MakeDirty] public Color TintB { get; set; } = new Color( 1f, 1f, 1f, 1f );
	/// <summary> A third Tint C. </summary>		
	[Property, Feature( "Tint C" ), MakeDirty] public Color TintC { get; set; } = new Color( 1f, 1f, 1f, 1f );

	[Property, Feature( "Debug" ), ReadOnly] public ModelRenderer mdlrender { get; set; }

	public bool IsOnProp { get; set; }
	[Property, Feature( "Debug" ), ReadOnly] private GameProp Prop { get; set; }

	protected override void OnDirty()
	{
		base.OnDirty();

		if ( IsOnProp )
		{
			Prop = Components.Get<GameProp>();

			if ( !Prop.IsValid() )
				return;
		}

		mdlrender = Components.Get<ModelRenderer>();

		if ( !mdlrender.IsValid() )
			return;

		// John: When an asset first put into a scene can cause an nre
		// As it doesn't immediately get the sceneobject.
		if ( mdlrender.SceneObject == null )
			return;


		if ( IsOnProp && Prop != null )
		{
			if ( Prop.WhichTints == GameProp.PropTintCount.OneTint )
			{
				Prop.PropBatchableDebug = true;
				SetBatchable( true );
			}
			else
			{
				Prop.PropBatchableDebug = false;
				SetBatchable( false );
			}
		}
		else
		{
			SetBatchable( false );
		}

		mdlrender.Tint = TintA;

		mdlrender?.SceneObject.Attributes.Set( "BTintColor", TintB );
		mdlrender?.SceneObject.Attributes.Set( "CTintColor", TintC );
	}

	private void SetBatchable( bool value )
	{
		// Ensure both mdlrender and its SceneObject are valid before assigning
		if ( mdlrender?.SceneObject != null )
			mdlrender.SceneObject.Batchable = value;
		else
			Log.Warning( $"[SetBatchable] Skipped setting Batchable — mdlrender or SceneObject was null." );
	}

}


