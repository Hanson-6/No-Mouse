/// <summary>
/// Static container for persisting player data between scenes/levels.
/// </summary>
public static class GameData
{
    public static int CurrentLevel = 1;
    public static int Lives = 3;
    public static int Score = 0;

    public static void Reset()
    {
        CurrentLevel = 1;
        Lives = 3;
        Score = 0;
    }
}
