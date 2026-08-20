namespace Core;

partial class GameProp
{
	[Property, Order( 12 ), Header( "Rendering" ), Sync]
	[Model.BodyGroupMask]
	[ShowIf( nameof( _hasBodyGroups ), true )]
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

	protected bool _hasBodyGroups => Model?.Parts.All.Sum( x => x.Choices.Count ) > 1;

	[Property, Order( 11 ), Header( "Rendering" ), Title( "Skin" ), Sync]
	[Model.MaterialGroup]
	[ShowIf( nameof( _hasMaterialGroups ), true )]
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

	protected bool _hasMaterialGroups => Model?.MaterialGroupCount > 0;

	[Property, Order( 11 ), Title( "Cast Shadows?" ), Group( "Render Properties" )]
	public ModelRenderer.ShadowRenderType ShadowRenderType
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				if ( _modelRenderer.IsValid() ) _modelRenderer.RenderType = value;
			}
		}
	}
}
