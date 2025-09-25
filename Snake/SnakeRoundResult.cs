namespace Snake;

internal readonly struct SnakeRoundResult
{
    public SnakeRoundResult(bool exitToMenu, int score)
    {
        this.ExitToMenu = exitToMenu;
        this.Score = score;
    }

    public bool ExitToMenu { get; }

    public int Score { get; }

    public static SnakeRoundResult ExitToMenuResult() => new(true, 0);

    public static SnakeRoundResult Finished(int score) => new(false, score);
}
