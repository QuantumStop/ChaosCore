namespace Core;

public static class WorldTime
{
	public static float Now => GameManagerSystem.WorldNow;
	public static float Delta => GameManagerSystem.WorldDelta;
}
