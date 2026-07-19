namespace Core;

using System;
using System.Text.Json.Nodes;

public sealed partial class SaveSystem
{
	private static float? _stagedWorldTimeForSceneLoad;

	public static bool TryGetStagedWorldTime( out float worldTime )
	{
		if ( _stagedWorldTimeForSceneLoad.HasValue )
		{
			worldTime = _stagedWorldTimeForSceneLoad.Value;
			return true;
		}

		worldTime = default;
		return false;
	}

	private static void StageWorldTimeForSceneLoad( JsonObject data )
	{
		_stagedWorldTimeForSceneLoad = null;

		if ( data?["WorldTime"] is JsonValue worldTimeNode &&
			 worldTimeNode.TryGetValue<float>( out var worldTime ) )
			_stagedWorldTimeForSceneLoad = worldTime;
	}

	private void RestoreWorldTime( JsonObject data )
	{
		if ( data?["WorldTime"] is not JsonValue worldTimeNode ||
			 !worldTimeNode.TryGetValue<float>( out var worldTime ) )
			return;

		GameManagerSystem.Current?.SetWorldTime( worldTime );
		_stagedWorldTimeForSceneLoad = null;
	}

	private static void AdjustSaveDataToCurrentTime( JsonObject data )
	{
		if ( data is null )
			return;

		if ( data["WorldTime"] is not null )
			return;

		if ( data["SavedTimeNow"] is not JsonValue savedTimeNode ||
			 !savedTimeNode.TryGetValue<float>( out var savedTimeNow ) )
			return;

		var delta = Time.Now - savedTimeNow;
		if ( MathF.Abs( delta ) < 0.0001f )
			return;

		AdjustAbsoluteTimes( data["Patch"], delta );
		AdjustAbsoluteTimes( data["PrefabSnapshots"], delta );
		AdjustAbsoluteTimes( data["SavedRoots"], delta );
		AdjustAbsoluteTimes( data["RuntimeObjectState"], delta );
		AdjustAbsoluteTimes( data["CustomComponentData"], delta );
	}

	private static void AdjustAbsoluteTimes( JsonNode node, float delta, string key = null )
	{
		switch ( node )
		{
			case JsonObject obj:
				foreach ( var prop in obj.ToArray() )
					AdjustAbsoluteTimes( prop.Value, delta, prop.Key );
				break;

			case JsonArray array:
				foreach ( var child in array )
					AdjustAbsoluteTimes( child, delta, key );
				break;

			case JsonValue value when IsAbsoluteTimeKey( key ) && value.TryGetValue<float>( out var time ):
				node.ReplaceWith( JsonValue.Create( time + delta ) );
				break;
		}
	}

	private static bool IsAbsoluteTimeKey( string key )
	{
		if ( string.IsNullOrWhiteSpace( key ) )
			return false;

		if ( key.Contains( "TimeSince", StringComparison.OrdinalIgnoreCase ) ||
			 key.Contains( "TimeUntil", StringComparison.OrdinalIgnoreCase ) ||
			 key.Contains( "Duration", StringComparison.OrdinalIgnoreCase ) ||
			 key.Contains( "Delay", StringComparison.OrdinalIgnoreCase ) ||
			 key.Contains( "Interval", StringComparison.OrdinalIgnoreCase ) ||
			 key.Contains( "Rate", StringComparison.OrdinalIgnoreCase ) ||
			 key.Contains( "Scale", StringComparison.OrdinalIgnoreCase ) ||
			 key.Contains( "FadeTime", StringComparison.OrdinalIgnoreCase ) ||
			 key.Contains( "Seconds", StringComparison.OrdinalIgnoreCase ) )
			return false;

		return key.EndsWith( "Time", StringComparison.OrdinalIgnoreCase ) ||
			   key.EndsWith( "TimeNow", StringComparison.OrdinalIgnoreCase ) ||
			   key.Contains( "StartTime", StringComparison.OrdinalIgnoreCase ) ||
			   key.Contains( "EndTime", StringComparison.OrdinalIgnoreCase ) ||
			   key.Contains( "Next", StringComparison.OrdinalIgnoreCase ) && key.Contains( "Time", StringComparison.OrdinalIgnoreCase ) ||
			   key.Contains( "Last", StringComparison.OrdinalIgnoreCase ) && key.Contains( "Time", StringComparison.OrdinalIgnoreCase );
	}
}
