namespace Core;

// DONT put this into "editor" subfolder anywhere it thinks this is an editor project

[Category( "Rendering" )]
[Icon( "light_mode" )]
public sealed class TintComponent : Component, Component.ExecuteInEditor
{
	/// <summary> A basic single Tint A, secretly a model's vertex color. </summary>
	[Property, Feature( "Tint A" )]
	public Color TintA
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
	} = new Color( 1f, 1f, 1f, 1f );

	/// <summary> A second Tint B. </summary>	
	[Property, Feature( "Tint B" )]
	public Color TintB
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
	} = new Color( 1f, 1f, 1f, 1f );

	/// <summary> A third Tint C. </summary>		
	[Property, Feature( "Tint C" )]
	public Color TintC
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
	} = new Color( 1f, 1f, 1f, 1f );

	[Property, Feature( "Debug" ), ReadOnly] public ModelRenderer ModelRenderer { get; set; }

	public bool IsOnProp { get; set; }
	[Property, Feature( "Debug" ), ReadOnly] private GameProp _prop { get; set; }

	protected override void OnEnabled() => Dirty();

	private void Dirty()
	{
		if ( IsOnProp )
		{
			_prop ??= Components.Get<GameProp>();

			if ( !_prop.IsValid() )
				return;
		}

		ModelRenderer ??= Components.Get<ModelRenderer>();

		if ( !ModelRenderer.IsValid() )
			return;

		// John: When an asset first put into a scene can cause an nre
		// As it doesn't immediately get the sceneobject.
		if ( !ModelRenderer.SceneObject.IsValid() )
			return;

		if ( IsOnProp && _prop.IsValid() )
		{
			if ( _prop.WhichTints == GameProp.PropTintCount.OneTint )
			{
				_prop.PropBatchableDebug = true;
				SetBatchable( true );
			}
			else
			{
				_prop.PropBatchableDebug = false;
				SetBatchable( false );
			}
		}
		else
		{
			SetBatchable( false );
		}

		ModelRenderer.Tint = TintA;

		ModelRenderer?.SceneObject.Attributes.Set( "BTintColor", TintB );
		ModelRenderer?.SceneObject.Attributes.Set( "CTintColor", TintC );
	}

	private void SetBatchable( bool value )
	{
		// Ensure both mdlrender and its SceneObject are valid before assigning
		if ( ModelRenderer?.SceneObject is not null )
			ModelRenderer.SceneObject.Batchable = value;
		else
			Log.Warning( $"[SetBatchable] Skipped setting Batchable — mdlrender or SceneObject was null." );
	}

}
