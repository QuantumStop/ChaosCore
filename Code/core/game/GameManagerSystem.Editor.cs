namespace Core;

public abstract partial class GameManagerSystem : GameObjectSystem
{
	[Property, ReadOnly] public Transform LastEditorCameraPosition { get; protected set; }

	protected virtual void OnEditorUpdate()
	{
		// you have to get it like this because onstart is too early or something
		var obj = Scene.GetAllObjects( true ).LastOrDefault( x => x.Name == "editor_camera" );

		if ( obj.IsValid() )
			LastEditorCameraPosition = obj.Transform.World;
	}
	protected virtual void OnEditorFixedUpdate() { }
	protected virtual void OnEditorStart() { }
}
