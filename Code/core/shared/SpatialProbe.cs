namespace Core;

using System;
using System.Collections.Generic;

/// <summary>
/// A spatial probe is a system for sampling visibility and occlusion in the world.
/// 
/// <para> 
/// 	The probe can sample in two modes:
/// 	<para> - Overhead, the probe casts rays downward from a point above the origin, sampling the world below. </para>
/// 	<para> - Radial, the probe casts rays outward in a circle around the origin, sampling the world in a horizontal plane. </para>
/// </para>
/// 
/// <remarks>
/// 	Can be configured with various settings, such as the number of rays to cast, the distance to sample, 
/// 	and whether to use an arc or full circle for radial sampling.
/// </remarks>
/// 
/// </summary>
public partial class SpatialProbe
{
	public SpatialProbeSettings Settings { get; set; } = SpatialProbeSettings.Default;
	public SpatialProbeResult Result { get; private set; }

	public IReadOnlyList<SpatialProbeOverheadSample> OverheadSamples => _overheadSamples;
	public IReadOnlyList<SpatialProbeRadialSample> RadialSamples => _radialSamples;

	private SpatialProbeOverheadSample[] _overheadSamples = [];
	private SpatialProbeRadialSample[] _radialSamples = [];
	private float _updateAccumulator;

	public bool Update( SpatialProbeContext context, bool force = false ) => Update( context, WorldTime.Delta, force );

	public bool Update( SpatialProbeContext context, float delta, bool force = false )
	{
		if ( context.Scene is null )
			return false;

		_updateAccumulator += MathF.Max( delta, 0f );

		var interval = MathF.Max( Settings.UpdateInterval, 0f );
		if ( !force && interval > 0f && _updateAccumulator < interval )
			return false;

		_updateAccumulator = interval > 0f ? _updateAccumulator % interval : 0f;

		Result = new SpatialProbeResult
		{
			HasSample = true,
			Origin = context.Origin
		};

		if ( Settings.OverheadEnabled )
			UpdateOverhead( context );
		else
			_overheadSamples = [];

		if ( Settings.RadialEnabled )
		{
			if ( Settings.SamplingMode == SpatialProbeSamplingMode.Arc )
				UpdateArc( context );
			else
				UpdateRadial( context );
		}
		else
			_radialSamples = [];

		UpdateDerivedContext();
		return true;
	}

	public void Reset()
	{
		Result = default;
		_overheadSamples = [];
		_radialSamples = [];
		_updateAccumulator = 0f;
	}
}
