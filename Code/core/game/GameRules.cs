namespace Core;

public class GameRules
{
	/// <summary>
	/// Is this game singleplayer?
	/// </summary>
	public virtual bool IsSinglePlayer => false;
	/// <summary>
	/// Is this game multiplayer?
	/// </summary>
	public virtual bool IsMultiPlayer => false;
	/// <summary>
	/// Is this game coop?
	/// </summary>
	public virtual bool IsCoop => false;
	/// <summary>
	/// Is this game anything that uses network
	/// </summary>
	public bool IsOnline => IsMultiPlayer || IsCoop;
	/// <summary>
	/// Do we allow map transitioning
	/// </summary>
	public virtual bool CanTransition => false;
	/// <summary>
	/// Game Logic every FixedUpdate()
	/// </summary>
	public virtual void GameTick() { }
	/// <summary>
	/// Game Logic every Update()
	/// </summary>
	public virtual void GameFrame() { }
	/// <summary>
	/// Game Logic was OnStart()'ed
	/// </summary>
	public virtual void GameStart() { }
	/// <summary>
	/// Game Rules were changed to current rules (manually called)
	/// </summary>
	public virtual void GameChange() { }
	/// <summary>
	/// When you pause should the world stop?
	/// </summary>
	public virtual bool ShouldPause => IsSinglePlayer;
	/// <summary>
	/// Do we allow changing the map (manually)
	/// </summary>
	public virtual bool AllowMapChange => IsSinglePlayer;
}
