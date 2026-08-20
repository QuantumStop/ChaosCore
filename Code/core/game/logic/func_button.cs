namespace Core;

public class func_button : BaseUsable
{
	protected override string GetEditorVis() => null;
	protected override bool _canBeHeldAccessor => false;
	public override bool CanBeHeld => false;

	public override bool Press( IPressable.Event press )
	{
		if ( !press.Source.Components.TryGet<BasePlayer>( out var basePlayer ) || basePlayer?.LifeState == LifeState.Dead ) return false;

		// Source of the Use should be a player, as this is specifically a player input press thing rather than general "anyone" (NPC) interaction
		OnUse?.Invoke( basePlayer );

		return true;
	}
}
