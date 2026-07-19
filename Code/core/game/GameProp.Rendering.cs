namespace Core;

partial class GameProp
{
	[Property, Order( 12 ), Header( "Rendering" ), Sync]
	[Model.BodyGroupMask]
	[ShowIf( nameof( HasBodyGroups ), true )]
	public ulong BodyGroups
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				if ( _modelRenderer.IsValid() ) _modelRenderer.BodyGroups = value;
			}
		}
	} = ulong.MaxValue;

	protected bool HasBodyGroups => Model?.Parts.All.Sum( x => x.Choices.Count ) > 1;

	[Property, Order( 11 ), Header( "Rendering" ), Title( "Skin" ), Sync]
	[Model.MaterialGroup]
	[ShowIf( nameof( HasMaterialGroups ), true )]
	public string MaterialGroup
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				if ( _modelRenderer.IsValid() ) _modelRenderer.MaterialGroup = value;
			}
		}
	}

	protected bool HasMaterialGroups => Model?.MaterialGroupCount > 0;

	[Property, Order( 11 ), Title( "Cast Shadows?" ), Group( "Render Properties" )]
	public ModelRenderer.ShadowRenderType ShadowRenderType
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;

				if ( _modelRenderer.IsValid() )
					_modelRenderer.RenderType = value;
			}
		}
	}
}
