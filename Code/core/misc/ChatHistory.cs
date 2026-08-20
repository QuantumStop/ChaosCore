#if IGNIS
namespace Core;

using System;
using System.Collections;
using System.Collections.Generic;
using Sandbox.Diagnostics;

public readonly record struct ChatEntry(
	int Id,
	string Name,
	string Message,
	long SteamId,
	string ExtraClass,
	string Channel,
	float CreatedAt
);

public sealed class ChatHistory : IReadOnlyList<ChatEntry>
{
	public static Logger GameChatLog { get; } = new Logger( "Game Chat" );
	public static Logger SystemChatLog { get; } = new Logger( "System Chat" );

	const int _defaultMaxEntries = 4096;
	const float _duplicateWindow = 0.25f;

	ChatEntry[] _entries = new ChatEntry[_defaultMaxEntries];

	int _start;
	int _count;
	int _nextId;
	int _maxEntries = _defaultMaxEntries;

	public IReadOnlyList<ChatEntry> Entries => this;
	public int Version { get; private set; }
	public int Count => _count;

	public ChatEntry this[int index]
	{
		get
		{
			if ( index < 0 || index >= _count )
				throw new ArgumentOutOfRangeException( nameof( index ) );

			return _entries[(_start + index) % _entries.Length];
		}
	}

	public int MaxEntries
	{
		get => _maxEntries;
		set
		{
			var maxEntries = Math.Max( 1, value );

			if ( _maxEntries == maxEntries )
				return;

			Resize( maxEntries );
		}
	}

	public event Action Changed;

	public ChatEntry AddMessage( string name, string message, long steamId = 0, string channel = "", string extraClass = "", float? createdAt = null )
	{
		if ( string.IsNullOrWhiteSpace( message ) )
			return default;

		var entryCreatedAt = createdAt ?? GameManagerSystem.WorldNow;
		if ( ContainsRecentDuplicate( name, message, steamId, channel, extraClass, entryCreatedAt ) )
			return default;

		var entry = new ChatEntry(
			_nextId++,
			name ?? "",
			message,
			steamId,
			extraClass ?? "",
			channel ?? "",
			entryCreatedAt
		);

		AddEntry( entry );
		return entry;
	}

	public void AddEntry( ChatEntry entry )
	{
		if ( ContainsEntry( entry.Id ) )
			return;

		if ( _count == _entries.Length )
		{
			_entries[_start] = entry;
			_start = (_start + 1) % _entries.Length;
		}
		else
		{
			_entries[(_start + _count) % _entries.Length] = entry;
			_count++;
		}

		_nextId = Math.Max( _nextId, entry.Id + 1 );
		Version++;
		LogEntry( entry );
		Changed?.Invoke();
	}

	public void Clear()
	{
		if ( _count == 0 )
			return;

		Array.Clear( _entries );
		_start = 0;
		_count = 0;

		Version++;
		Changed?.Invoke();
	}

	public static float GetAge( ChatEntry entry )
	{
		return MathF.Max( 0f, GameManagerSystem.WorldNow - entry.CreatedAt );
	}

	static void LogEntry( ChatEntry entry )
	{
		var isSystem = IsSystemEntry( entry );
		var logger = isSystem ? SystemChatLog : GameChatLog;

		if ( isSystem || string.IsNullOrWhiteSpace( entry.Name ) )
		{
			logger.Info( entry.Message );
			return;
		}

		logger.Info( $"{entry.Name}: {entry.Message}" );
	}

	static bool IsSystemEntry( ChatEntry entry )
	{
		return string.Equals( entry.ExtraClass, "system", StringComparison.OrdinalIgnoreCase );
	}

	bool ContainsEntry( int id )
	{
		for ( int i = 0; i < _count; i++ )
		{
			if ( this[i].Id == id )
				return true;
		}

		return false;
	}

	bool ContainsRecentDuplicate( string name, string message, long steamId, string channel, string extraClass, float createdAt )
	{
		for ( int i = _count - 1; i >= 0; i-- )
		{
			var entry = this[i];
			if ( createdAt - entry.CreatedAt > _duplicateWindow )
				return false;

			if ( entry.SteamId != steamId )
				continue;

			if ( entry.Name != (name ?? "") )
				continue;

			if ( entry.Message != message )
				continue;

			if ( entry.Channel != (channel ?? "") )
				continue;

			if ( entry.ExtraClass != (extraClass ?? "") )
				continue;

			return true;
		}

		return false;
	}

	void Resize( int maxEntries )
	{
		var oldCount = _count;
		var newCount = Math.Min( _count, maxEntries );
		var newEntries = new ChatEntry[maxEntries];
		var sourceStart = _count - newCount;

		for ( int i = 0; i < newCount; i++ )
			newEntries[i] = this[sourceStart + i];

		_entries = newEntries;
		_start = 0;
		_count = newCount;
		_maxEntries = maxEntries;

		if ( _count != oldCount )
		{
			Version++;
			Changed?.Invoke();
		}
	}

	public IEnumerator<ChatEntry> GetEnumerator()
	{
		for ( int i = 0; i < _count; i++ )
			yield return this[i];
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
#endif
