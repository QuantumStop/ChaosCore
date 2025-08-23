namespace Core;

public partial class GameProp
{
	private Color _tintA = new Color( 1f, 1f, 1f, 1f );
	private Color _tintB = new Color( 1f, 1f, 1f, 1f );
	private Color _tintC = new Color( 1f, 1f, 1f, 1f );

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

	[Property, Order( 13 ), Title( "Tint Mode" )]
	public PropTintCount WhichTints;
	[DebugExpose]

	[Property, Order( 14 )]
	public Color Tint
	{
		get => _tintA;
		set
		{
			if ( (_tintA != value) )
			{
				_tintA = value;

				if ( tintComponent.mdlrender.IsValid() )
				{
					tintComponent.TintA = _tintA;

					if ( WhichTints == PropTintCount.OneTint )
					{
						tintComponent.TintB = new Color( 1f, 1f, 1f, 1f );
						tintComponent.TintC = new Color( 1f, 1f, 1f, 1f );
					}
				}
			}
		}
	}

	[Property, Order( 15 ), ShowIf( "WhichTints", PropTintCount.TwoTints ), ShowIf( "WhichTints", PropTintCount.ThreeTints )]
	public Color TintB
	{
		get => _tintB;
		set
		{
			if ( (_tintB != value) )
			{
				_tintB = value;
				if ( tintComponent.mdlrender.IsValid() )
				{
					tintComponent.TintB = _tintB;

					if ( WhichTints == PropTintCount.TwoTints )
					{
						tintComponent.TintC = new Color( 1f, 1f, 1f, 1f );
					}
				}

			}
		}
	}

	[Property, Order( 16 ), ShowIf( "WhichTints", PropTintCount.ThreeTints )]
	public Color TintC
	{
		get => _tintC;
		set
		{
			if ( (_tintC != value) )
			{
				_tintC = value;
				if ( tintComponent.mdlrender.IsValid() )
					tintComponent.TintC = _tintC;
			}
		}
	}

	private TintComponent tintComponent;

	void CreateTintComponent()
	{
		tintComponent = Components.GetOrCreate<TintComponent>();

		tintComponent.IsOnProp = true;

		tintComponent.TintA = _tintA;
		tintComponent.TintB = _tintB;
		tintComponent.TintC = _tintC;

		AddProcedural( tintComponent );
	}
}
