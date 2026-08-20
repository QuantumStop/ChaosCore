namespace Core;

using System;

public static partial class Effects
{
	static public class BloodColor
	{
		/// <summary>
		/// Struct to hold both colors
		/// </summary>
		public struct Age
		{
			public Color Fresh { get; set; }
			public Color Old { get; set; }
		}
		/// <summary>
		/// Enum with all available blood colors, to be expanded
		/// </summary>
		public enum ColorList
		{
			[Description( "Most commonly found on Earth-native creatures" ), Icon( "public" )]
			Red,
			[Description( "Color preferrable to Xen creatures" ), Icon( "public_off" )]
			Yellow,
			[Description( "Some sort of saline solution used for some of combine synths" ), Icon( "emergency" )]
			Pale,
			[Description( "Nobody knows the real reason behind this color, but it can be found on certain combine army units" ), Icon( "sentiment_very_dissatisfied" )]
			Blue
		}

		// feel free to convert these out from 255 rgb into 0-1 rgb, but im lazy

		/// <summary>
		/// Most commonly found on Earth-native creatures
		/// </summary>
		public static readonly Age Red = new()
		{
			Fresh = new Color32( 80, 0, 0 ),
			Old = new Color32( 31, 10, 10 )
		};

		/// <summary>
		/// Color preferrable to Xen creatures
		/// </summary>
		public static readonly Age Yellow = new()
		{
			Fresh = new Color32( 80, 80, 0 ),
			Old = new Color32( 64, 44, 0 )
		};

		/// <summary>
		/// Some sort of saline solution used for some of combine synths
		/// </summary>
		public static readonly Age Pale = new()
		{
			Fresh = new Color32( 80, 80, 90 ),
			Old = new Color32( 39, 39, 64 )
		};

		/// <summary>
		/// Nobody knows the real reason behind this color, but it can be found on certain combine army units
		/// </summary>
		public static readonly Age Blue = new()
		{
			Fresh = new Color32( 0, 0, b: 80 ),
			Old = new Color32( 0, 39, 64 )
		};

		public static Color ConvertColor( in ColorList color, bool Old = false )
		{
			Color32 result = color switch
			{
				ColorList.Yellow => Old ? Yellow.Old : Yellow.Fresh,
				ColorList.Pale => Old ? Pale.Old : Pale.Fresh,
				ColorList.Blue => Old ? Blue.Old : Blue.Fresh,
				_ => Old ? Red.Old : Red.Fresh,
			};

			return result;
		}
	}

	/// <summary>
	/// You can't use the LifeTime feature of the decal without it deleting itself, which is why we do it separately here
	/// </summary>
	public class BloodDrier : BaseEntity
	{
		protected override string GetEditorVis() { return null; }
#if IGNIS
		[DebugExpose]
#endif
		[Property, FeatureEnabled( "Custom Color" )] private bool _useCustomColor { get; set; } = false;
		[Property, Feature( "Custom Color" )] public Gradient CustomColor { get; set; }
		[Property, ReadOnly, Feature( "Debug" )] public Decal Decal { get; set; }
		[Property] public float TimeToDry { get; set; } = 60f;
		/// <summary>
		/// The predefined colors
		/// </summary>
		[Property, Space] public BloodColor.ColorList PresetColor { get; set; }
		/// <summary>
		/// We can cheat and sort of get a multiply effect by putting the Color Mix to -1, but that inverts the colors so we have to account for that, also the edges are a tiny bit shit but should be ok
		/// </summary>
		[Property] public bool CoolBlendMode { get; set; } = false;

		private float _startScale;
		[Property, ReadOnly, Feature( "Debug" )] private Gradient _definedColors = Color.Red;
		private TimeSince _time { get; set; }

		protected override void OnStart()
		{
			if ( Components.TryGet<Decal>( out var comp ) )
			{
				Decal = comp;
				_startScale = Decal.Scale.ConstantValue;
				Decal.ColorMix = CoolBlendMode ? -1 : Decal.ColorMix;
			}

			if ( !_useCustomColor )
			{
				_definedColors = new();
				_definedColors.AddColor( 0, CoolBlendMode ? BloodColor.ConvertColor( PresetColor ).Invert() : BloodColor.ConvertColor( PresetColor ) );
				_definedColors.AddColor( x: 1, CoolBlendMode ? BloodColor.ConvertColor( PresetColor, true ).Invert() : BloodColor.ConvertColor( PresetColor, true ) );
			}

			_time = 0;
		}

		[Property, FeatureEnabled( "Spill" )] public bool SpillEnabled { get; set; } = false;
		[Property, Feature( "Spill" ), Range( 1, 2 )] public float SpillSize { get; set; } = 1;

		protected override void OnFixedUpdate()
		{
			base.OnFixedUpdate();

			if ( _time < TimeToDry )
			{
				Decal?.Scale = MathX.Lerp( _startScale, _startScale * SpillSize, MathX.Remap( _time, 0, TimeToDry, 0, 1 ) );
				Decal?.ColorTint = _useCustomColor ? CustomColor.Evaluate( MathX.Remap( _time, 0, TimeToDry ) ) : _definedColors.Evaluate( MathX.Remap( _time, 0, TimeToDry ) );
			}
		}
	}
}
