namespace Core;

public partial class GameProp
{
	[Property, Order( 70 ), Feature( "Debug" ), ReadOnly, Title( "Is Prop Batchable?" )] public bool PropBatchableDebug { get; set; }

	public enum PropTintCount
	{
		/// <summary>A single Tint + Mask</summary>
		OneTint,
		/// <summary>Two combinations of Tint + Mask</summary>
		TwoTints,
		/// <summary>Three of everything</summary>
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

				if ( _tintComponent.IsValid() && _tintComponent.ModelRenderer.IsValid() ) // check specifically if the tint component has a reference, so theres no pant shitting
				{
					_tintComponent.TintA = field;

					if ( WhichTints == PropTintCount.OneTint )
					{
						_tintComponent.TintB = new Color( 1f, 1f, 1f, 1f );
						_tintComponent.TintC = new Color( 1f, 1f, 1f, 1f );
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

				if ( _tintComponent.IsValid() && _tintComponent.ModelRenderer.IsValid() )
				{
					_tintComponent.TintB = value;

					if ( WhichTints == PropTintCount.TwoTints )
					{
						_tintComponent.TintC = new Color( 1f, 1f, 1f, 1f );
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

				if ( _tintComponent.IsValid() && _tintComponent.ModelRenderer.IsValid() )
					_tintComponent.TintC = value;
			}
		}
	} = Color.White.WithAlpha( 1 );

	private TintComponent _tintComponent;

	void CreateTintComponent()
	{
		_tintComponent = Components.GetOrCreate<TintComponent>();

		_tintComponent.IsOnProp = true;

		_tintComponent.TintA = Tint;
		_tintComponent.TintB = TintB;
		_tintComponent.TintC = TintC;

		AddProcedural( _tintComponent );
	}
}
