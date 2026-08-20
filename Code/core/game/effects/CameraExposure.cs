namespace Core;

[Title( "Exposure Manager" )]
public class CameraExposure : BaseEntity, Component.ExecuteInEditor
{
	public enum ExposureMode
	{
		[Icon( "exposure" ), Description( "Don't use external exposure (for Physical Light Units), at this point just probably dont spawn this and thats it. Not that it would do anything if you dont have proper shaders." )]
		None,
		[Icon( "exposure" ), Description( "Manually controlling exposure through the exposure triangle" )]
		Manual,
		[Icon( "hdr_auto" ), Description( "Exposure is controlled by setting the value in each zone" )]
		ManualZoning,
		[Icon( "hdr_auto" ), Description( "Exposure is controlled automatically through metering bullshit" )]
		AutoHistogram
	}

	static public CameraExposure Instance;
#if PLU
	[DebugExpose, Property, ReadOnly, Feature( "Debug" )]
	public float ResultExposure
	{
		get;
		private set
		{
			if ( field != value )
			{
				field = value;
				Dirty();
			}
		}
	} = 1f;

	[DebugExpose, Property, ReadOnly]
	public float RealEV
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
	} = 0;

	[DebugExpose, Property]
	public ExposureMode Mode
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
	} = ExposureMode.Manual;
	[Space]

	// Manual

	// Default settings are set for the Sunny 16
	[DebugExpose, Header( "Settings" ), Range( 100, 1600 ), Step( 100 ), Property, Title( "ISO" ), ShowIf( nameof( Mode ), ExposureMode.Manual )]
	public float ISO
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
	} = 100f;
	[DebugExpose, Property, Title( "Shutter Speed (1 / float)" ), Range( 30, 1000 ), Step( 10 ), ShowIf( nameof( Mode ), ExposureMode.Manual )]
	public float Shutter
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
	} = 100f;
	[DebugExpose, Property, Title( "Aperture" ), Range( 1.4f, 22f ), Step( 0.1f ), ShowIf( nameof( Mode ), ExposureMode.Manual )]
	public float Aperture
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
	} = 16f;
	// Manual with Zoning
	[DebugExpose, Header( "Settings" ), Property, Title( "Target EV" ), Range( -4, 16 ), ShowIf( nameof( Mode ), ExposureMode.ManualZoning )]
	public float TargetEV
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
	} = 0;
	[Property, Title( "Sun ND" ), Header( "Hacks" ), Description( "Cheap hack to expose the sky correctly in cases where it sucks" ), Range( 0, 1 ), Step( 0.1f )] public float SunND { get; set; } = 1f;

	private void Dirty()
	{
		switch ( Mode )
		{
			case ExposureMode.None:
				TargetEV = -0.263f; // bullshit value to get 1
				break;
			case ExposureMode.Manual:
				TargetEV = CalculateEV( Aperture * Aperture, 1 / Shutter, ISO );
				break;
		}

		Blend = 0;

		Scene.RenderAttributes.Set( "SunND", SunND );
	}

	/// <summary>
	/// Calculate EV100 value from all the settings
	/// </summary>
	/// <param name="aperture">Aperture value</param>
	/// <param name="shutterSpeed">Shutter Speed value</param>
	/// <param name="sensitivity">ISO value</param>
	/// <returns>EV100 to be converted</returns>
	private static float CalculateEV( float aperture, float shutterSpeed, float sensitivity )
	{
		float math = aperture / shutterSpeed;

		return MathF.Log2( math * (100 / sensitivity) );
	}

	/// <summary>
	/// Calculate the exposure bias to send to the shader
	/// </summary>
	/// <param name="EV100">Manual setting or automatic metering value</param>
	/// <returns>Exposure bias to send to the render</returns>
	private static float CalculateExposureFromEV100( float EV100 )
	{
		float maxLuminance = 1.2f * MathF.Pow( 2f, EV100 );
		return 1.0f / maxLuminance;
	}

	[DebugExpose, Property, Range( 0, 1 ), Feature( "Debug" ), ReadOnly]
	private float Blend
	{
		get;
		set
		{
			if ( field != value )
			{
				field = value;
				BlendChange();
			}
		}
	}

	private void BlendChange()
	{
		RealEV = MathX.Lerp( RealEV, TargetEV, Blend );
		ResultExposure = CalculateExposureFromEV100( RealEV );

		Scene.RenderAttributes.Set( "CalculatedEV", RealEV );
		Scene.RenderAttributes.Set( "ExposureFromEV", ResultExposure );
	}

	protected override void OnUpdate()
	{
		Blend = EasingPlus.EaseOutQuad( Math.Clamp( Blend + Time.Delta, 0f, 1f ) );
	}
#else
	[Button]
	public void ClearExposure() => Scene.RenderAttributes.Set( "ExposureFromEV", 1f );
#endif



	protected override void OnStart()
	{
		base.OnStart();
		if ( !IsProxy ) Instance = this;
	}
}
