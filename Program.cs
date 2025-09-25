using System.Text;

namespace Snake;

internal static class Program
{
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        using var audioService = new AudioService();
        var highscoreService = new HighscoreService();
        var snakeGame = new SnakeGame();
        var app = new SnakeApp(audioService, highscoreService, snakeGame);

        app.Run();
    }
}
