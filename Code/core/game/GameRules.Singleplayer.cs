namespace Core;
/// <summary>
/// Game rules and logic specific to a singleplayer game
/// </summary>
public class SingleplayerRules : GameRules
{
	public override bool IsSinglePlayer { get => true; }
	public virtual bool CanSaveLoad { get => true; }
	public override bool CanTransition { get => true; }
}
