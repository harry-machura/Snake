using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Snake;

internal sealed class SnakeGame
{
    private const int DefaultFieldWidth = 40;
    private const int DefaultFieldHeight = 20;
    private const int MinFieldWidth = 20;
    private const int MinFieldHeight = 12;
    private const int HudLineCount = 4;

    private int _fieldWidth = DefaultFieldWidth;
    private int _fieldHeight = DefaultFieldHeight;

    private const char BorderCh = '█';
    private const char HeadCh = '■';
    private const char BodyCh = '■';
    private const char FoodCh = '●';
    private const char BonusFoodCh = '★';
    private const char SlowFoodCh = '□';
    private const char ObstacleCh = '▓';

    private static readonly ConsoleColor BorderFg = ConsoleColor.DarkGray;
    private static readonly ConsoleColor HeadFg = ConsoleColor.Green;
    private static readonly ConsoleColor BodyFg = ConsoleColor.DarkGreen;
    private static readonly ConsoleColor FoodFg = ConsoleColor.Red;
    private static readonly ConsoleColor BonusFoodFg = ConsoleColor.Yellow;
    private static readonly ConsoleColor SlowFoodFg = ConsoleColor.Cyan;
    private static readonly ConsoleColor ObstacleFg = ConsoleColor.DarkYellow;

    private static readonly Random Rng = new();

    private enum FoodKind
    {
        Normal,
        Bonus,
        Slowdown
    }

    private readonly record struct FoodItem((int x, int y) Position, FoodKind Kind);

    public int Width => _fieldWidth;
    public int Height => _fieldHeight;

    public SnakeRoundResult PlayRound(SnakeDifficulty difficulty)
    {
        Console.CursorVisible = false;

        UpdateFieldDimensions();
        EnsureWindowSize();
        NormalizeFieldDimensions();
        Console.Clear();
        DrawBorder();

        var snake = new LinkedList<(int x, int y)>();
        int startX = _fieldWidth / 2;
        int startY = _fieldHeight / 2;
        snake.AddFirst((startX, startY));
        snake.AddLast((startX - 1, startY));
        snake.AddLast((startX - 2, startY));

        var occupied = new HashSet<(int x, int y)>(snake);

        int dx = 1, dy = 0;

        var obstacles = CreateObstacles(difficulty);
        foreach (var obstacle in obstacles)
        {
            DrawAt(obstacle.x, obstacle.y, ObstacleCh, ObstacleFg);
            occupied.Add(obstacle);
        }

        var food = SpawnFoodItem(occupied);
        DrawFood(food);

        int score = 0;
        int tickMs = GetInitialTickMs(difficulty);
        int minTickMs = GetMinimumTickMs(difficulty);
        var timer = new Stopwatch();
        timer.Start();

        bool gameOver = false;
        bool paused = false;

        DrawTitleBar(score, difficulty);
        DrawPauseOverlay(false);
        DrawLegend();

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

                if (nextX <= 0 || nextX >= _fieldWidth - 1 || nextY <= 0 || nextY >= _fieldHeight - 1)
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

                bool ate = (nextX, nextY) == food.Position;

                if (ate)
                {
                    ApplyFoodEffect(food.Kind, ref score, ref tickMs, minTickMs, difficulty);
                    DrawTitleBar(score, difficulty);
                    TryBeep(880, 80);

                    food = SpawnFoodItem(occupied);
                    DrawFood(food);
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

        Console.SetCursorPosition(0, _fieldHeight);
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

    private static void ApplyFoodEffect(FoodKind kind, ref int score, ref int tickMs, int minTickMs, SnakeDifficulty difficulty)
    {
        switch (kind)
        {
            case FoodKind.Bonus:
                score += 3;
                break;
            case FoodKind.Slowdown:
                score++;
                int maxTick = GetInitialTickMs(difficulty);
                tickMs = Math.Min(maxTick, tickMs + 15);
                break;
            default:
                score++;
                if (tickMs > minTickMs)
                {
                    tickMs = Math.Max(minTickMs, tickMs - GetTickDecreaseStep(difficulty));
                }
                break;
        }
    }

    private void DrawTitleBar(int score, SnakeDifficulty difficulty)
    {
        Console.SetCursorPosition(0, _fieldHeight);
        Console.ResetColor();
        string text = $" Score: {score}    Modus: {GetDifficultyLabel(difficulty)}  (WASD/Pfeile, ESC Menü, P Pause)";
        Console.Write(text.PadRight(_fieldWidth + 20));
    }

    private void DrawBorder()
    {
        for (int x = 0; x < _fieldWidth; x++)
        {
            DrawAt(x, 0, BorderCh, BorderFg);
            DrawAt(x, _fieldHeight - 1, BorderCh, BorderFg);
        }

        for (int y = 0; y < _fieldHeight; y++)
        {
            DrawAt(0, y, BorderCh, BorderFg);
            DrawAt(_fieldWidth - 1, y, BorderCh, BorderFg);
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

    private static void DrawFood(FoodItem food)
    {
        (char ch, ConsoleColor color) = food.Kind switch
        {
            FoodKind.Bonus => (BonusFoodCh, BonusFoodFg),
            FoodKind.Slowdown => (SlowFoodCh, SlowFoodFg),
            _ => (FoodCh, FoodFg)
        };

        DrawAt(food.Position.x, food.Position.y, ch, color);
    }

    private void DrawPauseOverlay(bool paused)
    {
        Console.SetCursorPosition(0, _fieldHeight + 1);
        Console.ResetColor();
        if (paused)
        {
            Console.Write(" Pause aktiviert – P zum Fortsetzen.".PadRight(_fieldWidth + 20));
        }
        else
        {
            Console.Write(new string(' ', _fieldWidth + 20));
            Console.SetCursorPosition(0, _fieldHeight + 1);
        }
    }

    private void DrawLegend()
    {
        Console.SetCursorPosition(0, _fieldHeight + 2);
        Console.ResetColor();
        string legend = $" Items: {FoodCh}=+1  {BonusFoodCh}=+3  {SlowFoodCh}=Verlangsamung  Hindernisse={ObstacleCh}";
        Console.Write(legend.PadRight(_fieldWidth + 25));
    }

    private FoodItem SpawnFoodItem(HashSet<(int x, int y)> occupied)
    {
        int x;
        int y;
        do
        {
            x = Rng.Next(1, _fieldWidth - 1);
            y = Rng.Next(1, _fieldHeight - 1);
        }
        while (occupied.Contains((x, y)));

        FoodKind kind = RollFoodKind();
        return new FoodItem((x, y), kind);
    }

    private static FoodKind RollFoodKind()
    {
        int roll = Rng.Next(100);
        if (roll < 70)
        {
            return FoodKind.Normal;
        }

        if (roll < 90)
        {
            return FoodKind.Bonus;
        }

        return FoodKind.Slowdown;
    }

    private List<(int x, int y)> CreateObstacles(SnakeDifficulty difficulty)
    {
        var list = new List<(int x, int y)>();

        if (difficulty == SnakeDifficulty.Easy)
        {
            return list;
        }

        if (difficulty == SnakeDifficulty.Normal)
        {
            int left = _fieldWidth / 3;
            int right = _fieldWidth - 1 - _fieldWidth / 3;
            for (int y = 3; y < _fieldHeight - 3; y++)
            {
                if (y % 2 == 0)
                {
                    list.Add((left, y));
                    list.Add((right, y));
                }
            }

            return list;
        }

        int innerLeft = 5;
        int innerRight = _fieldWidth - 6;
        int innerTop = 4;
        int innerBottom = _fieldHeight - 5;

        for (int x = innerLeft; x <= innerRight; x++)
        {
            if (x % 2 == 0)
            {
                list.Add((x, innerTop));
                list.Add((x, innerBottom));
            }
        }

        for (int y = innerTop; y <= innerBottom; y++)
        {
            if (y % 2 == 1)
            {
                list.Add((innerLeft, y));
                list.Add((innerRight, y));
            }
        }

        return list;
    }

    private void UpdateFieldDimensions()
    {
        int availableWidth = Console.WindowWidth;
        int availableHeight = Console.WindowHeight;

        if (availableWidth <= 0)
        {
            availableWidth = DefaultFieldWidth;
        }

        if (availableHeight <= 0)
        {
            availableHeight = DefaultFieldHeight + HudLineCount;
        }

        int computedWidth = Math.Max(MinFieldWidth, availableWidth);
        int computedHeight = Math.Max(MinFieldHeight, availableHeight - HudLineCount);

        _fieldWidth = computedWidth;
        _fieldHeight = computedHeight;
    }

    private void NormalizeFieldDimensions()
    {
        int bufferWidth = Console.BufferWidth;
        if (bufferWidth > 0)
        {
            int minAllowed = Math.Min(MinFieldWidth, bufferWidth);
            _fieldWidth = Math.Clamp(_fieldWidth, minAllowed, bufferWidth);
        }

        int bufferHeight = Console.BufferHeight;
        if (bufferHeight > 0)
        {
            int maxPlayableHeight = Math.Max(1, bufferHeight - HudLineCount);
            int minAllowedHeight = Math.Min(MinFieldHeight, maxPlayableHeight);
            _fieldHeight = Math.Clamp(_fieldHeight, minAllowedHeight, maxPlayableHeight);
        }
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

    private void EnsureWindowSize()
    {
        try
        {
            int requiredHeight = _fieldHeight + HudLineCount;

            if (Console.WindowWidth < _fieldWidth || Console.WindowHeight < requiredHeight)
            {
                Console.SetWindowSize(
                    Math.Max(Console.WindowWidth, _fieldWidth),
                    Math.Max(Console.WindowHeight, requiredHeight));
            }

            if (Console.BufferWidth < _fieldWidth || Console.BufferHeight < requiredHeight)
            {
                Console.SetBufferSize(
                    Math.Max(Console.BufferWidth, _fieldWidth),
                    Math.Max(Console.BufferHeight, requiredHeight));
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
