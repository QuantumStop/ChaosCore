namespace Core;

using Sandbox;
using Sandbox.Audio;
using System;

public enum SpatialDirectorDspFamily
{
	RoomEmpty,
	RoomDiffuse,
	DuctEmpty,
	DuctDiffuse,
	Metallic,
	Tunnel,
	Chamber,
	Brite,
	Concrete,
	Cavern
}

public enum SpatialDirectorDspSize
{
	Small,
	Medium,
	Large
}

internal enum SpatialDirectorDspTransition
{
	Idle,
	FadeOut,
	Switch,
	FadeIn
}

public sealed partial class SpatialDirector
{
	[InfoBox( "DSP is currently using S&Box built-in mixer and it's presets. In the future this could work with FMOD as well." )]
	[Property, Feature( "DSP" ), Title( "Enable DSP" ), Order( -100 )]
	public bool DspEnabled { get; set; } = false;

	[Property, Title( "Mixer" ), Feature( "DSP" )]
	public MixerHandle DspMixer { get; set; }

	[Property, Title( "Mix" ), Feature( "DSP" ), Range( 0f, 1f )]
	public float DspMix { get; set; } = 1f;

	[Property, Title( "Blend Speed" ), Feature( "DSP" ), Range( 0.1f, 10f )]
	public float DspBlendSpeed { get; set; } = 3f;

	// Definition

	[Property, Title( "Small Scale" ), Feature( "DSP" ), Group( "Definition" ), Range( 16f, 1024f )]
	public float DspSmallScale { get; set; } = 140f;

	[Property, Title( "Medium Scale" ), Feature( "DSP" ), Group( "Definition" ), Range( 32f, 2048f )]
	public float DspMediumScale { get; set; } = 320f;

	[Property, Title( "Corridor Ratio" ), Feature( "DSP" ), Group( "Definition" ), Range( 1f, 4f )]
	public float DspCorridorRatio { get; set; } = 1.5f;

	[Property, Title( "Corridor Min Hit Coverage" ), Feature( "DSP" ), Group( "Definition" ), Range( 0f, 1f )]
	public float DspCorridorMinHitCoverage { get; set; } = 0.45f;

	// Interior

	[Property, Title( "Interior Family" ), Feature( "DSP" ), Group( "Interior" )]
	public SpatialDirectorDspFamily DspInteriorFamily { get; set; } = SpatialDirectorDspFamily.RoomEmpty;

	[Property, Title( "Detect Corridors" ), Feature( "DSP" ), Group( "Interior" )]
	public bool DspDetectCorridors { get; set; } = true;

	[Property, Title( "Corridor Family" ), Feature( "DSP" ), Group( "Interior" )]
	public SpatialDirectorDspFamily DspCorridorFamily { get; set; } = SpatialDirectorDspFamily.Tunnel;

	// Outside

	[Property, Title( "Outside Enabled" ), Feature( "DSP" ), Group( "Outside" )]
	public bool DspOutsideEnabled { get; set; } = true;

	[Property, Title( "Outside Medium Openness" ), Feature( "DSP" ), Group( "Outside" ), Range( 0f, 1f )]
	public float DspOutsideMediumOpenness { get; set; } = 0.45f;

	[Property, Title( "Outside Large Openness" ), Feature( "DSP" ), Group( "Outside" ), Range( 0f, 1f )]
	public float DspOutsideLargeOpenness { get; set; } = 0.75f;


	[Property, Feature( "Debug" ), ReadOnly]
	public string DspCurrentPreset => _dspCurrentPreset ?? "";

	[Property, Feature( "Debug" ), ReadOnly]
	public string DspTargetPreset => _dspTargetPreset ?? "";

	[Property, Feature( "Debug" ), ReadOnly]
	public string DspEnvironment => _dspEnvironment ?? "";

	[Property, Feature( "Debug" ), ReadOnly]
	public float DspCurrentMix => _dspProcessor?.Mix ?? 0f;

	[Property, Feature( "Debug" ), ReadOnly]
	public float DspSpatialScale => _dspSpatialScale;

	[Property, Feature( "Debug" ), ReadOnly]
	public float DspCorridorFactor => _dspCorridorFactor;

	private Mixer _dspActiveMixer;
	private DspProcessor _dspProcessor;

	private string _dspCurrentPreset;
	private string _dspTargetPreset;
	private string _dspEnvironment;

	private float _dspSpatialScale;
	private float _dspCorridorFactor;

	private SpatialDirectorDspTransition _dspTransition;

	/// <summary>
	/// Updates DSP processor and fades it out when disabled.
	/// </summary>
	private void UpdateDsp()
	{
		if ( !DspEnabled || !SpatialResult.HasSample )
		{
			UpdateDspDisabled();
			return;
		}

		var mixer = DspMixer.GetOrDefault();

		if ( mixer is null )
			return;

		if ( !EnsureDspProcessor( mixer ) )
			return;

		var targetPreset = ResolveDspPreset();

		if ( string.IsNullOrWhiteSpace( targetPreset ) )
		{
			UpdateDspDisabled();
			return;
		}

		RequestDspPreset( targetPreset );
		UpdateDspTransition();
	}

	private bool EnsureDspProcessor( Mixer mixer )
	{
		if ( _dspProcessor is not null )
		{
			if ( _dspActiveMixer == mixer )
				return true;

			return false;
		}

		var preset = ResolveDspPreset();

		if ( string.IsNullOrWhiteSpace( preset ) )
			preset = "outside.medium";

		_dspProcessor = new DspProcessor( preset )
		{
			Enabled = true,
			Mix = 0f
		};

		mixer.AddProcessor( _dspProcessor );

		_dspActiveMixer = mixer;
		_dspCurrentPreset = preset;
		_dspTargetPreset = preset;
		_dspTransition = SpatialDirectorDspTransition.FadeIn;

		return true;
	}

	private void RequestDspPreset( string preset )
	{
		if ( string.Equals( preset, _dspTargetPreset, StringComparison.OrdinalIgnoreCase ) )
			return;

		_dspTargetPreset = preset;

		if ( string.Equals( _dspTargetPreset, _dspCurrentPreset, StringComparison.OrdinalIgnoreCase ) )
		{
			_dspTransition = SpatialDirectorDspTransition.FadeIn;
			return;
		}

		_dspTransition = SpatialDirectorDspTransition.FadeOut;
	}

	/// <summary>
	/// Fades the active processor to silence before switching its preset.
	/// </summary>
	private void UpdateDspTransition()
	{
		if ( _dspProcessor is null )
			return;

		var targetMix = Math.Clamp( DspMix, 0f, 1f );
		var step = WorldTime.Delta * MathF.Max( DspBlendSpeed, 0.01f );

		switch ( _dspTransition )
		{
			case SpatialDirectorDspTransition.Idle:
				_dspProcessor.Enabled = true;
				_dspProcessor.Mix = _dspProcessor.Mix.Approach( targetMix, step );
				break;

			case SpatialDirectorDspTransition.FadeOut:
				_dspProcessor.Mix = _dspProcessor.Mix.Approach( 0f, step );

				if ( _dspProcessor.Mix <= 0.001f )
				{
					_dspProcessor.Mix = 0f;
					_dspTransition = SpatialDirectorDspTransition.Switch;
				}
				break;

			case SpatialDirectorDspTransition.Switch:
				// Keep the same AudioProcessor alive.
				// Only request a new DSP preset.
				_dspProcessor.Effect = _dspTargetPreset;
				_dspCurrentPreset = _dspTargetPreset;
				_dspTransition = SpatialDirectorDspTransition.FadeIn;
				break;

			case SpatialDirectorDspTransition.FadeIn:
				_dspProcessor.Enabled = true;
				_dspProcessor.Mix = _dspProcessor.Mix.Approach( targetMix, step );

				if ( MathF.Abs( _dspProcessor.Mix - targetMix ) <= 0.001f )
				{
					_dspProcessor.Mix = targetMix;
					_dspTransition = SpatialDirectorDspTransition.Idle;
				}
				break;
		}
	}

	private void UpdateDspDisabled()
	{
		if ( _dspProcessor is null )
			return;

		var step = WorldTime.Delta * MathF.Max( DspBlendSpeed, 0.01f );

		_dspProcessor.Mix = _dspProcessor.Mix.Approach( 0f, step );

		if ( _dspProcessor.Mix <= 0.001f )
		{
			_dspProcessor.Mix = 0f;
			_dspProcessor.Enabled = false;
		}
	}

	/// <summary>
	/// Chooses an environment preset from the shared enclosure and radial samples.
	/// </summary>
	private string ResolveDspPreset()
	{
		var result = SpatialResult;

		if ( !result.IsCovered )
		{
			_dspEnvironment = "Outside";
			_dspCorridorFactor = 0f;
			_dspSpatialScale = result.AverageOpenDistance;

			if ( !DspOutsideEnabled )
				return null;

			return ResolveOutsidePreset( ResolveOutsideDspSize( result.Openness ) );
		}

		_dspSpatialScale = CalculateInteriorSpatialScale( result );
		_dspCorridorFactor = CalculateCorridorFactor( result );

		var size = ResolveInteriorDspSize( _dspSpatialScale );

		if ( DspDetectCorridors && _dspCorridorFactor >= DspCorridorRatio 
			&& result.RadialHitCoverage >= DspCorridorMinHitCoverage )
		{
			_dspEnvironment = $"Corridor {size}";
			return ResolveFamilyPreset( DspCorridorFamily, size );
		}

		_dspEnvironment = $"Interior {size}";
		return ResolveFamilyPreset( DspInteriorFamily, size );
	}

	private static float CalculateInteriorSpatialScale( SpatialProbeResult result )
	{
		var horizontal = MathF.Max( result.AverageOpenDistance, 0f );

		var vertical = result.AverageCeilingDepth > 0f
			? result.AverageCeilingDepth
			: result.CeilingDepth;

		if ( vertical <= 0f )
			return horizontal;

		return horizontal * 0.7f + vertical * 0.3f;
	}

	private static float CalculateCorridorFactor( SpatialProbeResult result )
	{
		var average = MathF.Max( result.AverageOpenDistance, 1f );

		return result.FurthestOpenDistance / average;
	}

	private SpatialDirectorDspSize ResolveInteriorDspSize( float scale )
	{
		if ( scale <= DspSmallScale )
			return SpatialDirectorDspSize.Small;

		if ( scale <= DspMediumScale )
			return SpatialDirectorDspSize.Medium;

		return SpatialDirectorDspSize.Large;
	}

	private SpatialDirectorDspSize ResolveOutsideDspSize( float openness )
	{
		if ( openness >= DspOutsideLargeOpenness )
			return SpatialDirectorDspSize.Large;

		if ( openness >= DspOutsideMediumOpenness )
			return SpatialDirectorDspSize.Medium;

		return SpatialDirectorDspSize.Small;
	}

	private static string ResolveOutsidePreset( SpatialDirectorDspSize size )
	{
		return size switch
		{
			SpatialDirectorDspSize.Small => "outside.small",
			SpatialDirectorDspSize.Medium => "outside.medium",
			_ => "outside.large"
		};
	}

	private static string ResolveFamilyPreset( SpatialDirectorDspFamily family, SpatialDirectorDspSize size )
	{
		return family switch
		{
			SpatialDirectorDspFamily.RoomEmpty => size == SpatialDirectorDspSize.Small ? "room.empty.small" : "room.empty.huge",
			SpatialDirectorDspFamily.RoomDiffuse => size == SpatialDirectorDspSize.Small ? "room.diffuse.small" : "room.diffuse.huge",
			SpatialDirectorDspFamily.DuctEmpty => size == SpatialDirectorDspSize.Small ? "duct.empty.small" : "duct.empty.huge",
			SpatialDirectorDspFamily.DuctDiffuse => size == SpatialDirectorDspSize.Small ? "duct.diffuse.small" : "duct.diffuse.huge",

			SpatialDirectorDspFamily.Metallic => ResolveSizedPreset( "metallic", size ),
			SpatialDirectorDspFamily.Tunnel => ResolveSizedPreset( "tunnel", size ),
			SpatialDirectorDspFamily.Chamber => ResolveSizedPreset( "chamber", size ),
			SpatialDirectorDspFamily.Brite => ResolveSizedPreset( "brite", size ),
			SpatialDirectorDspFamily.Concrete => ResolveSizedPreset( "concrete", size ),
			SpatialDirectorDspFamily.Cavern => ResolveSizedPreset( "cavern", size ),

			_ => "room.empty.small"
		};
	}

	private static string ResolveSizedPreset( string family, SpatialDirectorDspSize size )
	{
		var resolvedSize = size switch
		{
			SpatialDirectorDspSize.Small => "small",
			SpatialDirectorDspSize.Medium => "medium",
			_ => "large"
		};

		return $"{family}.{resolvedSize}";
	}

	/// <summary>
	/// Silences the processor when the entity or supported view becomes inactive.
	/// </summary>
	private void DisableDspImmediately()
	{
		if ( _dspProcessor is null )
			return;

		_dspProcessor.Mix = 0f;
		_dspProcessor.Enabled = false;
	}
}
