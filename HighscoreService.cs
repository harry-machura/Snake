using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;

namespace Snake;

internal sealed class HighscoreService
{
    private readonly string _highscoreDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnakeConsole");
    private readonly string _highscoreFile;
    private const int HighscoreKeep = 10;
    private readonly List<HighscoreEntry> _highscores = new();

    public HighscoreService()
    {
        _highscoreFile = Path.Combine(_highscoreDir, "highscores.txt");
    }

    public IReadOnlyList<HighscoreEntry> Highscores => _highscores;

    public void Load()
    {
        _highscores.Clear();
        try
        {
            if (File.Exists(_highscoreFile))
            {
                foreach (var line in File.ReadAllLines(_highscoreFile))
                {
                    ParseLine(line.Trim());
                }

                SortAndTrim();
            }
        }
        catch
        {
            // Fehler beim Laden ignorieren, Spiel kann trotzdem starten.
        }
    }

    public void SaveScore(int score, SnakeDifficulty difficulty)
    {
        try
        {
            Directory.CreateDirectory(_highscoreDir);
            _highscores.Add(new HighscoreEntry(score, difficulty, DateTime.UtcNow));
            SortAndTrim();
            var lines = _highscores.ConvertAll(static entry => entry.ToPersistedString());
            File.WriteAllLines(_highscoreFile, lines);
        }
        catch
        {
            // Schreiben der Highscores ist optional.
        }
    }

    private void SortAndTrim()
    {
        _highscores.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (_highscores.Count > HighscoreKeep)
        {
            _highscores.RemoveRange(HighscoreKeep, _highscores.Count - HighscoreKeep);
        }
    }

    private void ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        // Neues Format: score;difficulty;timestamp
        string[] parts = line.Split(';');
        if (parts.Length >= 3 &&
            int.TryParse(parts[0], out int score) &&
            Enum.TryParse(parts[1], out SnakeDifficulty difficulty) &&
            DateTime.TryParse(parts[2], null, DateTimeStyles.RoundtripKind, out DateTime timestamp))
        {
            _highscores.Add(new HighscoreEntry(score, difficulty, timestamp));
            return;
        }

        // Legacy-Einträge nur mit Punktzahl
        if (int.TryParse(parts[0], out int legacyScore))
        {
            _highscores.Add(new HighscoreEntry(legacyScore, SnakeDifficulty.Normal, DateTime.MinValue));
        }
    }
}
