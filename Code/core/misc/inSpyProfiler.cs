using Sandbox;
using System;
using System.Collections.Generic;
using System.Diagnostics;

/// <summary>
/// aiSpy profiler core.
/// This class handles the data and structures used by the spy profiler.
/// It serves as a lightweight and easy to set up live debugger.
/// TODO: scratchpad for blackboards
/// TODO: per agent profiling
/// TODO: 
/// </summary>

public static class iSpy
{

	public class SectionData
	{
		public string Name;
		public double Peak;
		public double Last;
		public int Count;
		public float[] History = new float[128];
		public int HistoryIndex = 0;
		public int HistoryCount = 0;
		private readonly Stopwatch _sw = new();

		public void Begin() => _sw.Restart();

		public void End()
		{
			_sw.Stop();
			double ms = _sw.Elapsed.TotalMilliseconds;
			Last = ms;
			if ( ms > Peak ) Peak = ms;
			Count++;
			History[HistoryIndex] = (float)ms;
			HistoryIndex = (HistoryIndex + 1) % 128;
			if ( HistoryCount < 128 ) HistoryCount++;
		}

	}

	static readonly Dictionary<string, SectionData> _sections = new();
	static readonly Stack<string> _activeStack = new();

	public static IReadOnlyDictionary<string, SectionData> Sections => _sections;

	public static void iSpyStartProfile( string name )
	{
		if ( !_sections.TryGetValue( name, out var sec ) )
		{
			sec = new SectionData { Name = name };
			_sections[name] = sec;

		}
		_activeStack.Push( name );
		sec.Begin();
	}

	public static void iSpyEndProfile()
	{
		if ( _activeStack.Count == 0 ) return;

		var name = _activeStack.Pop();
		_sections[name].End();
	}

	public static void Reset()
	{
		_sections.Clear();
		_activeStack.Clear();
	}
}
