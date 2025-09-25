using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

internal sealed class SnakeGame
{
    public const int PlayfieldWidth = 40;
    public const int PlayfieldHeight = 20;
    private const int HudHeight = 3;

    public static int RequiredHeight => PlayfieldHeight + HudHeight;

    private const char BorderCh = '█';
    private const char HeadCh = '■';
    private const char BodyCh = '■';
    private const char FoodCh = '●';

    private static readonly ConsoleColor BorderFg = ConsoleColor.DarkGray;
    private static readonly ConsoleColor HeadFg = ConsoleColor.Green;
    private static readonly ConsoleColor BodyFg = ConsoleColor.DarkGreen;
    private static readonly ConsoleColor FoodFg = ConsoleColor.Red;
    private static readonly ConsoleColor HudFg = ConsoleColor.Gray;

    private readonly Random _rng;
    private ConsoleFrameBuffer? _frameBuffer;
    private bool _useBuffer;

    private int TotalHeight => PlayfieldHeight + HudHeight;

    public SnakeGame(Random rng)
    {
        _rng = rng;
    }

    public int PlayRound()
    {
        Console.CursorVisible = false;
        Console.Clear();
        EnsureWindowSize();

        _frameBuffer = ConsoleFrameBuffer.TryCreate(PlayfieldWidth, TotalHeight);
        _useBuffer = _frameBuffer != null;

        _frameBuffer?.Clear(' ');

        DrawBorder();

        var snake = new LinkedList<(int x, int y)>();
        int startX = PlayfieldWidth / 2;
        int startY = PlayfieldHeight / 2;
        snake.AddFirst((startX, startY));
        snake.AddLast((startX - 1, startY));
        snake.AddLast((startX - 2, startY));

        var occupied = new HashSet<(int x, int y)>(snake);

        int dx = 1, dy = 0;

        var food = SpawnFood(occupied);

        DrawAt(food.x, food.y, FoodCh, FoodFg);
        foreach (var segment in snake)
        {
            DrawAt(segment.x, segment.y, segment == snake.First!.Value ? HeadCh : BodyCh, segment == snake.First!.Value ? HeadFg : BodyFg);
        }

        DrawTitleBar(score: 0);
        DrawLegend();
        DrawPauseOverlay(isPaused: false);

        _frameBuffer?.Flush();

        int score = 0;
        int tickMs = 120;
        var timer = new Stopwatch();
        timer.Start();

        bool gameOver = false;
        bool paused = false;

        while (!gameOver)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                switch (key)
                {
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.A:
                        if (dx != 1) { dx = -1; dy = 0; }
                        break;
                    case ConsoleKey.RightArrow:
                    case ConsoleKey.D:
                        if (dx != -1) { dx = 1; dy = 0; }
                        break;
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.W:
                        if (dy != 1) { dx = 0; dy = -1; }
                        break;
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        if (dy != -1) { dx = 0; dy = 1; }
                        break;
                    case ConsoleKey.P:
                    case ConsoleKey.Spacebar:
                        paused = !paused;
                        DrawPauseOverlay(paused);
                        FlushIfNeeded();
                        break;
                    case ConsoleKey.Escape:
                        RestoreHudBeforeExit();
                        return -1;
                }
            }

            if (paused)
            {
                Thread.Sleep(16);
                continue;
            }

            if (timer.ElapsedMilliseconds >= tickMs)
            {
                timer.Restart();

                var head = snake.First!.Value;
                int nx = head.x + dx;
                int ny = head.y + dy;

                if (nx <= 0 || nx >= PlayfieldWidth - 1 || ny <= 0 || ny >= PlayfieldHeight - 1)
                {
                    gameOver = true;
                }
                else if (occupied.Contains((nx, ny)))
                {
                    gameOver = true;
                }
                else
                {
                    snake.AddFirst((nx, ny));
                    occupied.Add((nx, ny));

                    bool ate = (nx, ny) == food;

                    DrawAt(nx, ny, HeadCh, HeadFg);
                    DrawAt(head.x, head.y, BodyCh, BodyFg);

                    if (ate)
                    {
                        score++;
                        DrawTitleBar(score);
                        TryBeep(880, 80);

                        if (tickMs > 45)
                        {
                            tickMs -= 3;
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

                    FlushIfNeeded();
                }
            }

            Thread.Sleep(1);
        }

        FlushIfNeeded();

        Console.SetCursorPosition(0, TotalHeight);
        Console.ResetColor();
        Console.WriteLine($"\nGame Over! Score: {score}   (Enter = weiter, ESC = zurück zum Menü)");
        Console.CursorVisible = true;

        while (true)
        {
            var k = Console.ReadKey(true).Key;
            if (k == ConsoleKey.Enter) break;
            if (k == ConsoleKey.Escape) return -1;
        }

        return score;
    }

    private void FlushIfNeeded()
    {
        _frameBuffer?.Flush();
    }

    private void DrawTitleBar(int score)
    {
        string text = $" Score: {score}    (WASD/Pfeile, ESC Menü, P Pause)";
        WriteHudLine(PlayfieldHeight, text, HudFg);
    }

    private void DrawLegend()
    {
        string legend = $" {HeadCh} Kopf  {BodyCh} Körper  {FoodCh} Futter";
        WriteHudLine(PlayfieldHeight + 1, legend, HudFg);
    }

    private void DrawPauseOverlay(bool isPaused)
    {
        string message = isPaused ? " == P A U S E ==  (P oder Leertaste) " : string.Empty;
        WriteHudLine(PlayfieldHeight + 2, message, HudFg);
    }

    private void WriteHudLine(int y, string text, ConsoleColor color)
    {
        string padded = text.Length > PlayfieldWidth ? text[..PlayfieldWidth] : text.PadRight(PlayfieldWidth);
        for (int i = 0; i < padded.Length && i < PlayfieldWidth; i++)
        {
            DrawAt(i, y, padded[i], color);
        }
    }

    private void DrawBorder()
    {
        for (int x = 0; x < PlayfieldWidth; x++)
        {
            DrawAt(x, 0, BorderCh, BorderFg);
            DrawAt(x, PlayfieldHeight - 1, BorderCh, BorderFg);
        }

        for (int y = 0; y < PlayfieldHeight; y++)
        {
            DrawAt(0, y, BorderCh, BorderFg);
            DrawAt(PlayfieldWidth - 1, y, BorderCh, BorderFg);
        }
    }

    private (int x, int y) SpawnFood(HashSet<(int x, int y)> occupied)
    {
        int x, y;
        do
        {
            x = _rng.Next(1, PlayfieldWidth - 1);
            y = _rng.Next(1, PlayfieldHeight - 1);
        }
        while (occupied.Contains((x, y)));

        return (x, y);
    }

    private void DrawAt(int x, int y, char ch, ConsoleColor? fg)
    {
        if (_useBuffer)
        {
            _frameBuffer!.SetCell(x, y, ch, fg);
            return;
        }

        Console.SetCursorPosition(x, y);
        var prevFg = Console.ForegroundColor;
        if (fg.HasValue)
        {
            Console.ForegroundColor = fg.Value;
        }
        Console.Write(ch);
        if (fg.HasValue)
        {
            Console.ForegroundColor = prevFg;
        }
    }

    private void EnsureWindowSize()
    {
        try
        {
            if (Console.WindowWidth < PlayfieldWidth || Console.WindowHeight < TotalHeight + 2)
            {
                Console.SetWindowSize(
                    Math.Max(Console.WindowWidth, PlayfieldWidth),
                    Math.Max(Console.WindowHeight, TotalHeight + 2));
            }
            if (Console.BufferWidth < PlayfieldWidth || Console.BufferHeight < TotalHeight + 2)
            {
                Console.SetBufferSize(
                    Math.Max(Console.BufferWidth, PlayfieldWidth),
                    Math.Max(Console.BufferHeight, TotalHeight + 2));
            }
        }
        catch
        {
        }
    }

    private void RestoreHudBeforeExit()
    {
        FlushIfNeeded();
        Console.CursorVisible = true;
    }

    private static void TryBeep(int freq, int ms)
    {
        try
        {
            Console.Beep(freq, ms);
        }
        catch
        {
        }
    }
}
