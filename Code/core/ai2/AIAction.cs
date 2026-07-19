using System;
using HL2K.AI;

namespace Core.AI;

public abstract class AIAction
{
#if IGNIS
	[SaveRestore]
#endif
	public List<WorldFact> Preconditions { get; set; } = [];
#if IGNIS
	[SaveRestore]
#endif
	public List<WorldFact> Effects { get; set; } = [];
#if IGNIS
	[SaveRestore]
#endif
	public float Cost { get; set; } = 1f;

#if IGNIS
	[SaveRestore]
#endif
	public AIActionDefinition ActionInstanceRef { get; set; } // ref to the action
#if IGNIS
	[SaveRestore]
#endif
	public AIActionDefinition.ActionList ActionType { get; set; }
	public AIController Owner { get; set; } // ref to the owner

	public virtual void ApplyEffects( WorldState world )
	{
		foreach ( var eff in Effects )
		{
			world.Set( eff.Name, eff.Value );
		}
	}

	public abstract bool IsDone();
	public abstract bool CheckProceduralPrecondition( AIController agent );
	public abstract void Perform( AIController agent );
	/// <summary>
	/// Gathers and assigns data from an action definition. Preconditions, postconditions, and cost.
	/// </summary>
	/// <param name="actionType"></param>
	/// <param name="owner"></param>
	public virtual void RegisterActionDefinition( AIActionDefinition.ActionList actionType, AIController owner )
	{
		ActionType = actionType;
		Owner = owner;

		foreach ( var action in owner.Definition.ActionList )
		{
			if ( action.Action != actionType )
				continue;

			foreach ( var pre in action.PreConditions ) Preconditions.Add( new WorldFact( pre.Name, pre.Value ) );
			foreach ( var post in action.PostConditions ) Effects.Add( new WorldFact( post.Name, post.Value ) );

			Cost = action.Cost;
			break;
		}

	}
	public virtual void OnEnter( AIController agent ) { }
	public virtual void OnExit( AIController agent ) { }
	public virtual bool IsFailed() { return false; }
}

// finally.
public class AIActionRegistry( Scene scene ) : GameObjectSystem<AIActionRegistry>( scene )
{
	private readonly Dictionary<AIActionDefinition.ActionList, Func<AIController, AIAction>> _registry = new()
	{
		{ AIActionDefinition.ActionList.ActionTakeCover,        a => new MoveToCoverAction( a )        },
		{ AIActionDefinition.ActionList.ActionRangeAttack1,     a => new HoundeyeAttackAction( a )     },
		{ AIActionDefinition.ActionList.ActionChaseEnemy,       a => new MoveToEnemyAction( a )        },
		{ AIActionDefinition.ActionList.ActionScatter,          a => new PanicScatterAction( a )       },
		{ AIActionDefinition.ActionList.ActionBackAwayFromEnemy,a => new HoundeyeBackOffAction( a )    },
		{ AIActionDefinition.ActionList.ActionFollowTheLeader,  a => new FollowTheLeaderAction( a )    },
		{ AIActionDefinition.ActionList.ActionGoToEnemyLKP,     a => new GoToEnemyLKPAction( a )       },
		{ AIActionDefinition.ActionList.ActionHeadcrabLeapAttack,a => new HeadcrabLeapAttackAction( a )},
		{ AIActionDefinition.ActionList.ActionBarnacleWait,     a => new BarnacleWait( a )             },
		{ AIActionDefinition.ActionList.ActionBarnacleLift,     a => new BarnacleLift( a )             },
		{ AIActionDefinition.ActionList.ActionBarnacleEat,      a => new BarnacleEat( a )             },
		{ AIActionDefinition.ActionList.ActionMeleeAttack1,     a => new MeleeAttack1Action( a )       },
		{ AIActionDefinition.ActionList.ActionHoundeyeRegroup,  a => new HoundeyeRegroup( a )          },
		{ AIActionDefinition.ActionList.ActionHoundeyeFindRestingPoint,  a => new HoundeyeFindRestingPoint( a )          },
		{ AIActionDefinition.ActionList.ActionHoundeyeRest,  a => new HoundeyeRest( a )          },
		{ AIActionDefinition.ActionList.ActionHoundeyeGuard,  a => new HoundeyeGuard( a )          },
		{ AIActionDefinition.ActionList.ActionHoundeyeMoveToGuardPoint,  a => new HoundeyeMoveToGuardPoint( a )          },
		{ AIActionDefinition.ActionList.ActionHoundeyeSearch,  a => new HoundeyeSearchAction( a )          },
		{ AIActionDefinition.ActionList.ActionHoundeyeEncircle,  a => new HoundeyeEncircleAction( a )          },
		{ AIActionDefinition.ActionList.ActionHoundeyeCommunicate,  a => new HoundeyeCommunicate( a )          },
		{ AIActionDefinition.ActionList.ActionHoundeyePlayFleeFriend,  a => new HoundeyeFleeFriendAction( a )          },
		{ AIActionDefinition.ActionList.ActionHoundeyePlayChaseFriend,  a => new HoundeyeChaseFriendAction( a )          },
		{ AIActionDefinition.ActionList.ActionHoundeyeReceiveCommunication,  a => new HoundeyeReceiveCommunication( a )          },
		{ AIActionDefinition.ActionList.ProcessPainAction,  a => new ProcessPainAction( a )          },
		{ AIActionDefinition.ActionList.InvestigateSoundAction,  a => new InvestigateSound( a )          },
		{ AIActionDefinition.ActionList.HoundeyeHearSuspiciousSoundAction,  a => new HoundeyeHearSuspiciousAction( a )          },
		{ AIActionDefinition.ActionList.ActionBullsquidSpitAttack,  a => new BullsquidSpitAttackAction( a )          },
		{ AIActionDefinition.ActionList.SniffOutScent,  a => new SniffOutScentAction( a )          },
	};

	public void Register( AIActionDefinition.ActionList key, Func<AIController, AIAction> factory )
	{
		_registry[key] = factory;
	}

	public AIAction Create( AIActionDefinition.ActionList key, AIController owner )
	{
		if ( _registry.TryGetValue( key, out var factory ) )
			return factory( owner );

		Log.Warning( $"[AIActionRegistry] No factory registered for action '{key}'!" );
		return null;
	}
}
