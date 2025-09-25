using System;
using System.Collections.Generic;
using System.IO;

namespace Snake;

internal sealed class HighscoreService
{
    private readonly string _highscoreDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnakeConsole");
    private readonly string _highscoreFile;
    private const int HighscoreKeep = 10;
    private readonly List<int> _highscores = new();

    public HighscoreService()
    {
        _highscoreFile = Path.Combine(_highscoreDir, "highscores.txt");
    }

    public IReadOnlyList<int> Highscores => _highscores;

    public void Load()
    {
        _highscores.Clear();
        try
        {
            if (File.Exists(_highscoreFile))
            {
                foreach (var line in File.ReadAllLines(_highscoreFile))
                {
                    if (int.TryParse(line.Trim(), out int score))
                    {
                        _highscores.Add(score);
                    }
                }

                SortAndTrim();
            }
        }
        catch
        {
            // Fehler beim Laden ignorieren, Spiel kann trotzdem starten.
        }
    }

    public void SaveScore(int score)
    {
        try
        {
            Directory.CreateDirectory(_highscoreDir);
            _highscores.Add(score);
            SortAndTrim();
            var lines = _highscores.ConvertAll(static s => s.ToString());
            File.WriteAllLines(_highscoreFile, lines);
        }
        catch
        {
            // Schreiben der Highscores ist optional.
        }
    }

    private void SortAndTrim()
    {
        _highscores.Sort((a, b) => b.CompareTo(a));
        if (_highscores.Count > HighscoreKeep)
        {
            _highscores.RemoveRange(HighscoreKeep, _highscores.Count - HighscoreKeep);
        }
    }
}
