namespace Core;
public class GameRules
{
	/// <summary>
	/// Is this game singleplayer?
	/// </summary>
	public virtual bool IsSinglePlayer { get => false; }
	/// <summary>
	/// Is this game multiplayer?
	/// </summary>
	public virtual bool IsMultiPlayer { get => false; }
	/// <summary>
	/// Is this game coop?
	/// </summary>
	public virtual bool IsCoop { get => false; }
	/// <summary>
	/// Is this game anything that uses network
	/// </summary>
	public bool IsOnline => IsMultiPlayer || IsCoop;
	/// <summary>
	/// Do we allow map transitioning
	/// </summary>
	public virtual bool CanTransition { get => false; }
	/// <summary>
	/// Game Logic every FixedUpdate()
	/// </summary>
	public virtual void GameTick() { }
	/// <summary>
	/// Game Logic every Update()
	/// </summary>
	public virtual void GameFrame() { }
}
