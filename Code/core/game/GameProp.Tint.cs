namespace Core;

public partial class GameProp
{
	[Property, Order( 70 ), Feature( "Debug" ), ReadOnly, Title( "Is Prop Batchable?" )] public bool PropBatchableDebug { get; set; }

	public enum PropTintCount
	{
		[Description( "A single Tint + Mask" )]
		OneTint,
		[Description( "Two combinations of Tint + Mask" )]
		TwoTints,
		[Description( "Three of everything" )]
		ThreeTints
	}

	[Property, Order( 10 ), Title( "Tint Mode" ), Group( "Render Properties" )]
	public PropTintCount WhichTints;
#if IGNIS
	[DebugExpose]
#endif

	[Property, Order( 10 ), Group( "Render Properties" )]
	public Color Tint
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;

				if ( tintComponent.IsValid() && tintComponent.ModelRenderer.IsValid() ) // check specifically if the tint component has a reference, so theres no pant shitting
				{
					tintComponent.TintA = field;

					if ( WhichTints == PropTintCount.OneTint )
					{
						tintComponent.TintB = new Color( 1f, 1f, 1f, 1f );
						tintComponent.TintC = new Color( 1f, 1f, 1f, 1f );
					}
				}
			}
		}
	} = Color.White.WithAlpha( 1 );

	[Property, Group( "Render Properties" ), Order( 10 ), ShowIf( "WhichTints", PropTintCount.TwoTints ),
	ShowIf( "WhichTints", PropTintCount.ThreeTints )]
	public Color TintB
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;

				if ( tintComponent.IsValid() && tintComponent.ModelRenderer.IsValid() )
				{
					tintComponent.TintB = value;

					if ( WhichTints == PropTintCount.TwoTints )
					{
						tintComponent.TintC = new Color( 1f, 1f, 1f, 1f );
					}
				}

			}
		}
	} = Color.White.WithAlpha( 1 );

	[Property, Group( "Render Properties" ), Order( 10 ), ShowIf( "WhichTints", PropTintCount.ThreeTints )]
	public Color TintC
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;

				if ( tintComponent.IsValid() && tintComponent.ModelRenderer.IsValid() )
					tintComponent.TintC = value;
			}
		}
	} = Color.White.WithAlpha( 1 );

	private TintComponent tintComponent;

	void CreateTintComponent()
	{
		tintComponent = Components.GetOrCreate<TintComponent>();

		tintComponent.IsOnProp = true;

		tintComponent.TintA = Tint;
		tintComponent.TintB = TintB;
		tintComponent.TintC = TintC;

		AddProcedural( tintComponent );
	}
}
