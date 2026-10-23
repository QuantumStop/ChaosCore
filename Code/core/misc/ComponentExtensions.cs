namespace Core;

public static class ComponentExtensions
{
	[Rpc.Broadcast] public static void DestroyNetwork( this Component component ) => component.Destroy();
}
