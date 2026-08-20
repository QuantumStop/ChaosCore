using System;

[Dock( "Editor", "iSpy Profiler", "troubleshoot" )]
public class iSpyProfilerWidget : Widget
{

	private readonly Color[] _palette = {
		new Color( 0.22f, 0.54f, 0.87f ),
		new Color( 0.11f, 0.62f, 0.46f ),
		new Color( 0.85f, 0.35f, 0.19f ),
		new Color( 0.73f, 0.46f, 0.09f ),
		new Color( 0.83f, 0.33f, 0.49f ),
		new Color( 0.50f, 0.47f, 0.87f ),
		new Color( 0.89f, 0.30f, 0.29f ),
		new Color( 0.39f, 0.60f, 0.13f ),
	};

	private readonly Dictionary<string, Color> _colors = new();
	private int _colorIdx = 0;

	private Widget _graphWidget;
	private Widget _tableWidget;
	private const int GraphWidth = 120;

	public iSpyProfilerWidget( Widget parent ) : base( parent )
	{
		WindowTitle = "iSpy Profiler";
		MinimumSize = new Vector2( 420, 320 );

		Layout = Layout.Column();
		Layout.Margin = 0;
		Layout.Spacing = 0;

		BuildToolbar();

		_graphWidget = new Widget( this );
		_graphWidget.MinimumHeight = 200;
		_graphWidget.OnPaintOverride = PaintGraph;
		Layout.Add( _graphWidget, 1 );

		var sep = new Widget( this );
		sep.MinimumHeight = 1;
		sep.MaximumHeight = 1;
		Layout.Add( sep );

		_tableWidget = new Widget( this );
		_tableWidget.MinimumHeight = 130;
		_tableWidget.OnPaintOverride = PaintTable;
		Layout.Add( _tableWidget );

	}

	void BuildToolbar()
	{
		var bar = new Widget( this );
		bar.Layout = Layout.Row();
		bar.Layout.Margin = 4;
		bar.Layout.Spacing = 4;
		bar.MinimumHeight = 32;
		bar.MaximumHeight = 32;


		var clearBtn = new Button( "Clear", "delete", bar );
		clearBtn.Clicked += OnClear;
		bar.Layout.Add( clearBtn );

		bar.Layout.AddStretchCell();

		Layout.Add( bar );

		var sepLine = new Widget( this );
		sepLine.MinimumHeight = 1;
		sepLine.MaximumHeight = 1;
		Layout.Add( sepLine );
	}

	void OnClear()
	{
		iSpy.Reset();
		_colors.Clear();
		_colorIdx = 0;
		_graphWidget.Update();
		_tableWidget.Update();
	}

	float lastFrameTime = 0.00f;
	[EditorEvent.Frame]
	private void Frame()
	{
		if ( !IsValid ) return;
		if ( !Visible ) return;


		// expose timing somehow?
		//Log.Info("iSpy::Frame() start");
		var sw = System.Diagnostics.Stopwatch.StartNew();

		_cachedSections = GetSections();
		_graphWidget.Update();
		_tableWidget.Update();

		sw.Stop();
		lastFrameTime = (float)sw.Elapsed.TotalMilliseconds;
		//Log.Info( $"iSpy::Frame() took {lastFrameTime}ms" );

	}
	Color GetColor( string name )
	{
		if ( !_colors.TryGetValue( name, out var c ) )
		{
			c = _palette[_colorIdx % _palette.Length]; // why did i struggle with this
			_colors[name] = c;
			_colorIdx++;
		}
		return c;
	}

	bool PaintGraph()
	{
		var rect = _graphWidget.LocalRect;

		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( rect );

		if ( !Game.IsPlaying )
		{
			Paint.SetDefaultFont( 9 );
			Paint.SetPen( Theme.TextControl.WithAlpha( 0.4f ) );
			Paint.DrawText( rect, "Game must be running to profile.", TextFlag.Center );
			return true;
		}

		var sections = _cachedSections;
		if ( sections is null || sections.Count == 0 )
		{
			Paint.SetDefaultFont( 9 );
			Paint.SetPen( Theme.TextControl.WithAlpha( 0.4f ) );
			Paint.DrawText( rect, "Wrap code with iSpyStartProfile/iSpyEndProfile", TextFlag.Center );
			return true;
		}

		float visibleMax = 0f;
		foreach ( var s in sections )
		{
			var h = GetHistory( s.Key, s.Value );
			foreach ( var v in h )
				if ( v > visibleMax ) visibleMax = v;
		}

		float maxMs;
		if ( visibleMax <= 5 ) maxMs = 5;
		else if ( visibleMax <= 10 ) maxMs = 10;
		else if ( visibleMax <= 20 ) maxMs = 20;
		else if ( visibleMax <= 50 ) maxMs = 50;
		else maxMs = MathF.Ceiling( visibleMax / 10f ) * 10f;

		float leftPad = 54f;
		float topPad = 12f;
		float botPad = 8f;

		var graphRect = new Rect( rect.Left + leftPad, rect.Top + topPad, rect.Width - leftPad, rect.Height - topPad - botPad );

		float[] test = { 0.5f, 1f, 2f, 5f, 10f, 20f, 50f }; // this should be the limits of our y units...
		float step = test.FirstOrDefault( s => maxMs / s <= 6, 10f );

		Paint.SetDefaultFont( 7 );
		for ( float ms = 0; ms <= maxMs + step * 0.01f; ms += step )
		{
			float y = graphRect.Bottom - (ms / maxMs) * graphRect.Height;
			Paint.SetPen( Theme.TextControl.WithAlpha( 0.08f ), 1f );
			Paint.DrawLine( new Vector2( graphRect.Left, y ), new Vector2( graphRect.Right, y ) );
			Paint.SetPen( Theme.TextControl.WithAlpha( 0.35f ) );
			Paint.DrawText( new Rect( rect.Left + 4, y - 10, 50, 16 ), $"{ms:F0}ms", TextFlag.LeftCenter );
		}

		foreach ( var kv in sections )
		{
			var samples = GetHistory( kv.Key, kv.Value );
			if ( samples.Length < 2 ) continue;

			var col = GetColor( kv.Key );
			Paint.SetPen( col, 1.5f );

			Vector2? prev = null;
			for ( int i = 0; i < samples.Length; i++ )
			{
				float x = graphRect.Left + i * (graphRect.Width / (float)(GraphWidth - 1));
				float y = graphRect.Bottom - (samples[i] / maxMs) * graphRect.Height;
				y = Math.Clamp( y, graphRect.Top + 2, graphRect.Bottom - 2 );

				var pt = new Vector2( x, y );
				if ( prev.HasValue ) Paint.DrawLine( prev.Value, pt );
				prev = pt;
			}
		}

		// another legend attempt
		Paint.SetDefaultFont( 7 );
		float lx = graphRect.Left + 6;
		float ly = rect.Top + 4;
		foreach ( var kv in sections )
		{
			var col = GetColor( kv.Key );
			double avg = GetAverage( kv.Key, kv.Value );
			string label = $"{kv.Key}  {avg:F2}ms";
			float lw = Paint.MeasureText( new Rect( 0, 0, 300, 20 ), label, TextFlag.LeftCenter | TextFlag.WordWrap ).Width + 20;

			Paint.ClearPen();
			Paint.SetBrush( col );
			Paint.DrawRect( new Rect( lx, ly + 5, 9, 9 ) );

			Paint.SetPen( Theme.TextControl );
			Paint.DrawText( new Rect( lx + 13, ly, lw, 18 ), label, TextFlag.LeftCenter | TextFlag.WordWrap );
			lx += lw + 4;
		}

		return true;
	}
	bool PaintTable()
	{
		var rect = _tableWidget.LocalRect;

		Paint.ClearPen();
		Paint.SetBrush( Theme.WindowBackground );
		Paint.DrawRect( rect );

		var sections = _cachedSections;
		if ( sections is null || sections.Count == 0 ) return true;

		Paint.SetDefaultFont( 7 );

		float rowH = 22f;
		float col0 = 160f, col1 = 68f, col2 = 68f, col3 = 68f;
		float col4 = rect.Width - col0 - col1 - col2 - col3;

		Paint.ClearPen();
		Paint.SetBrush( Theme.ControlBackground );
		Paint.DrawRect( new Rect( rect.Left, rect.Top, rect.Width, rowH ) );

		Paint.SetPen( Theme.TextControl.WithAlpha( 0.5f ) );
		float hx = rect.Left + 8;
		foreach ( var (h, w) in new[] {
		("section", col0), ("avg ms", col1), ("peak ms", col2), ("last ms", col3), ("% frame", col4)
	} )
		{
			Paint.DrawText( new Rect( hx, rect.Top, w, rowH ), h, TextFlag.LeftCenter );
			hx += w;
		}

		Paint.SetPen( Theme.TextControl.WithAlpha( 0.1f ), 1f );
		Paint.DrawLine( new Vector2( rect.Left, rect.Top + rowH ), new Vector2( rect.Right, rect.Top + rowH ) );

		double totalLast = sections.Values.Sum( s => GetField( s, "Last" ) );

		int row = 0;
		foreach ( var kv in sections )
		{
			float ry = rect.Top + rowH * (row + 1);
			if ( ry + rowH > rect.Bottom ) break;

			if ( row % 2 == 0 )
			{
				Paint.ClearPen();
				Paint.SetBrush( Theme.ControlBackground.WithAlpha( 0.3f ) );
				Paint.DrawRect( new Rect( rect.Left, ry, rect.Width, rowH ) );
			}

			var col = GetColor( kv.Key );
			Paint.ClearPen();
			Paint.SetBrush( col );
			Paint.DrawRect( new Rect( rect.Left + 6, ry + 7, 8, 8 ) );

			double avg = GetAverage( kv.Key, kv.Value );
			double peak = GetField( kv.Value, "Peak" );
			double last = GetField( kv.Value, "Last" );
			double pct = totalLast > 0 ? last / totalLast * 100.0 : 0;

			float rx = rect.Left + 8;
			void DrawCell( string text, float w, bool warn = false )
			{
				Paint.SetPen( warn ? Color.Parse( "#E24B4A" ).Value : Theme.TextControl );
				Paint.DrawText( new Rect( rx, ry, w, rowH ), text, TextFlag.LeftCenter );
				rx += w;
			}

			DrawCell( $"   {kv.Key}", col0 );
			DrawCell( $"{avg:F2}", col1 );
			DrawCell( $"{peak:F2}", col2, peak > 16.0 );
			DrawCell( $"{last:F2}", col3, last > 16.0 );
			DrawCell( $"{pct:F1}%", col4 );

			row++;
		}

		return true;
	}

	private double GetAverage( string key, object sectionData )
	{
		var history = GetHistory( key, sectionData );
		return history.Length == 0 ? 0 : history.Average();
	}


	private IReadOnlyDictionary<string, object> _cachedSections;

	private Type spyType;

	// this is my solution for hot loading not destroying the graph. is it good? i dont know.
	private bool GetIgnisSpyType()
	{
		// invalidate if the assembly was replaced by a hotload
		if ( spyType is not null )
		{
			var stillLoaded = AppDomain.CurrentDomain.GetAssemblies()
				.Any( a => a == spyType.Assembly );
			if ( !stillLoaded ) spyType = null;
		}

		if ( spyType is null )
		{
			foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies()
				.Where( a => a.GetName().Name.StartsWith( "package.local." ) ) )
			{
				spyType = assembly.GetType( "iSpy" );
				if ( spyType is not null ) break;
			}
		}

		return spyType is not null;
	}
	Dictionary<string, object> iSpyEntries = new Dictionary<string, object>();
	// thank you c-sharpcorner.com
	private IReadOnlyDictionary<string, object> GetSections()
	{

		if ( !GetIgnisSpyType() ) return null;

		var prop = spyType.GetProperty( "Sections" );
		var raw = prop?.GetValue( null ) as System.Collections.IDictionary;
		if ( raw is null || raw.Count == 0 ) return null;

		var result = iSpyEntries;
		result.Clear();
		foreach ( System.Collections.DictionaryEntry entry in raw )
			result[(string)entry.Key] = entry.Value;
		return result;
	}

	private double GetField( object sectionData, string name )
	{
		return (double)sectionData.GetType().GetField( name ).GetValue( sectionData );
	}

	private readonly Dictionary<string, float[]> _historyCache = new();

	private float[] GetHistory( string key, object sectionData )
	{
		var t = sectionData.GetType();
		int count = (int)t.GetField( "HistoryCount" ).GetValue( sectionData );
		int idx = (int)t.GetField( "HistoryIndex" ).GetValue( sectionData );
		var raw = (float[])t.GetField( "History" ).GetValue( sectionData );

		if ( !_historyCache.TryGetValue( key, out var result ) || result.Length != count )
		{
			result = new float[count];
			_historyCache[key] = result;
		}

		int start = count < 128 ? 0 : idx;
		for ( int i = 0; i < count; i++ )
			result[i] = raw[(start + i) % 128];
		return result;
	}
}
