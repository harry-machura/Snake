using System;

namespace Snake;

internal readonly record struct HighscoreEntry(int Score, SnakeDifficulty Difficulty, DateTime Timestamp)
{
    public string ToPersistedString() => string.Join(";", Score, Difficulty, Timestamp.ToString("o"));
}
