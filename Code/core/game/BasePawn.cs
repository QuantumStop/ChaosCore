namespace Core;

public abstract class BasePawn : BaseEntity
{
	public static BasePawn Local { get; private set; }
	[Property, ReadOnly, Sync( SyncFlags.FromHost )] public Client Owner { get; set; }
	/// <summary>
	/// Are we possessing this pawn right now? (Clientside)
	/// </summary>
	[Property, Feature( "Debug" ), ReadOnly] public bool IsPossessedLocally => Local == this;
	/// <summary>
	/// Are we controlling this pawn right now? (Clientside)
	/// </summary>
	[Property, Feature( "Debug" ), ReadOnly] public bool IsControlledLocally => IsPossessedLocally && !IsProxy;
	/// <summary>
	/// The pawn's camera. Has to have one.
	/// </summary>
	[Property, Feature( "Defines" )] public CameraComponent PawnCamera { get; protected set; }
	/// <summary>
	/// Who's the owner?
	/// </summary>
	[Sync] public ulong SteamId { get; set; }

	public virtual void SetCameraActive( bool which ) => PawnCamera?.GameObject.Enabled = which;

	/// <summary>
	/// Possess "this" pawn.
	/// </summary>
	public void Possess() => Possess( this );
	/// <summary>
	/// Possess a given pawn
	/// </summary>
	/// <param name="pawn">Given pawn</param>
	public static void Possess( BasePawn pawn )
	{
		if ( pawn.IsPossessedLocally ) return; // already possessing this

		DePossess( Local ); // stop possing current pawn before hopping to the new

		Local = pawn;
		pawn?.OnPossess();

		// Valid and we own it?
		if ( pawn.IsValid() )
		{
			if ( !pawn.IsProxy ) pawn.SteamId = Connection.Local.SteamId;
			pawn.SetCameraActive( true );
		}

		// call the "event" that we were possessed
		Client.OnPossess( pawn );
	}

	/// <summary>
	/// Stop possessing "this" pawn.
	/// </summary>
	public void DePossess() => DePossess( this );
	/// <summary>
	/// Stop possessing a given pawn
	/// </summary>
	/// <param name="pawn"></param>
	public static void DePossess( BasePawn pawn )
	{
		if ( pawn.IsValid() && pawn.IsPossessedLocally )
		{
			Local = null;
			pawn?.OnDePossess();

			// Valid and we own it?
			if ( pawn.IsValid() )
			{
				if ( !pawn.IsProxy ) pawn.SteamId = 0;
				pawn.SetCameraActive( false );
			}
		}
	}

	/// <summary>
	/// Possess this pawn as client's "Main Pawn" (primarily for player spawning)
	/// </summary>
	public void AsMain( Client owner )
	{
		Owner = owner;
		if ( this is BasePlayer basePlayer ) Owner?.MainPawn = basePlayer;

		if ( !Owner.IsValid() )
		{
			Possess();
			return;
		}

		if ( Owner.IsLocalPlayer )
		{
			Possess();
			return;
		}

		OnOwnerAssigned( owner );
	}

	[Rpc.Owner]
	public virtual void OnOwnerAssigned( Client owner )
	{
		if ( !owner.IsValid() || !owner.IsLocalPlayer )
			return;

		Possess();
	}

	protected override void OnStart()
	{
		base.OnStart();
		PawnCamera?.GameObject.Enabled = IsPossessedLocally;
	}

	/// <summary>
	/// This pawn was possessed
	/// </summary>
	protected virtual void OnPossess() { }
	/// <summary>
	/// This pawn was depossessed
	/// </summary>
	protected virtual void OnDePossess() { }

	/// <summary>
	/// Stop possessing if disabled (or destoryed) the pawn, return to main pawn if this wasn't one
	/// </summary>
	protected override void OnDisabled()
	{
		if ( Owner.IsValid() && Owner.MainPawn != this && Owner.MainPawn.IsValid() ) Possess( Owner.MainPawn );
		else DePossess();
	}

	[ConCmd( "pawn_force_possess", ConVarFlags.Cheat, Help = "Forcefully possess a pawn with given GO name" )] public static void ForcePossess( string name ) => Game.ActiveScene.GetAllComponents<BasePawn>().FirstOrDefault( x => x.GameObject.Name == name && Client.Local.MainPawn != x )?.Possess();

	[ConCmd( "pawn_force_main", ConVarFlags.Cheat, Help = "Forcefully return to main pawn" )] public static void ForceReturnMain() => Possess( Client.Local.MainPawn );
}
