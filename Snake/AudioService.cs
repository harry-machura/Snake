using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using ManagedBass;
using Snake.Properties;

namespace Snake;

internal sealed class AudioService : IDisposable
{
    private bool _musicEnabled = true;
    private int _musicHandle;
    private string? _tempXmPath;
    private string? _selectedSongKey;
    private string? _selectedSongTitle;
    private readonly List<(string title, string key)> _songs = new();

    private bool _bassReady;
    private string? _extractedBassPath;

    public bool MusicEnabled => _musicEnabled;
    public string? SelectedSongTitle => _selectedSongTitle;
    public string? SelectedSongKey => _selectedSongKey;
    public IReadOnlyList<(string title, string key)> Songs => _songs;

    public void DiscoverSongsFromResources()
    {
        string? previouslySelectedKey = _selectedSongKey;

        _songs.Clear();

        ResourceManager manager = Resources.ResourceManager;
        var set = manager.GetResourceSet(CultureInfo.CurrentUICulture, true, true);
        foreach (DictionaryEntry entry in set)
        {
            string key = entry.Key.ToString()!;
            try
            {
                using var stream = manager.GetStream(key, CultureInfo.CurrentUICulture);
                if (stream != null && stream.Length > 0)
                {
                    string title = key;
                    _songs.Add((title, key));
                }
            }
            catch
            {
                // Ignorieren – einige Resource-Einträge liefern evtl. keine Streams.
            }
        }

        _songs.Sort((a, b) => string.Compare(a.title, b.title, StringComparison.OrdinalIgnoreCase));

        if (_songs.Count > 0)
        {
            int index = _songs.FindIndex(s => s.key == previouslySelectedKey);
            if (index < 0)
            {
                index = 0;
            }

            var song = _songs[index];
            _selectedSongTitle = song.title;
            _selectedSongKey = song.key;
        }
        else
        {
            _selectedSongTitle = null;
            _selectedSongKey = null;
        }
    }

    public void ToggleMusic()
    {
        _musicEnabled = !_musicEnabled;
        if (!_musicEnabled)
        {
            if (_musicHandle != 0)
            {
                Bass.ChannelPause(_musicHandle);
            }
        }
        else
        {
            EnsureMusicPlaying();
        }
    }

    public bool TrySelectSong(int index)
    {
        if (index < 0 || index >= _songs.Count)
        {
            return false;
        }

        var song = _songs[index];
        _selectedSongTitle = song.title;
        _selectedSongKey = song.key;

        if (_musicEnabled)
        {
            EnsureMusicPlaying(reload: true);
        }

        return true;
    }

    public void EnsureMusicPlaying(bool reload = false)
    {
        if (!_musicEnabled || _selectedSongKey == null)
        {
            return;
        }

        if (reload && _musicHandle != 0)
        {
            Bass.ChannelStop(_musicHandle);
            Bass.MusicFree(_musicHandle);
            _musicHandle = 0;
        }

        if (_musicHandle != 0)
        {
            Bass.ChannelPlay(_musicHandle, false);
            return;
        }

        if (StartMusicFromResourceKey(_selectedSongKey))
        {
            Bass.ChannelSetAttribute(_musicHandle, ChannelAttribute.Volume, 0.85f);
            Bass.ChannelPlay(_musicHandle, false);
        }
    }

    private bool StartMusicFromResourceKey(string resourceKey)
    {
        try
        {
            if (!TryInitBass())
            {
                return false;
            }

            using Stream? resourceStream = Resources.ResourceManager.GetStream(resourceKey, CultureInfo.CurrentUICulture);
            if (resourceStream == null)
            {
                Debug.WriteLine($"Ressource '{resourceKey}' nicht gefunden.");
                return false;
            }

            _tempXmPath = Path.Combine(Path.GetTempPath(), $"snake_song_{resourceKey}.xm");
            using (var fs = new FileStream(_tempXmPath, FileMode.Create, FileAccess.Write))
            {
                resourceStream.CopyTo(fs);
            }

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

    private bool TryInitBass()
    {
        if (_bassReady)
        {
            return true;
        }

        string logicalName;
        string fileName;
        string rid;

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

#if DEBUG
        var names = Assembly.GetExecutingAssembly().GetManifestResourceNames();
        if (!Array.Exists(names, n => n.Equals(logicalName, StringComparison.Ordinal)))
        {
            Console.WriteLine($"Embedded Resource fehlt: '{logicalName}'");
            return false;
        }
#endif

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

        if (OperatingSystem.IsLinux())
        {
            try
            {
                Process.Start("chmod", $"+x \"{_extractedBassPath}\"")?.WaitForExit();
            }
            catch
            {
                // Ignorieren – fehlende chmod-Unterstützung ist unkritisch.
            }
        }

        NativeLibrary.SetDllImportResolver(typeof(Bass).Assembly, (name, asm, searchPath) =>
        {
            if (name.Equals("bass", StringComparison.OrdinalIgnoreCase))
            {
                return NativeLibrary.Load(_extractedBassPath!);
            }

            return IntPtr.Zero;
        });

        if (!Bass.Init(-1, 44100, DeviceInitFlags.Default))
        {
            Console.WriteLine("Bass.Init fehlgeschlagen: " + Bass.LastError);
            return false;
        }

        _bassReady = true;
        return true;
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private void Cleanup()
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
            if (_bassReady)
            {
                Bass.Free();
            }

            if (!string.IsNullOrEmpty(_tempXmPath) && File.Exists(_tempXmPath))
            {
                try
                {
                    File.Delete(_tempXmPath);
                }
                catch
                {
                    // Ignorieren – temporäre Datei kann beim nächsten Start bereinigt werden.
                }
            }
        }
    }
}
