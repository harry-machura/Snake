using System;

namespace Snake;

internal sealed class SnakeApp
{
    private readonly AudioService _audioService;
    private readonly HighscoreService _highscoreService;
    private readonly SnakeGame _snakeGame;
    private SnakeDifficulty _selectedDifficulty = SnakeDifficulty.Normal;

    public SnakeApp(AudioService audioService, HighscoreService highscoreService, SnakeGame snakeGame)
    {
        _audioService = audioService;
        _highscoreService = highscoreService;
        _snakeGame = snakeGame;
    }

    public void Run()
    {
        _highscoreService.Load();
        _audioService.DiscoverSongsFromResources();

        while (true)
        {
            MainMenuChoice choice = ShowMainMenu();
            switch (choice)
            {
                case MainMenuChoice.Play:
                    RunGameUntilExitToMenu();
                    break;
                case MainMenuChoice.Settings:
                    ShowSettingsMenu();
                    break;
                case MainMenuChoice.Highscores:
                    ShowHighscores();
                    break;
                case MainMenuChoice.Exit:
                    return;
            }
        }
    }

    private MainMenuChoice ShowMainMenu()
    {
        while (true)
        {
            Console.Clear();
            Console.CursorVisible = true;

            Console.WriteLine("====== S N A K E ======");
            Console.WriteLine();
            Console.WriteLine(" 1) Spielen");
            Console.WriteLine(" 2) Einstellungen");
            Console.WriteLine(" 3) Highscores");
            Console.WriteLine(" 4) Exit");
            Console.WriteLine();
            Console.WriteLine($" Musik: [{(_audioService.MusicEnabled ? 'X' : ' ')}]  Song: {(_audioService.SelectedSongTitle ?? "-")}");
            Console.WriteLine($" Modus: {FormatDifficulty(_selectedDifficulty)}");
            Console.WriteLine();
            Console.Write(" Auswahl (1-4): ");

            var keyInfo = Console.ReadKey(true);
            Console.WriteLine(keyInfo.KeyChar);

            switch (keyInfo.KeyChar)
            {
                case '1':
                    return MainMenuChoice.Play;
                case '2':
                    return MainMenuChoice.Settings;
                case '3':
                    return MainMenuChoice.Highscores;
                case '4':
                    return MainMenuChoice.Exit;
            }
        }
    }

    private void ShowSettingsMenu()
    {
        while (true)
        {
            Console.Clear();
            Console.CursorVisible = true;

            Console.WriteLine("====== Einstellungen ======");
            Console.WriteLine();
            Console.WriteLine($" 1) Musik an/aus   [{(_audioService.MusicEnabled ? 'X' : ' ')}]");
            Console.WriteLine(" 2) Song auswählen");
            Console.WriteLine($" 3) Schwierigkeit: {FormatDifficulty(_selectedDifficulty)}");
            Console.WriteLine(" 4) Zurück");
            Console.WriteLine();
            Console.Write(" Auswahl: ");

            var key = Console.ReadKey(true).KeyChar;
            Console.WriteLine(key);

            if (key == '1')
            {
                _audioService.ToggleMusic();
            }
            else if (key == '2')
            {
                ShowSongSelectionMenu();
            }
            else if (key == '3')
            {
                CycleDifficulty();
            }
            else if (key == '4' || key == (char)27)
            {
                return;
            }
        }
    }

    private void ShowSongSelectionMenu()
    {
        if (_audioService.Songs.Count == 0)
        {
            Console.WriteLine("\nKeine Songs in Resources gefunden. Beliebige Taste…");
            Console.ReadKey(true);
            return;
        }

        while (true)
        {
            Console.Clear();
            Console.WriteLine("====== Song auswählen ======");
            Console.WriteLine();

            for (int i = 0; i < _audioService.Songs.Count; i++)
            {
                var song = _audioService.Songs[i];
                bool selected = song.key == _audioService.SelectedSongKey;
                Console.WriteLine($" {i + 1,2}) {(selected ? ">" : " ")} {song.title}");
            }

            Console.WriteLine();
            Console.WriteLine("  0) Zurück");
            Console.WriteLine();
            Console.Write(" Auswahl (Nummer): ");

            string? input = Console.ReadLine();
            if (int.TryParse(input, out int idx))
            {
                if (idx == 0)
                {
                    return;
                }

                if (idx >= 1 && idx <= _audioService.Songs.Count)
                {
                    if (_audioService.TrySelectSong(idx - 1))
                    {
                        Console.WriteLine($"\nAusgewählt: {_audioService.SelectedSongTitle}  (Beliebige Taste …)");
                        Console.ReadKey(true);
                        return;
                    }
                }
            }
        }
    }

    private void ShowHighscores()
    {
        Console.Clear();
        Console.CursorVisible = true;
        Console.WriteLine("====== Highscores ======\n");

        if (_highscoreService.Highscores.Count == 0)
        {
            Console.WriteLine("Noch keine Highscores.");
        }
        else
        {
            Console.WriteLine(" Rang  Score  Modus   Zeitpunkt");
            Console.WriteLine(" ----  -----  ------  ---------");
            int rank = 1;
            foreach (HighscoreEntry entry in _highscoreService.Highscores)
            {
                string difficulty = FormatDifficulty(entry.Difficulty);
                string time = entry.Timestamp == DateTime.MinValue
                    ? "-"
                    : entry.Timestamp.ToLocalTime().ToString("g");
                Console.WriteLine($"{rank,2}.  {entry.Score,5}  {difficulty,-6}  {time}");
                rank++;
            }
        }

        Console.WriteLine("\nBeliebige Taste = zurück");
        Console.ReadKey(true);
    }

    private void RunGameUntilExitToMenu()
    {
        if (_audioService.MusicEnabled)
        {
            _audioService.EnsureMusicPlaying();
        }

        while (true)
        {
            SnakeRoundResult result = _snakeGame.PlayRound(_selectedDifficulty);
            if (result.ExitToMenu)
            {
                return;
            }

            _highscoreService.SaveScore(result.Score, _selectedDifficulty);

            Console.SetCursorPosition(0, _snakeGame.Height + 2);
            Console.ResetColor();
            Console.WriteLine($"Score gespeichert ({FormatDifficulty(_selectedDifficulty)}): {result.Score}.  (Enter = neue Runde, ESC = zurück zum Menü)");

            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.Escape)
            {
                return;
            }
        }
    }

    private void CycleDifficulty()
    {
        _selectedDifficulty = _selectedDifficulty switch
        {
            SnakeDifficulty.Easy => SnakeDifficulty.Normal,
            SnakeDifficulty.Normal => SnakeDifficulty.Hard,
            _ => SnakeDifficulty.Easy,
        };

        Console.WriteLine($"\nNeue Schwierigkeit: {FormatDifficulty(_selectedDifficulty)} (Beliebige Taste …)");
        Console.ReadKey(true);
    }

    private static string FormatDifficulty(SnakeDifficulty difficulty) => difficulty switch
    {
        SnakeDifficulty.Easy => "Leicht",
        SnakeDifficulty.Hard => "Schwer",
        _ => "Normal",
    };

    private enum MainMenuChoice
    {
        Play,
        Settings,
        Highscores,
        Exit
    }
}
