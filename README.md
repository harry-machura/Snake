# Snake (Console Edition)

Eine klassische Snake-Variante für die Konsole mit modernen Komfortfunktionen: einstellbare Schwierigkeitsgrade, Hindernis-Layouts, Power-ups sowie integrierter Musik-Player auf Basis von [ManagedBass](https://github.com/ManagedBass/ManagedBass).

## Features
- Drei Schwierigkeitsgrade mit jeweils eigener Geschwindigkeitsspanne und Hindernis-Layout.
- Bonus- und Verlangsamungs-Food, das Punkte- bzw. Tempo-Booster auslöst.
- Pausierbarer Spielablauf (Taste `P`) inklusive HUD-Legende für alle Items.
- Integrierter Musikplayer mit auswählbaren Songs aus den Projektressourcen.
- Highscore-Tabelle mit Speicherung von Punktzahl, Modus und Zeitstempel unter `%LOCALAPPDATA%/SnakeConsole` (Windows) bzw. `$XDG_DATA_HOME`/`~/.local/share` (Linux).

## Steuerung
| Taste | Funktion |
| ----- | -------- |
| `W` / `↑` | Schlange nach oben bewegen |
| `A` / `←` | Schlange nach links bewegen |
| `S` / `↓` | Schlange nach unten bewegen |
| `D` / `→` | Schlange nach rechts bewegen |
| `P` | Spiel pausieren / fortsetzen |
| `ESC` | Runde abbrechen und ins Menü zurückkehren |
| `ENTER` | Nach Game Over eine neue Runde starten |

## Voraussetzungen
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download) (für Build und Ausführung aus dem Quellcode).
- Eine Konsole, die UTF-8 und ANSI-Farben unterstützt.
- Für Soundausgabe: funktionierendes Audio-Subsystem (unter Linux i. d. R. ALSA oder PulseAudio).

Alle notwendigen nativen `bass`-Bibliotheken für Windows x64 und Linux x64 sind im Projekt eingebettet. Eine Internetverbindung wird nur für das initiale Wiederherstellen der NuGet-Pakete benötigt.

## Projektstruktur
```
Snake.sln
└── Snake/
    ├── Program.cs           # Einstiegspunkt, richtet Dienste ein und startet die App
    ├── SnakeApp.cs          # Menüführung und Orchestrierung
    ├── SnakeGame.cs         # Spiellogik, Rendering, Power-ups, Hindernisse
    ├── AudioService.cs      # Wiedergabe & Verwaltung der Ressource-basierten Musik
    ├── HighscoreService.cs  # Persistente Highscores im Benutzerprofil
    ├── SnakeDifficulty.cs   # Aufzählung der Schwierigkeitsgrade
    ├── SnakeRoundResult.cs  # Rückgabewerte für abgeschlossene Spielrunden
    ├── Properties/Resources # Musikdateien & Resource-Designer
    └── runtimes/...         # eingebettete native BASS-Bibliotheken für Win/Linux
```

## Build & Ausführung
### 1. Repository klonen
```bash
git clone https://github.com/<dein-account>/Snake.git
cd Snake
```

### 2. NuGet-Pakete wiederherstellen
```bash
dotnet restore
```

### 3. Entwicklungsversion starten
```bash
dotnet run --project Snake/Snake.csproj
```

### 4. Build (Debug)
```bash
dotnet build Snake/Snake.csproj
```

## Veröffentlichung als Single-File-Build
Das Projekt ist für selbstenthaltende Single-File-Pakete konfiguriert. Mit folgenden Kommandos erstellst du lauffähige Binaries inklusive eingebetteter nativer Bibliotheken:

### Windows (x64)
```bash
dotnet publish Snake/Snake.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true
```
Das Ergebnis befindet sich im Ordner `Snake/bin/Release/net8.0/win-x64/publish/` als `Snake.exe`.

### Linux (x64)
```bash
dotnet publish Snake/Snake.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true
```
Das Ergebnis liegt im Ordner `Snake/bin/Release/net8.0/linux-x64/publish/` als ausführbare Datei `Snake`.

> Tipp: Falls die veröffentlichte Linux-Datei nicht ausführbar ist, setze die Berechtigung mit `chmod +x Snake`.

## Highscores & Speicherdaten
- Windows: `%LOCALAPPDATA%/SnakeConsole/highscores.txt`
- Linux: `~/.local/share/SnakeConsole/highscores.txt` (wenn `XDG_DATA_HOME` nicht gesetzt ist)

Die Datei kann gefahrlos gelöscht werden, um die Bestenliste zurückzusetzen.

## Musik hinzufügen
Lege neue `.xm`-, `.mod`- oder andere Tracker-Dateien in `Snake/Resources/` ab und referenziere sie in `Properties/Resources.resx`. Beim nächsten Start werden sie automatisch im Musikmenü angezeigt.

## Fehlerbehebung
- **Kein Ton unter Linux:** Stelle sicher, dass die Pakete `libasound2` bzw. PulseAudio installiert sind. Starte das Spiel in einem Terminal, um etwaige Fehlermeldungen zu sehen.
- **Flackernde Ausgabe:** Verwende ein Terminal mit fester Breite und deaktiviere Zeilenumbruch. Alternativ kann die Fenstergröße manuell erhöht werden.
- **dotnet-Befehle fehlen:** Prüfe mit `dotnet --version`, ob das .NET 8 SDK korrekt installiert wurde.

## Lizenz
Dieses Projekt steht unter der [MIT-Lizenz](LICENSE). Beachte zusätzlich die Lizenzbedingungen der verwendeten Bibliotheken (ManagedBass und BASS).
