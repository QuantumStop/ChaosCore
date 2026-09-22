namespace Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Sandbox;
using Sandbox.Diagnostics;

/// <summary>
/// API for inspecting, validating, reading, and applying generic presets.
/// This is what should be interacted with to set presets, while a game is live.
/// </summary>
public static class PresetAPI
{
	internal sealed class PresetCompiledApplyPlan
	{
		public string PayloadJson { get; init; }
		public string PresetType { get; init; }
		public string PresetMode { get; init; }
		public Type TargetType { get; init; }
		public PresetCompiledBinding[] Bindings { get; init; }
	}

	internal sealed class PresetCompiledBinding
	{
		public PropertyDescription Property { get; init; }
		public JsonNode Node { get; init; }
		public object Value { get; init; }
		public bool ReuseValue { get; init; }
	}

	private static readonly Dictionary<GenericPresetResource, Dictionary<Type, PresetCompiledApplyPlan>> _compiledPlans = new();

	/// <summary>
	/// Returns whether a preset can be applied to the supplied target.
	/// </summary>
	public static bool CanApply( GenericPresetResource preset, object target )
	{
		if ( preset is null || target is null )
			return false;

		var targetInfo = PresetRegistry.Find( preset.PresetType, target.GetType() );

		if ( targetInfo is null )
			return false;

		return targetInfo.SupportsMode( PresetModes.Normalize( preset.PresetMode ) );
	}

	/// <summary>
	/// Applies the preset to the target and, when possible, updates the target's
	/// PresetSelectorAttribute resource reference to the applied preset.
	/// </summary>
	public static bool TrySetAndApply( GenericPresetResource preset, Component target )
	{
		if ( preset is null || target is null )
			return false;

		if ( !TryApply( preset, target ) )
			return false;

		var type = Game.TypeLibrary.GetType( target.GetType() );

		if ( type is null )
			return false;

		foreach ( var property in type.Properties )
		{
			if ( property.PropertyType != typeof( GenericPresetResource ) || !property.CanWrite )
				continue;

			property.SetValue( target, preset );
			break;
		}

		return true;
	}

	/// <summary>
	/// Applies a preset to a component when compatible.
	/// </summary>
	internal static bool TryApply( GenericPresetResource preset, Component target )
	{
		if ( preset is null || target is null )
			return false;

		var plan = GetCompiledApplyPlan( preset, target );

		return TryApplyCompiledPlan( plan, target );
	}

	internal static bool TryApplyCompiledPlan( PresetCompiledApplyPlan plan, object target )
	{
		if ( plan is null || target is null || plan.TargetType != target.GetType() )
			return false;

		var changed = false;

		foreach ( var binding in plan.Bindings )
		{
			try
			{
				var value = binding.ReuseValue
					? binding.Value
					: binding.Node is null
						? null
						: Json.FromNode( binding.Node, binding.Property.PropertyType );

				binding.Property.SetValue( target, value );
				changed = true;
			}
			catch ( Exception e )
			{
				Log.Warning(
					$"Failed applying preset '{plan.PresetType}' property " +
					$"'{binding.Property.Name}' to '{target.GetType().Name}': {e.Message}"
				);
			}
		}

		return changed;
	}

	/// <summary>
	/// Returns the property names that would be affected by applying
	/// the preset to the supplied target.
	/// </summary>
	public static IReadOnlyList<string> GetAffectedProperties( GenericPresetResource preset, object target )
	{
		if ( preset is null || target is null )
			return [];

		var targetInfo = PresetRegistry.Find(
			preset.PresetType,
			target.GetType()
		);

		if ( targetInfo is null )
			return [];

		var mode = targetInfo.FindMode(
			PresetModes.Normalize( preset.PresetMode )
		);

		if ( mode is null )
			return [];

		var payload = ParsePayload( preset );

		if ( payload is null )
			return [];

		return [ .. GetPresetProperties( targetInfo, mode )
				.Where( x => payload.ContainsKey( x.Name ) )
				.Select( x => x.Name )
		];
	}

	/// <summary>
	/// Returns the properties participating in a preset target and mode.
	/// </summary>
	public static IEnumerable<PropertyDescription> GetPresetProperties( PresetTargetInfo target, PresetModeInfo mode )
	{
		if ( target?.TargetType is null || mode is null )
			yield break;

		foreach ( var property in GetCandidateProperties( target ) )
		{
			if ( !property.CanRead || !property.CanWrite )
				continue;

			if ( property.IsIndexer )
				continue;

			if ( property.PropertyType == typeof( GenericPresetResource ) )
				continue;

			if ( !HasAttribute<PropertyAttribute>( property ) )
				continue;

			if ( HasAttribute<PresetIgnoreAttribute>( property ) )
				continue;

			if ( mode.IncludeAll )
			{
				yield return property;
				continue;
			}

			var presetProperties = property.Attributes.OfType<PresetPropertyAttribute>();

			if ( presetProperties.Any( x => x.IncludesMode( mode.Key ) ) )
				yield return property;
		}
	}

	/// <summary>
	/// Attempts to read a typed value from a preset payload.
	/// </summary>
	public static bool TryGetValue<T>( GenericPresetResource preset, string propertyName, out T value )
	{
		value = default;

		if ( preset is null || string.IsNullOrWhiteSpace( propertyName ) )
			return false;

		var payload = ParsePayload( preset );

		if ( payload is null )
			return false;

		if ( !payload.TryGetPropertyValue( propertyName, out var node ) )
			return false;

		if ( node is null )
			return true;

		try
		{
			value = Json.FromNode<T>( node );
			return true;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed reading preset '{preset.PresetType}' property " +
				$"'{propertyName}' as '{typeof( T ).Name}': {e.Message}"
			);

			return false;
		}
	}

	/// <summary>
	/// Writes a typed value into an in-memory preset payload.
	/// </summary>
	public static bool SetValue<T>( GenericPresetResource preset, string propertyName, T value )
	{
		if ( preset is null || string.IsNullOrWhiteSpace( propertyName ) )
			return false;

		var payload = ParsePayload( preset );

		if ( payload is null )
			return false;

		try
		{
			payload[propertyName] = Json.ToNode( value, typeof( T ) );

			preset.PayloadJson = payload.ToJsonString();
			InvalidateCompiledPlan( preset );

			return true;
		}
		catch ( Exception e )
		{
			Log.Warning(
				$"Failed writing preset '{preset.PresetType}' property " +
				$"'{propertyName}': {e.Message}"
			);

			return false;
		}
	}

	internal static PresetCompiledApplyPlan GetCompiledApplyPlan( GenericPresetResource preset, object target )
	{
		if ( preset is null || target is null )
			return null;

		var targetType = target.GetType();

		if ( _compiledPlans.TryGetValue( preset, out var byType ) &&
			byType.TryGetValue( targetType, out var cached ) &&
			IsCompiledPlanCurrent( cached, preset, targetType ) )
		{
			return cached;
		}

		var plan = BuildCompiledApplyPlan( preset, target );

		if ( plan is null )
			return null;

		if ( byType is null )
		{
			byType = new Dictionary<Type, PresetCompiledApplyPlan>();
			_compiledPlans[preset] = byType;
		}

		byType[targetType] = plan;

		return plan;
	}

	internal static PresetCompiledApplyPlan BuildCompiledApplyPlan( GenericPresetResource preset, object target )
	{
		if ( preset is null || target is null )
			return null;

		var targetType = target.GetType();
		var targetInfo = PresetRegistry.Find( preset.PresetType, targetType );

		if ( targetInfo is null )
			return null;

		var mode = targetInfo.FindMode( PresetModes.Normalize( preset.PresetMode ) );

		if ( mode is null )
			return null;

		var payload = ParsePayload( preset );

		if ( payload is null )
			return null;

		var bindings = new List<PresetCompiledBinding>();

		foreach ( var property in GetPresetProperties( targetInfo, mode ) )
		{
			if ( !payload.TryGetPropertyValue( property.Name, out var node ) )
				continue;

			var reuseValue = node is null || CanReuseCompiledValue( property );
			object value = null;

			if ( reuseValue && node is not null )
			{
				try
				{
					value = Json.FromNode( node, property.PropertyType );
				}
				catch ( Exception e )
				{
					Log.Error(
						$"Failed compiling preset '{preset.PresetType}' property " +
						$"'{property.Name}' as '{property.PropertyType.Name}': {e.Message}" +
						$"Skipping"
					);

					continue;
				}
			}
			bindings.Add( new PresetCompiledBinding
			{
				Property = property,
				Node = reuseValue ? null : node,
				Value = value,
				ReuseValue = reuseValue
			} );
		}

		return new PresetCompiledApplyPlan
		{
			PayloadJson = preset.PayloadJson,
			PresetType = preset.PresetType,
			PresetMode = PresetModes.Normalize( preset.PresetMode ),
			TargetType = targetType,
			Bindings = [.. bindings]
		};
	}

	internal static void InvalidateCompiledPlan( GenericPresetResource preset )
	{
		if ( preset is not null )
			_compiledPlans.Remove( preset );
	}

	internal static void InvalidateCompiledPlans()
	{
		_compiledPlans.Clear();
	}

	private static bool IsCompiledPlanCurrent( PresetCompiledApplyPlan plan, GenericPresetResource preset, Type targetType )
	{
		return plan is not null &&
			plan.TargetType == targetType &&
			plan.PayloadJson == preset.PayloadJson &&
			plan.PresetType == preset.PresetType &&
			plan.PresetMode == PresetModes.Normalize( preset.PresetMode );
	}

	private static bool CanReuseCompiledValue( PropertyDescription property )
	{
		if ( property.PropertyType == typeof( string ) )
			return true;

		var type = Game.TypeLibrary.GetType( property.PropertyType );

		return type?.IsValueType == true;
	}

	private static IEnumerable<PropertyDescription> GetCandidateProperties( PresetTargetInfo target )
	{
		if ( target?.TargetType is null )
			return [];

		if ( target.IncludeInherited )
			return target.TargetType.Properties;

		return target.TargetType.DeclaredMembers.OfType<PropertyDescription>();
	}

	private static bool HasAttribute<T>( PropertyDescription property ) where T : Attribute
	{
		return property.Attributes.Any( x => x is T );
	}

	private static JsonObject ParsePayload( GenericPresetResource preset )
	{
		if ( preset is null )
			return null;

		if ( string.IsNullOrWhiteSpace( preset.PayloadJson ) )
			return [];

		try
		{
			return Json.ParseToJsonObject( preset.PayloadJson );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Failed parsing preset '{preset.PresetType}' payload: {e.Message}" );

			return null;
		}
	}
}


[Category( "Tests" )]
[Title( "Preset Benchmark" )]
public sealed class PresetBenchmark : Component
{
	[Property] public Component Target { get; set; }

	[Property, Range( 1, 100000 )] public int Iterations { get; set; } = 10000;

	[Property, Range( 0, 10000 )] public int WarmupIterations { get; set; } = 1000;

	[Property, Range( 1, 100 )] public int Batches { get; set; } = 20;

	[Property] public bool RunOnStart { get; set; }

	private readonly record struct PresetBinding( PropertyDescription Property, object Value );

	private readonly record struct BenchmarkResult( string Name, double MinimumMilliseconds,
		double AverageMilliseconds, double MaximumMilliseconds, double Microseconds );

	private int _sink;

	protected override void OnStart()
	{
		if ( RunOnStart )
			RunDetailed();
	}

	[Button( "Run Preset Benchmark" )]
	public void Run()
	{
		if ( !TryGetBenchmarkPreset( out var preset ) )
			return;

		if ( !PresetAPI.CanApply( preset, Target ) )
		{
			Log.Warning(
				$"[Preset Benchmark] Preset '{preset.PresetName}' " +
				$"cannot be applied to '{Target.GetType().Name}'."
			);

			return;
		}

		RunWarmup( preset );

		var timer = new FastTimer();
		timer.Start();

		var successCount = 0;

		for ( var i = 0; i < Iterations; i++ )
		{
			if ( PresetAPI.TryApply( preset, Target ) )
				successCount++;
		}

		var elapsed = timer.ElapsedMilliSeconds;

		Log.Info(
			$"""
			Preset Benchmark
			Preset: {preset.PresetName}
			Type: {preset.PresetType}
			Target: {Target.GetType().Name}
			Iterations: {Iterations:N0}
			Successful: {successCount:N0}

			Total: {elapsed:0.###} ms
			Average: {ToMicroseconds( elapsed ):0.###} µs/apply
			"""
		);
	}

	[Button( "Run Detailed Benchmark" )]
	public void RunDetailed()
	{
		if ( !TryGetBenchmarkPreset( out var preset ) )
			return;

		if ( !TryPrepare( preset, out var targetInfo, out var mode, out var payload, out var properties ) )
			return;

		WarmupDetailed( preset, targetInfo, mode, payload, properties );

		var registry = Measure( "Registry lookup", () =>
		{
			var info = PresetRegistry.Find( preset.PresetType, Target.GetType() );

			if ( info is not null )
				_sink++;
		} );

		var parse = Measure( "Payload parse", () =>
		{
			var parsed = Json.ParseToJsonObject( preset.PayloadJson );
			_sink += parsed?.Count ?? 0;
		} );

		var discovery = Measure( "Property discovery", () =>
		{
			var count = 0;

			foreach ( var property in PresetAPI.GetPresetProperties( targetInfo, mode ) )
				count++;

			_sink += count;
		} );

		var conversion = Measure( "JSON conversion", () =>
		{
			foreach ( var property in properties )
			{
				if ( !payload.TryGetPropertyValue( property.Name, out var node ) )
					continue;

				var value = node is null ? null : Json.FromNode( node, property.PropertyType );

				if ( value is not null )
					_sink++;
			}
		} );

		var compiledValuePlan = PresetAPI.BuildCompiledApplyPlan( preset, Target );
		var compiledValues = Measure( "Compiled values", () =>
		{
			if ( PresetAPI.TryApplyCompiledPlan( compiledValuePlan, Target ) )
				_sink++;
		} );

		var planBuild = Measure( "Plan build", () =>
		{
			var plan = PresetAPI.BuildCompiledApplyPlan( preset, Target );

			if ( plan is not null )
				_sink += plan.Bindings.Length;
		} );


		// We pre-convert values so SetValue itself can be isolated.
		var bindings = BuildBindings( payload, properties );

		var setValue = Measure( "Property SetValue", () =>
		{
			foreach ( var binding in bindings )
				binding.Property.SetValue( Target, binding.Value );
		} );

		var convertAndSet = Measure( "Convert + SetValue", () =>
		{
			foreach ( var property in properties )
			{
				if ( !payload.TryGetPropertyValue( property.Name, out var node ) )
					continue;

				var value = node is null ? null : Json.FromNode( node, property.PropertyType );
				property.SetValue( Target, value );
			}
		} );

		PresetAPI.InvalidateCompiledPlan( preset );
		PresetAPI.TryApply( preset, Target );

		var cachedApply = Measure( "Cached TryApply", () =>
		{
			if ( PresetAPI.TryApply( preset, Target ) )
				_sink++;
		} );

		PrintDetailedResults( preset, properties.Length, registry, parse, discovery, planBuild, compiledValues,
			conversion, setValue, convertAndSet, cachedApply );
	}

	[Button( "Run Batched Benchmark" )]
	public void RunBatched()
	{
		if ( !TryGetBenchmarkPreset( out var preset ) )
			return;

		if ( !PresetAPI.CanApply( preset, Target ) )
			return;

		RunWarmup( preset );

		var minimum = double.MaxValue;
		var maximum = double.MinValue;
		var total = 0.0;

		for ( var batch = 0; batch < Batches; batch++ )
		{
			var timer = new FastTimer();
			timer.Start();

			for ( var i = 0; i < Iterations; i++ )
				PresetAPI.TryApply( preset, Target );

			var elapsed = timer.ElapsedMilliSeconds;

			minimum = Math.Min( minimum, elapsed );
			maximum = Math.Max( maximum, elapsed );
			total += elapsed;
		}

		var averageBatch = total / Batches;

		Log.Info(
			$"""
			Preset Benchmark [Batched]
			Preset: {preset.PresetName}
			Type: {preset.PresetType}
			Target: {Target.GetType().Name}

			Batches: {Batches}
			Iterations/Batch: {Iterations:N0}

			Minimum Batch: {minimum:0.###} ms
			Average Batch: {averageBatch:0.###} ms
			Maximum Batch: {maximum:0.###} ms

			Average Apply: {ToMicroseconds( averageBatch ):0.###} µs
			"""
		);
	}

	private BenchmarkResult Measure( string name, Action action )
	{
		var minimum = double.MaxValue;
		var maximum = double.MinValue;
		var total = 0.0;

		for ( var batch = 0; batch < Batches; batch++ )
		{
			var timer = new FastTimer();
			timer.Start();

			for ( var i = 0; i < Iterations; i++ )
				action();

			var elapsed = timer.ElapsedMilliSeconds;

			minimum = Math.Min( minimum, elapsed );
			maximum = Math.Max( maximum, elapsed );
			total += elapsed;
		}

		var average = total / Batches;

		return new BenchmarkResult( name, minimum, average, maximum, ToMicroseconds( average ) );
	}

	private double ToMicroseconds( double milliseconds )
	{
		return milliseconds * 1000.0 / Iterations;
	}

	private void PrintDetailedResults( GenericPresetResource preset, int propertyCount, params BenchmarkResult[] results )
	{
		var fullApply = results.First( x => x.Name == "Cached TryApply" );

		var lines = new List<string>
		{
			"Preset Benchmark [Detailed]",
			$"Preset: {preset.PresetName}",
			$"Type: {preset.PresetType}",
			$"Target: {Target.GetType().Name}",
			$"Properties: {propertyCount}",
			$"Batches: {Batches}",
			$"Iterations/Batch: {Iterations:N0}",
			string.Empty,
			"Stage                     Avg µs    % Cached      Min ms      Max ms",
			"---------------------------------------------------------------------"
		};

		foreach ( var result in results )
		{
			var percentage = fullApply.Microseconds > 0.0
				? result.Microseconds / fullApply.Microseconds * 100.0 : 0.0;

			lines.Add(
				$"{result.Name,-24}" +
				$"{result.Microseconds,8:0.###}   " +
				$"{percentage,7:0.0}%   " +
				$"{result.MinimumMilliseconds,9:0.###}   " +
				$"{result.MaximumMilliseconds,9:0.###}"
			);
		}

		lines.Add( string.Empty );
		lines.Add( $"Cached apply: {fullApply.Microseconds:0.###} µs" );

		Log.Info( string.Join( "\n", lines ) );
	}

	private void RunWarmup( GenericPresetResource preset )
	{
		for ( var i = 0; i < WarmupIterations; i++ )
			PresetAPI.TryApply( preset, Target );
	}

	private void WarmupDetailed( GenericPresetResource preset, PresetTargetInfo targetInfo, PresetModeInfo mode,
		JsonObject payload, PropertyDescription[] properties )
	{
		var warmup = Math.Max( 100, WarmupIterations );

		for ( var i = 0; i < warmup; i++ )
		{
			_ = PresetRegistry.Find( preset.PresetType, Target.GetType() );

			_ = Json.ParseToJsonObject( preset.PayloadJson );

			foreach ( var property in PresetAPI.GetPresetProperties( targetInfo, mode ) )
			{
				if ( !payload.TryGetPropertyValue( property.Name, out var node ) )
					continue;

				var value = node is null ? null : Json.FromNode( node, property.PropertyType );

				property.SetValue( Target, value );
			}
		}
	}

	private bool TryPrepare( GenericPresetResource preset, out PresetTargetInfo targetInfo,
		out PresetModeInfo mode, out JsonObject payload, out PropertyDescription[] properties )
	{
		targetInfo = null;
		mode = null;
		payload = null;
		properties = [];

		if ( preset is null || Target is null )
			return false;

		targetInfo = PresetRegistry.Find( preset.PresetType, Target.GetType() );

		if ( targetInfo is null )
		{
			Log.Warning( "[Preset Benchmark] Unable to resolve preset target." );

			return false;
		}

		mode = targetInfo.FindMode( PresetModes.Normalize( preset.PresetMode ) );

		if ( mode is null )
		{
			Log.Warning( "[Preset Benchmark] Unable to resolve preset mode." );

			return false;
		}

		try
		{
			payload = Json.ParseToJsonObject( preset.PayloadJson );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Preset Benchmark] Unable to parse payload: {e.Message}" );

			return false;
		}

		if ( payload is null )
			return false;

		properties = [.. PresetAPI.GetPresetProperties( targetInfo, mode )];

		return true;
	}

	private static List<PresetBinding> BuildBindings( JsonObject payload, PropertyDescription[] properties )
	{
		var result = new List<PresetBinding>( properties.Length );

		foreach ( var property in properties )
		{
			if ( !payload.TryGetPropertyValue( property.Name, out var node ) )
				continue;

			var value = node is null ? null : Json.FromNode( node, property.PropertyType );

			result.Add( new PresetBinding( property, value ) );
		}

		return result;
	}

	private bool TryGetBenchmarkPreset( out GenericPresetResource preset )
	{
		preset = null;

		if ( Target is null )
		{
			Log.Warning( "[Preset Benchmark] No target assigned." );

			return false;
		}

		var type = Game.TypeLibrary.GetType( Target.GetType() );

		if ( type is null )
		{
			Log.Warning(
				$"[Preset Benchmark] Unable to resolve type information for " +
				$"'{Target.GetType().Name}'."
			);

			return false;
		}

		var presetProperty = type.Properties.FirstOrDefault( x => x.PropertyType == typeof( GenericPresetResource )
			&& x.CanRead );

		if ( presetProperty is null )
		{
			Log.Warning(
				$"[Preset Benchmark] '{Target.GetType().Name}' does not expose " +
				$"a GenericPresetResource property."
			);

			return false;
		}

		preset = presetProperty.GetValue( Target ) as GenericPresetResource;

		if ( preset is null )
		{
			Log.Warning( $"[Preset Benchmark] '{Target.GetType().Name}' has no preset assigned." );

			return false;
		}

		return true;
	}

}
