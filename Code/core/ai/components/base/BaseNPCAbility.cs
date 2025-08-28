using System;
using System.Text.Json.Serialization;
namespace Core;

//[GameResource( "NPC Ability", "npcabl", "GET EVERYTHING YOU NEED FOR A BETTER INTERNET EXPERIENCE\r\n", Icon = "bolt" )]
/// <summary>
/// NPC Ability
/// </summary>
[AssetType( Name = "NPC Ability", Extension = "npcabl" )]
public class NpcAbilityResource : GameResource
{
	protected override Bitmap CreateAssetTypeIcon( int width, int height ) { return CreateSimpleAssetTypeIcon( "bolt", width, height ); }
	public string AnimEventPrefix { get; set; }
	[Category( "Implementation" ), HideIf( "HasAction", true )] public string AbilityClassname { get; set; }
	[JsonIgnore, Hide] public bool HasClass => AbilityClassname != "";
	[Category( "Implementation" ), HideIf( "HasClass", true )] public Action<GameObject, SceneModel.GenericEvent> AbilityAction { get; set; }
	[JsonIgnore, Hide] public bool HasAction => AbilityAction != null;
}

public class BaseNpcAbility : Component
{
	[Property] public BaseNpc Owner { get; set; }
	[Property] public NpcAbilityResource NpcAbility { get; set; }

	public void HandleAnimEvent( SceneModel.GenericEvent evt )
	{
		if ( NpcAbility == null )
			return;

		if ( evt.Type.StartsWith( NpcAbility.AnimEventPrefix ) )
			OnAbilityEvent( evt );
	}

	public virtual void OnAbilityEvent( SceneModel.GenericEvent evt )
	{
		if ( NpcAbility.AbilityAction != null )
			NpcAbility.AbilityAction.Invoke( GameObject, evt );
	}
}
