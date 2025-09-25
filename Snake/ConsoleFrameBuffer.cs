using System;

internal sealed class ConsoleFrameBuffer
{
    private readonly int _width;
    private readonly int _height;
    private readonly Cell[,] _cells;
    private readonly Cell[,] _backBuffer;
    private readonly ConsoleColor _defaultForeground;
    private readonly ConsoleColor _defaultBackground;

    private struct Cell
    {
        public char Char;
        public ConsoleColor? Foreground;
        public ConsoleColor? Background;
    }

    private ConsoleFrameBuffer(int width, int height)
    {
        _width = width;
        _height = height;
        _cells = new Cell[width, height];
        _backBuffer = new Cell[width, height];
        _defaultForeground = Console.ForegroundColor;
        _defaultBackground = Console.BackgroundColor;
        Clear(' ', null, null);
    }

    public static ConsoleFrameBuffer? TryCreate(int width, int height)
    {
        try
        {
            if (Console.IsOutputRedirected)
            {
                return null;
            }

            int originalLeft = Console.CursorLeft;
            int originalTop = Console.CursorTop;

            // Probes whether the terminal allows addressing the requested area
            Console.SetCursorPosition(Math.Min(width - 1, Math.Max(0, Console.BufferWidth - 1)),
                                       Math.Min(height - 1, Math.Max(0, Console.BufferHeight - 1)));
            Console.SetCursorPosition(originalLeft, originalTop);

            return new ConsoleFrameBuffer(width, height);
        }
        catch
        {
            return null;
        }
    }

    public void Clear(char fillChar = ' ', ConsoleColor? fg = null, ConsoleColor? bg = null)
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                _cells[x, y].Char = fillChar;
                _cells[x, y].Foreground = fg;
                _cells[x, y].Background = bg;
                _backBuffer[x, y].Char = fillChar;
                _backBuffer[x, y].Foreground = fg;
                _backBuffer[x, y].Background = bg;
            }
        }
    }

    public void SetCell(int x, int y, char ch, ConsoleColor? fg = null, ConsoleColor? bg = null)
    {
        if ((uint)x >= (uint)_width || (uint)y >= (uint)_height)
        {
            return;
        }

        _cells[x, y].Char = ch;
        _cells[x, y].Foreground = fg;
        _cells[x, y].Background = bg;
    }

    public void FillHorizontal(int x, int y, int length, char ch, ConsoleColor? fg = null, ConsoleColor? bg = null)
    {
        int start = Math.Max(0, x);
        int end = Math.Min(_width, x + length);
        for (int i = start; i < end; i++)
        {
            SetCell(i, y, ch, fg, bg);
        }
    }

    public void Flush()
    {
        var currentFg = Console.ForegroundColor;
        var currentBg = Console.BackgroundColor;

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                ref Cell target = ref _cells[x, y];
                ref Cell previous = ref _backBuffer[x, y];

                if (target.Char == previous.Char && target.Foreground == previous.Foreground && target.Background == previous.Background)
                {
                    continue;
                }

                Console.SetCursorPosition(x, y);

                ConsoleColor fg = target.Foreground ?? _defaultForeground;
                ConsoleColor bg = target.Background ?? _defaultBackground;

                if (currentFg != fg)
                {
                    Console.ForegroundColor = fg;
                    currentFg = fg;
                }

                if (currentBg != bg)
                {
                    Console.BackgroundColor = bg;
                    currentBg = bg;
                }

                Console.Write(target.Char);

                previous.Char = target.Char;
                previous.Foreground = target.Foreground;
                previous.Background = target.Background;
            }
        }

        if (currentFg != _defaultForeground)
        {
            Console.ForegroundColor = _defaultForeground;
        }

        if (currentBg != _defaultBackground)
        {
            Console.BackgroundColor = _defaultBackground;
        }
    }
}
