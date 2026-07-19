namespace Core.AI;

public class WorldState
{
	public List<WorldFact> facts = [];

	private readonly Dictionary<string, bool> _facts = new();

	/// <summary>
	/// Set or update a world fact.
	/// </summary>
	public void Set( string key, bool value )
	{
		_facts[key] = value;
		SyncFacts();
	}

	/// <summary>
	/// Get a fact value. Missing facts default to false.
	/// </summary>
	public bool Get( string key )
	{
		return _facts.TryGetValue( key, out var value ) && value;
	}

	/// <summary>
	/// Try to get a fact value. Returns false if the key does not exist.
	/// </summary>
	public bool TryGet( string key, out bool value )
	{
		return _facts.TryGetValue( key, out value );
	}

	/// <summary>
	/// True if the fact exists (regardless of value).
	/// </summary>
	public bool Has( string key ) => _facts.ContainsKey( key );

	/// <summary>
	/// Check if this world state satisfies a goal.
	/// </summary>
	public bool Satisfies( List<WorldFact> goal )
	{
		foreach ( var req in goal )
		{
			if ( !_facts.TryGetValue( req.Name, out var val ) || val != req.Value )
				return false;
		}
		return true;
	}

	private void SyncFacts()
	{
		facts.Clear();
		foreach ( var kvp in _facts )
			facts.Add( new WorldFact( kvp.Key, kvp.Value ) );
	}
}
