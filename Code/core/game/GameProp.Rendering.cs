namespace Core;

partial class GameProp
{
	private ulong _bodyGroups = ulong.MaxValue;

	[Property, Order( 12 ), Header( "Rendering" ), Sync]
	[Model.BodyGroupMask]
	[ShowIf( "HasBodyGroups", true )]
	public ulong BodyGroups
	{
		get => _bodyGroups;
		set
		{
			if ( _bodyGroups != value )
			{
				_bodyGroups = value;
				if ( ModelRenderer.IsValid() ) ModelRenderer.BodyGroups = BodyGroups;
			}
		}
	}

	protected bool HasBodyGroups { get { return Model?.BodyParts.Sum( ( Model.BodyPart x ) => x.Choices.Count ) > 1; } }

	private string _materialGroup;

	[Property, Order( 11 ), Header( "Rendering" ), Title( "Skin" ), Sync]
	[Model.MaterialGroup]
	[ShowIf( "HasMaterialGroups", true )]
	public string MaterialGroup
	{
		get => _materialGroup;
		set
		{
			if ( (_materialGroup != value) )
			{
				_materialGroup = value;
				if ( ModelRenderer.IsValid() ) ModelRenderer.MaterialGroup = MaterialGroup;
			}
		}
	}

	protected bool HasMaterialGroups { get { return Model?.MaterialGroupCount > 0; } }

	[Property, Order( 17 ), Title( "Cast Shadows?" )]
	public ModelRenderer.ShadowRenderType shadowRenderType
	{
		get => _shadowRenderType;
		set
		{
			if ( (_shadowRenderType != value) )
			{
				_shadowRenderType = value;

				if ( ModelRenderer.IsValid() )
					ModelRenderer.RenderType = _shadowRenderType;
			}
		}
	}

	private ModelRenderer.ShadowRenderType _shadowRenderType;
}
