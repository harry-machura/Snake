using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Snake;

internal sealed class SnakeGame
{
    private const int FieldWidth = 40;
    private const int FieldHeight = 20;

    private const char BorderCh = '█';
    private const char HeadCh = '■';
    private const char BodyCh = '■';
    private const char FoodCh = '●';

    private static readonly ConsoleColor BorderFg = ConsoleColor.DarkGray;
    private static readonly ConsoleColor HeadFg = ConsoleColor.Green;
    private static readonly ConsoleColor BodyFg = ConsoleColor.DarkGreen;
    private static readonly ConsoleColor FoodFg = ConsoleColor.Red;

    private static readonly Random Rng = new();

    public int Width => FieldWidth;
    public int Height => FieldHeight;

    public SnakeRoundResult PlayRound(SnakeDifficulty difficulty)
    {
        Console.CursorVisible = false;
        Console.Clear();
        EnsureWindowSize();
        DrawBorder();

        var snake = new LinkedList<(int x, int y)>();
        int startX = FieldWidth / 2;
        int startY = FieldHeight / 2;
        snake.AddFirst((startX, startY));
        snake.AddLast((startX - 1, startY));
        snake.AddLast((startX - 2, startY));

        var occupied = new HashSet<(int x, int y)>(snake);

        int dx = 1, dy = 0;

        var food = SpawnFood(occupied);
        DrawAt(food.x, food.y, FoodCh, FoodFg);

        int score = 0;
        int tickMs = GetInitialTickMs(difficulty);
        int minTickMs = GetMinimumTickMs(difficulty);
        var timer = new Stopwatch();
        timer.Start();

        bool gameOver = false;
        bool paused = false;

        DrawTitleBar(score, difficulty);
        DrawPauseOverlay(false);

        while (!gameOver)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.A:
                        if (dx != 1)
                        {
                            dx = -1;
                            dy = 0;
                        }
                        break;
                    case ConsoleKey.RightArrow:
                    case ConsoleKey.D:
                        if (dx != -1)
                        {
                            dx = 1;
                            dy = 0;
                        }
                        break;
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.W:
                        if (dy != 1)
                        {
                            dx = 0;
                            dy = -1;
                        }
                        break;
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        if (dy != -1)
                        {
                            dx = 0;
                            dy = 1;
                        }
                        break;
                    case ConsoleKey.P:
                        paused = !paused;
                        DrawPauseOverlay(paused);
                        timer.Restart();
                        continue;
                    case ConsoleKey.Escape:
                        Console.CursorVisible = true;
                        return SnakeRoundResult.ExitToMenu();
                }
            }

            if (paused)
            {
                Thread.Sleep(10);
                continue;
            }

            if (timer.ElapsedMilliseconds >= tickMs)
            {
                timer.Restart();

                var head = snake.First!.Value;
                int nextX = head.x + dx;
                int nextY = head.y + dy;

                if (nextX <= 0 || nextX >= FieldWidth - 1 || nextY <= 0 || nextY >= FieldHeight - 1)
                {
                    gameOver = true;
                    break;
                }

                if (occupied.Contains((nextX, nextY)))
                {
                    gameOver = true;
                    break;
                }

                DrawAt(nextX, nextY, HeadCh, HeadFg);
                DrawAt(head.x, head.y, BodyCh, BodyFg);

                snake.AddFirst((nextX, nextY));
                occupied.Add((nextX, nextY));

                bool ate = (nextX, nextY) == food;

                if (ate)
                {
                    score++;
                    DrawTitleBar(score, difficulty);
                    TryBeep(880, 80);

                    if (tickMs > minTickMs)
                    {
                        tickMs = Math.Max(minTickMs, tickMs - GetTickDecreaseStep(difficulty));
                    }

                    food = SpawnFood(occupied);
                    DrawAt(food.x, food.y, FoodCh, FoodFg);
                }
                else
                {
                    var tail = snake.Last!.Value;
                    snake.RemoveLast();
                    occupied.Remove(tail);
                    DrawAt(tail.x, tail.y, ' ', null);
                }
            }

            Thread.Sleep(1);
        }

        Console.SetCursorPosition(0, FieldHeight);
        Console.ResetColor();
        Console.WriteLine($"\nGame Over! Score: {score}   (Enter = weiter, ESC = zurück zum Menü)");
        Console.CursorVisible = true;
        DrawPauseOverlay(false);

        while (true)
        {
            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.Enter)
            {
                break;
            }

            if (key == ConsoleKey.Escape)
            {
                return SnakeRoundResult.ExitToMenu();
            }
        }

        return SnakeRoundResult.Finished(score);
    }

    private static void DrawTitleBar(int score, SnakeDifficulty difficulty)
    {
        Console.SetCursorPosition(0, FieldHeight);
        Console.ResetColor();
        Console.Write($" Score: {score}    Modus: {GetDifficultyLabel(difficulty)}  (WASD/Pfeile, ESC Menü, P Pause)   ");
    }

    private static void DrawBorder()
    {
        for (int x = 0; x < FieldWidth; x++)
        {
            DrawAt(x, 0, BorderCh, BorderFg);
            DrawAt(x, FieldHeight - 1, BorderCh, BorderFg);
        }

        for (int y = 0; y < FieldHeight; y++)
        {
            DrawAt(0, y, BorderCh, BorderFg);
            DrawAt(FieldWidth - 1, y, BorderCh, BorderFg);
        }
    }

    private static int GetInitialTickMs(SnakeDifficulty difficulty) => difficulty switch
    {
        SnakeDifficulty.Easy => 150,
        SnakeDifficulty.Hard => 90,
        _ => 120,
    };

    private static int GetMinimumTickMs(SnakeDifficulty difficulty) => difficulty switch
    {
        SnakeDifficulty.Easy => 75,
        SnakeDifficulty.Hard => 45,
        _ => 60,
    };

    private static int GetTickDecreaseStep(SnakeDifficulty difficulty) => difficulty switch
    {
        SnakeDifficulty.Easy => 2,
        SnakeDifficulty.Hard => 4,
        _ => 3,
    };

    private static string GetDifficultyLabel(SnakeDifficulty difficulty) => difficulty switch
    {
        SnakeDifficulty.Easy => "Leicht",
        SnakeDifficulty.Hard => "Schwer",
        _ => "Normal",
    };

    private static void DrawPauseOverlay(bool paused)
    {
        Console.SetCursorPosition(0, FieldHeight + 1);
        Console.ResetColor();
        if (paused)
        {
            Console.Write(" Pause aktiviert – P zum Fortsetzen.".PadRight(FieldWidth + 20));
        }
        else
        {
            Console.Write(new string(' ', FieldWidth + 20));
            Console.SetCursorPosition(0, FieldHeight + 1);
        }
    }

    private static (int x, int y) SpawnFood(HashSet<(int x, int y)> occupied)
    {
        int x;
        int y;
        do
        {
            x = Rng.Next(1, FieldWidth - 1);
            y = Rng.Next(1, FieldHeight - 1);
        }
        while (occupied.Contains((x, y)));

        return (x, y);
    }

    private static void DrawAt(int x, int y, char ch, ConsoleColor? fg)
    {
        Console.SetCursorPosition(x, y);
        var prevColor = Console.ForegroundColor;
        if (fg.HasValue)
        {
            Console.ForegroundColor = fg.Value;
        }

        Console.Write(ch);

        if (fg.HasValue)
        {
            Console.ForegroundColor = prevColor;
        }
    }

    private static void EnsureWindowSize()
    {
        try
        {
            if (Console.WindowWidth < FieldWidth || Console.WindowHeight < FieldHeight + 2)
            {
                Console.SetWindowSize(
                    Math.Max(Console.WindowWidth, FieldWidth),
                    Math.Max(Console.WindowHeight, FieldHeight + 2));
            }

            if (Console.BufferWidth < FieldWidth || Console.BufferHeight < FieldHeight + 2)
            {
                Console.SetBufferSize(
                    Math.Max(Console.BufferWidth, FieldWidth),
                    Math.Max(Console.BufferHeight, FieldHeight + 2));
            }
        }
        catch
        {
            // Manche Terminals erlauben keine Größenänderung.
        }
    }

    private static void TryBeep(int frequency, int durationMs)
    {
        try
        {
            Console.Beep(frequency, durationMs);
        }
        catch
        {
            // Kein Beep verfügbar – ignorieren.
        }
    }
}
