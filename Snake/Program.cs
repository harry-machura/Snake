// Passe das Namespace der Resources ggf. an:
using Snake.Properties;

using ManagedBass;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Text;
using System.Threading;
using System.Reflection;
using System.Runtime.InteropServices;
class Program
{
    // ===== Snake-Config =====
    static readonly Random Rng = new Random();

    // ===== Musik / Settings =====
    static bool MusicEnabled = true;
    static int _musicHandle = 0;
    static string? _tempXmPath = null;
    static string? _selectedSongKey = null;  // Resource-Key
    static string? _selectedSongTitle = null; // Anzeige-Name
    static List<(string title, string key)> _songs = new();

    // ===== Highscores =====
    static readonly string HighscoreDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnakeConsole");
    static readonly string HighscoreFile = Path.Combine(HighscoreDir, "highscores.txt");
    static readonly int HighscoreKeep = 10;
    static List<int> Highscores = new();

    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        // 1) Highscores laden
        LoadHighscores();

        // 2) Songs aus Resources auflisten
        DiscoverSongsFromResources();
        if (_songs.Count > 0)
        {
            // Standard: erster Song
            _selectedSongTitle = _songs[0].title;
            _selectedSongKey = _songs[0].key;
        }

        // 3) Bass initialisieren (optional schon hier, sonst beim Starten der Musik)
        TryInitBass();

        // 4) Hauptmenü-Schleife
        while (true)
        {
            var choice = ShowMainMenu();
            switch (choice)
            {
                case "1": // Spielen
                    RunGameUntilExitToMenu();
                    break;

                case "2": // Einstellungen
                    ShowSettingsMenu();
                    break;

                case "3": // Highscores
                    ShowHighscores();
                    break;

                case "4": // Exit
                    CleanupMusicAndExit();
                    return;

                default:
                    // ignorieren
                    break;
            }
        }
    }

    // ===== Hauptmenü =====
    static string ShowMainMenu()
    {
        Console.Clear();
        Console.CursorVisible = true;

        Console.WriteLine("====== S N A K E  (Console) ======");
        Console.WriteLine();
        Console.WriteLine(" 1) Spielen");
        Console.WriteLine(" 2) Einstellungen");
        Console.WriteLine(" 3) Highscores");
        Console.WriteLine(" 4) Exit");
        Console.WriteLine();
        Console.WriteLine($" Musik: [{(MusicEnabled ? 'X' : ' ')}]  Song: {(_selectedSongTitle ?? "-")}");
        Console.WriteLine();
        Console.Write(" Auswahl (1-4): ");

        var key = Console.ReadKey(true);
        Console.WriteLine(key.KeyChar);
        return key.KeyChar.ToString();
    }

    // ===== Einstellungen =====
    static void ShowSettingsMenu()
    {
        while (true)
        {
            Console.Clear();
            Console.CursorVisible = true;

            Console.WriteLine("====== Einstellungen ======");
            Console.WriteLine();
            Console.WriteLine($" 1) Musik an/aus   [{(MusicEnabled ? 'X' : ' ')}]");
            Console.WriteLine(" 2) Song auswählen");
            Console.WriteLine(" 3) Zurück");
            Console.WriteLine();
            Console.Write(" Auswahl: ");

            var k = Console.ReadKey(true).KeyChar;
            Console.WriteLine(k);

            if (k == '1')
            {
                ToggleMusic();
            }
            else if (k == '2')
            {
                ChooseSongMenu();
            }
            else if (k == '3' || k == (char)27)
            {
                return;
            }
        }
    }

    static void ToggleMusic()
    {
        MusicEnabled = !MusicEnabled;
        if (!MusicEnabled)
        {
            // Pause/Stop
            if (_musicHandle != 0) Bass.ChannelPause(_musicHandle);
        }
        else
        {
            // Starten/Weiter
            EnsureMusicPlaying();
        }
    }

    static void ChooseSongMenu()
    {
        if (_songs.Count == 0)
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
            for (int i = 0; i < _songs.Count; i++)
            {
                var s = _songs[i];
                bool sel = (s.key == _selectedSongKey);
                Console.WriteLine($" {i + 1,2}) {(sel ? ">" : " ")} {s.title}");
            }
            Console.WriteLine();
            Console.WriteLine("  0) Zurück");
            Console.WriteLine();
            Console.Write(" Auswahl (Nummer): ");

            var input = Console.ReadLine();
            if (int.TryParse(input, out int idx))
            {
                if (idx == 0) return;
                if (idx >= 1 && idx <= _songs.Count)
                {
                    var s = _songs[idx - 1];
                    _selectedSongTitle = s.title;
                    _selectedSongKey = s.key;

                    // Wenn Musik aktiv: neu laden/abspielen
                    if (MusicEnabled) EnsureMusicPlaying(reload: true);

                    Console.WriteLine($"\nAusgewählt: {s.title}  (Beliebige Taste …)");
                    Console.ReadKey(true);
                    return;
                }
            }
        }
    }

    // ===== Spiel-Runde =====
    static void RunGameUntilExitToMenu()
    {
        // Musik sicherstellen, falls aktiviert
        if (MusicEnabled) EnsureMusicPlaying();

        while (true)
        {
            var game = new SnakeGame(Rng);
            int score = game.PlayRound();
            if (score < 0) // ESC -> zurück
                return;

            SaveScore(score);

            Console.SetCursorPosition(0, SnakeGame.RequiredHeight + 2);
            Console.ResetColor();
            Console.WriteLine($"Score gespeichert: {score}.  (Enter = neue Runde, ESC = zurück zum Menü)");
            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.Escape) return;
        }
    }

    // ===== Musik: Start/Wechsel/Stop =====
    static void EnsureMusicPlaying(bool reload = false)
    {
        if (!MusicEnabled) return;
        if (_selectedSongKey == null) return;

        if (reload && _musicHandle != 0)
        {
            Bass.ChannelStop(_musicHandle);
            Bass.MusicFree(_musicHandle);
            _musicHandle = 0;
        }

        if (_musicHandle != 0)
        {
            // bereits geladen → weiter spielen
            Bass.ChannelPlay(_musicHandle, false);
            return;
        }

        // Laden & starten
        if (StartMusicFromResourceKey(_selectedSongKey))
        {
            Bass.ChannelSetAttribute(_musicHandle, ChannelAttribute.Volume, 0.85f);
            Bass.ChannelPlay(_musicHandle, false);
        }
    }

    static bool StartMusicFromResourceKey(string resourceKey)
    {
        try
        {
            if (!TryInitBass()) return false;

            // Stream aus ResourceManager holen:
            using Stream? resStream = Resources.ResourceManager.GetStream(resourceKey, CultureInfo.CurrentUICulture);
            if (resStream == null)
            {
                Debug.WriteLine($"Ressource '{resourceKey}' nicht gefunden.");
                return false;
            }

            _tempXmPath = Path.Combine(Path.GetTempPath(), $"snake_song_{resourceKey}.xm");
            using (var fs = new FileStream(_tempXmPath, FileMode.Create, FileAccess.Write))
                resStream.CopyTo(fs);

            var flags = BassFlags.Loop | BassFlags.MusicRamp | BassFlags.Prescan;
            _musicHandle = Bass.MusicLoad(_tempXmPath, 0, 0, flags, 0);
            if (_musicHandle == 0)
            {
                Debug.WriteLine("MusicLoad fehlgeschlagen: " + Bass.LastError);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine("StartMusicFromResourceKey() Fehler: " + ex.Message);
            return false;
        }
    }


    static bool _bassReady = false;
    static string? _extractedBassPath;

    static bool TryInitBass()
    {
        if (_bassReady) return true;

        string logicalName, fileName, rid;
        if (OperatingSystem.IsWindows())
        {
            rid = "win-x64";
            logicalName = "win-x64/bass.dll";
            fileName = "bass.dll";
        }
        else if (OperatingSystem.IsLinux())
        {
            rid = "linux-x64";
            logicalName = "linux-x64/libbass.so";
            fileName = "libbass.so";
        }
        else
        {
            Console.WriteLine("OS nicht unterstützt für BASS.");
            return false;
        }

        // 1) Ressource prüfen (Diagnose)
#if DEBUG
        var names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
        // Optional: Console.WriteLine(string.Join("\n", names));
        if (!Array.Exists(names, n => n.Equals(logicalName, StringComparison.Ordinal)))
        {
            Console.WriteLine($"Embedded Resource fehlt: '{logicalName}'");
            return false;
        }
#endif

        // 2) Nach %TEMP% extrahieren
        var tempDir = Path.Combine(Path.GetTempPath(), "SnakeNative", rid);
        Directory.CreateDirectory(tempDir);
        _extractedBassPath = Path.Combine(tempDir, fileName);

        if (!File.Exists(_extractedBassPath))
        {
            using Stream? res = Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName);
            if (res == null)
            {
                Console.WriteLine($"Ressource '{logicalName}' nicht gefunden.");
                return false;
            }
            using var fs = new FileStream(_extractedBassPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            res.CopyTo(fs);
        }

        // 3) Optional für Linux: Ausführbar machen (meist nicht nötig, aber schadet nicht)
        if (OperatingSystem.IsLinux())
        {
            try
            {
                // chmod 755
                System.Diagnostics.Process.Start("chmod", $"+x \"{_extractedBassPath}\"")?.WaitForExit();
            }
            catch { /* ignore */ }
        }

        // 4) DllImport-Resolver setzen, damit "bass" -> unser Pfad gemappt wird
        NativeLibrary.SetDllImportResolver(typeof(ManagedBass.Bass).Assembly, (name, asm, searchPath) =>
        {
            if (name.Equals("bass", StringComparison.OrdinalIgnoreCase))
                return NativeLibrary.Load(_extractedBassPath!);
            return IntPtr.Zero;
        });

        // 5) Initialisieren
        if (!ManagedBass.Bass.Init(-1, 44100, ManagedBass.DeviceInitFlags.Default))
        {
            Console.WriteLine("Bass.Init fehlgeschlagen: " + ManagedBass.Bass.LastError);
            return false;
        }

        _bassReady = true;
        return true;
    }
    static void CleanupMusicAndExit()
    {
        try
        {
            if (_musicHandle != 0)
            {
                Bass.ChannelStop(_musicHandle);
                Bass.MusicFree(_musicHandle);
                _musicHandle = 0;
            }
        }
        finally
        {
            if (_bassReady) Bass.Free();
            if (!string.IsNullOrEmpty(_tempXmPath) && File.Exists(_tempXmPath))
            {
                try { File.Delete(_tempXmPath); } catch { }
            }
        }
    }


    // ===== Songs aus Resources entdecken =====
    static void DiscoverSongsFromResources()
    {
        _songs.Clear();
        ResourceManager rm = Resources.ResourceManager;
        var set = rm.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
        foreach (DictionaryEntry e in set)
        {
            string key = e.Key.ToString()!;
            // Wir versuchen Streams zu bekommen (für eingebettete Dateien)
            // Filter: Nur Schlüssel, für die ein Stream existiert
            try
            {
                using var s = rm.GetStream(key, CultureInfo.CurrentUICulture);
                if (s != null && s.Length > 0)
                {
                    string title = key; // optional schöner machen
                    _songs.Add((title, key));
                }
            }
            catch { /* ignorieren */ }
        }

        // Optional: alphabetisch
        _songs.Sort((a, b) => string.Compare(a.title, b.title, StringComparison.OrdinalIgnoreCase));
    }

    // ===== Highscores =====
    static void LoadHighscores()
    {
        Highscores.Clear();
        try
        {
            if (File.Exists(HighscoreFile))
            {
                foreach (var line in File.ReadAllLines(HighscoreFile))
                {
                    if (int.TryParse(line.Trim(), out int s)) Highscores.Add(s);
                }
                Highscores.Sort((a, b) => b.CompareTo(a)); // absteigend
                if (Highscores.Count > HighscoreKeep) Highscores.RemoveRange(HighscoreKeep, Highscores.Count - HighscoreKeep);
            }
        }
        catch { /* egal */ }
    }

    static void SaveScore(int score)
    {
        try
        {
            Directory.CreateDirectory(HighscoreDir);
            Highscores.Add(score);
            Highscores.Sort((a, b) => b.CompareTo(a));
            if (Highscores.Count > HighscoreKeep) Highscores.RemoveRange(HighscoreKeep, Highscores.Count - HighscoreKeep);
            File.WriteAllLines(HighscoreFile, Highscores.ConvertAll(s => s.ToString()));
        }
        catch { /* egal */ }
    }

    static void ShowHighscores()
    {
        Console.Clear();
        Console.CursorVisible = true;
        Console.WriteLine("====== Highscores ======\n");
        if (Highscores.Count == 0)
        {
            Console.WriteLine("Noch keine Highscores.");
        }
        else
        {
            int rank = 1;
            foreach (var s in Highscores)
            {
                Console.WriteLine($"{rank,2}.  {s}");
                rank++;
            }
        }
        Console.WriteLine("\nBeliebige Taste = zurück");
        Console.ReadKey(true);
    }
}
