namespace Core;

public static partial class VanityAPI
{
	public static bool TryGetTargetSteamId( VanityRecipient recipient, BasePlayer specificPlayer, 
		BaseEntity activator, out ulong steamId )
	{
		steamId = 0;

		return recipient switch
		{
			VanityRecipient.Everyone => true,
			VanityRecipient.Specific => TryGetPlayerSteamId( specificPlayer, out steamId ),
			VanityRecipient.Activator => TryGetActivatorSteamId( activator, out steamId ),
			_ => true
		};
	}

	private static bool TryGetActivatorSteamId( BaseEntity activator, out ulong steamId )
	{
		steamId = 0;

		if ( !activator.IsValid() )
			return false;

		if ( activator is BasePlayer player )
			return TryGetPlayerSteamId( player, out steamId );

		if ( activator is BasePawn pawn && pawn.PawnCamera.IsValid() )
			return TryGetPawnSteamId( pawn, out steamId );

		var activatorPlayer = activator.GameObject.GetComponentInParent<BasePlayer>();
		if ( activatorPlayer.IsValid() )
			return TryGetPlayerSteamId( activatorPlayer, out steamId );

		var activatorPawn = activator.GameObject.GetComponentInParent<BasePawn>();
		if ( activatorPawn.IsValid() && activatorPawn.PawnCamera.IsValid() )
			return TryGetPawnSteamId( activatorPawn, out steamId );

		return false;
	}

	private static bool TryGetPlayerSteamId( BasePlayer player, out ulong steamId )
	{
		steamId = 0;

		if ( !player.IsValid() )
			return false;

		return TryGetPawnSteamId( player, out steamId );
	}

	private static bool TryGetPawnSteamId( BasePawn pawn, out ulong steamId )
	{
		steamId = 0;

		if ( !pawn.IsValid() )
			return false;

		if ( pawn.Owner.IsValid() && pawn.Owner.Connection is not null )
		{
			steamId = pawn.Owner.Connection.SteamId;
			return true;
		}

		if ( pawn.SteamId != 0 )
		{
			steamId = pawn.SteamId;
			return true;
		}

		if ( pawn.IsControlledLocally && Connection.Local is not null )
		{
			steamId = Connection.Local.SteamId;
			return true;
		}

		return false;
	}
}
