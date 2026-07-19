namespace Core.AI;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// thank you jeff orkin
/// </summary>
public sealed class AIPlanner
{

	public readonly struct WorldState : IEquatable<WorldState>
	{
		private readonly Dictionary<string, object> _facts;

		public WorldState( IEnumerable<WorldFact> facts )
		{
			_facts = new Dictionary<string, object>();
			foreach ( var f in facts )
				_facts[f.Name] = f.Value;
		}

		private WorldState( Dictionary<string, object> facts ) => _facts = facts;

		/// <summary>
		/// Returns a world state with the given effects applied
		/// </summary>
		/// <param name="effects"></param>
		/// <returns></returns>
		public WorldState WithEffects( IReadOnlyList<WorldFact> effects )
		{
			// Only copy if something actually changes
			Dictionary<string, object> next = null;

			foreach ( var eff in effects )
			{
				if ( _facts.TryGetValue( eff.Name, out var current ) &&
					 Equals( current, eff.Value ) )
					continue; // no change needed

				next ??= new Dictionary<string, object>( _facts );
				next[eff.Name] = eff.Value;
			}

			return next is null ? this : new WorldState( next );
		}


		public bool Satisfies( IReadOnlyList<WorldFact> required )
		{
			foreach ( var req in required )
			{
				if ( !_facts.TryGetValue( req.Name, out var val ) ||
					 !Equals( val, req.Value ) )
					return false;
			}
			return true;
		}

		public int CountUnsatisfied( IReadOnlyList<WorldFact> goal )
		{
			int missing = 0;
			foreach ( var g in goal )
			{
				if ( !_facts.TryGetValue( g.Name, out var val ) ||
					 !Equals( val, g.Value ) )
					missing++;
			}
			return missing;
		}

		public bool Equals( WorldState other )
		{
			if ( _facts.Count != other._facts.Count ) return false;
			foreach ( var kvp in _facts )
			{
				if ( !other._facts.TryGetValue( kvp.Key, out var v ) ||
					 !Equals( kvp.Value, v ) )
					return false;
			}
			return true;
		}

		public override bool Equals( object obj ) =>
			obj is WorldState ws && Equals( ws );

		public override int GetHashCode()
		{
			unchecked
			{
				int h = 17;
				// Sort keys so order doesn't affect hash..
				foreach ( var key in _facts.Keys.OrderBy( k => k ) )
				{
					h = h * 31 + key.GetHashCode();
					h = h * 31 + (_facts[key]?.GetHashCode() ?? 0);
				}
				return h;
			}
		}
	}

	private sealed class Node
	{
		public Node Parent;
		public float G;
		public int H;
		public float F => G + H;
		public WorldState State;
		public AIAction Action;

		// tie break on G descending (prefer nodes closer to goal)
		public float Priority => F * 1000f - G;
	}

	private readonly Stack<Node> _nodePool = new( 64 );

	private Node Rent() => _nodePool.Count > 0 ? _nodePool.Pop() : new Node(); // this should work for now
	private void Return( Node n )
	{
		n.Parent = null;
		n.Action = null;
		_nodePool.Push( n );
	}

	/// <summary>
	/// Returns the cheapest action sequence that transforms
	/// <paramref name="currentState"/> into a state satisfying
	/// <paramref name="goal"/>, or null if no plan exists.
	/// </summary>
	public List<AIAction> Plan(
		AIController agent,
		IReadOnlyList<WorldFact> currentState,
		IReadOnlyList<AIAction> actions,
		IReadOnlyList<WorldFact> goal )
	{
		// Filter usable actions once up-front
		var usable = new List<AIAction>( actions.Count );
		foreach ( var a in actions )
			if ( a.CheckProceduralPrecondition( agent ) )
				usable.Add( a );

		if ( usable.Count == 0 ) return null;

		var startState = new WorldState( currentState );

		var open = new PriorityQueue<Node, float>( 64 );
		var closed = new HashSet<WorldState>();

		// track all active nodes sowe can return them after planning
		var allNodes = new List<Node>( 64 );

		var startNode = Rent();
		startNode.Parent = null;
		startNode.G = 0f;
		startNode.H = startState.CountUnsatisfied( goal );
		startNode.State = startState;
		startNode.Action = null;
		allNodes.Add( startNode );

		open.Enqueue( startNode, startNode.Priority );

		List<AIAction> result = null;

		while ( open.Count > 0 )
		{
			var current = open.Dequeue();



			if ( !closed.Add( current.State ) )
				continue; // already processed an equal or better state

			if ( current.H == 0 ) // goal satisfied! woohoo!
			{
				result = ReconstructPlan( current );
				break;
			}



			foreach ( var action in usable )
			{
				if ( !current.State.Satisfies( action.Preconditions ) )
					continue;

				var nextState = current.State.WithEffects( action.Effects );

				if ( closed.Contains( nextState ) )
					continue;

				var node = Rent();
				node.Parent = current;
				node.G = current.G + action.Cost;
				node.H = nextState.CountUnsatisfied( goal );
				node.State = nextState;
				node.Action = action;
				allNodes.Add( node );

				open.Enqueue( node, node.Priority );
			}
		}

		// Return nodes to pool
		foreach ( var n in allNodes ) Return( n );

		return result;
	}

	private static List<AIAction> ReconstructPlan( Node node )
	{
		var plan = new List<AIAction>();
		while ( node is not null )
		{
			if ( node.Action is not null ) plan.Add( node.Action );
			node = node.Parent;
		}
		plan.Reverse();
		return plan;
	}
}
