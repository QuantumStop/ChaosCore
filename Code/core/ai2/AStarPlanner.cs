/*
namespace AI
{
	public class AStarPlanner
	{
		public class Node
		{
			public WorldState State;
			public Node ParentNode;
			public string ActionName;
			public int G, H, F;
		}

		private const int MAX_NODES = 500;            // hard cap for nodes
		private const int MAX_MS = 20;                 // planning time budget (per agent)

		public static bool Plan( ActionPlanner ap, WorldState start, WorldState goal,
			out List<string> plan, out List<WorldState> states )
		{
			plan = new();
			states = new();

			// keeps the lowest F on top
			var open = new PriorityQueue<Node, int>();
			var closed = new HashSet<WorldStateKey>();

			// best discovered G per worldstate
			var bestCost = new Dictionary<WorldStateKey, int>();

			Node startNode = new()
			{
				State = start,
				ParentNode = null,
				ActionName = null,
				G = 0,
				H = Heuristic( start, goal ),
			};
			startNode.F = startNode.G + startNode.H;

			open.Enqueue( startNode, startNode.F );

			var stopwatch = System.Diagnostics.Stopwatch.StartNew();
			int nodesExpanded = 0;

			while ( open.Count > 0 )
			{
				if ( nodesExpanded++ > MAX_NODES )
					return false;
				if ( stopwatch.ElapsedMilliseconds > MAX_MS )
					return false;

				Node current = open.Dequeue();
				var currentKey = new WorldStateKey( current.State );

				if ( closed.Contains( currentKey ) )
					continue;

				closed.Add( currentKey );

				// goal check
				if ( current.State.Matches( goal ) )
				{
					while ( current is not null && current.ActionName is not null )
					{
						plan.Insert( 0, current.ActionName );
						states.Insert( 0, current.State );
						current = current.ParentNode;
					}
					return true;
				}

				foreach ( var (nextState, action) in ap.GetTransitions( current.State ) )
				{
					var nextKey = new WorldStateKey( nextState );
					int gCost = current.G + action.Cost;

					// Skip if we already found a cheaper path
					if ( bestCost.TryGetValue( nextKey, out int oldG ) && oldG <= gCost )
						continue;

					bestCost[nextKey] = gCost;

					int hCost = Heuristic( nextState, goal );
					int fCost = gCost + hCost;

					Node nextNode = new()
					{
						State = nextState,
						ParentNode = current,
						ActionName = action.Name,
						G = gCost,
						H = hCost,
						F = fCost
					};

					open.Enqueue( nextNode, fCost );
				}
			}

			return false;
		}

		private static int Heuristic( WorldState from, WorldState to )
		{
			long care = ~to.DontCare;
			long diff = (from.Values & care) ^ (to.Values & care);

			int dist = 0;
			while ( diff != 0 )
			{
				dist += (int)(diff & 1);
				diff >>= 1;
			}

			return dist * 2;  // hard coded weight bad. This might suit better as some sort of param
		}

		public static WorldState AllDontCare( int atomCount )
		{
			return new WorldState
			{
				Values = 0,
				DontCare = (1 << atomCount) - 1
			};
		}


		private readonly struct WorldStateKey : IEquatable<WorldStateKey>
		{
			public readonly long Values;
			public readonly long DontCare;

			public WorldStateKey( WorldState ws )
			{
				Values = ws.Values;
				DontCare = ws.DontCare;
			}

			public bool Equals( WorldStateKey other )
			{
				long care = ~DontCare;
				return (Values & care) == (other.Values & care);
			}

			public override bool Equals( object obj )
				=> obj is WorldStateKey other && Equals( other );

			public override int GetHashCode()
			{
				long care = ~DontCare;
				return (Values & care).GetHashCode();
			}
		}
	}
}*/
