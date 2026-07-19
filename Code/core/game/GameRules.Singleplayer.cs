namespace Core;
/// <summary>
/// Game rules and logic specific to a singleplayer game
/// </summary>
public class SingleplayerRules : GameRules
{
	public override bool IsSinglePlayer => true;
	public virtual bool CanSaveLoad => true;
	public override bool CanTransition => true;
}
