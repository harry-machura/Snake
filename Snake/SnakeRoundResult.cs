namespace Snake;

internal readonly struct SnakeRoundResult
{
    public SnakeRoundResult(bool exitToMenu, int score)
    {
        ExitToMenu = exitToMenu;
        Score = score;
    }

    public bool ExitToMenu { get; }

    public int Score { get; }

    public static SnakeRoundResult ExitToMenu() => new(true, 0);

    public static SnakeRoundResult Finished(int score) => new(false, score);
}
