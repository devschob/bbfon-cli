# BBFon – Mikrofon-Überwachung mit Signal/Telegram/WhatsApp-Benachrichtigung

BBFon überwacht ein Mikrofon des Windows-PCs und sendet eine Nachricht über Signal, Telegram oder WhatsApp, sobald die Lautstärke einen konfigurierbaren Schwellwert überschreitet. Zwischen zwei Nachrichten gilt ein Cooldown-Zeitraum, um Spam zu verhindern.

---

## Inhaltsverzeichnis

1. [Voraussetzungen](#1-voraussetzungen)
2. [Projekt kompilieren](#2-projekt-kompilieren)
3. [Konfiguration (appsettings.json)](#3-konfiguration-appsettingsjson)
4. [Telegram einrichten](#4-telegram-einrichten)
5. [Signal einrichten](#5-signal-einrichten)
6. [WhatsApp einrichten](#6-whatsapp-einrichten)
7. [Starten und Bedienen](#7-starten-und-bedienen)
8. [Projektstruktur](#8-projektstruktur)
9. [Technische Details](#9-technische-details)
10. [Fehlerbehebung](#10-fehlerbehebung)

---

## 1. Voraussetzungen

| Anforderung | Details |
|---|---|
| Betriebssystem | Windows 10 / 11 |
| .NET SDK | .NET 8 SDK ([download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)) |
| Mikrofon | Aufnahmegerät in Windows konfiguriert |
| Für Signal | Java 17+ (für signal-cli) |
| Für Telegram | Internetzugang, Telegram-Account |
| Für WhatsApp | mudslide.exe ([Download](https://github.com/robvanderleek/mudslide/releases)), WhatsApp-Account |
| Für Komprimierung / Kamera | FFmpeg (siehe [FFMPEG.md](FFMPEG.md)) |

---

## 2. Projekt kompilieren

### Normaler Build (benötigt .NET 8 auf dem Zielrechner)

```cmd
cd src\BBFon
dotnet build -c Release
```

### Self-Contained Publish (alles in einer EXE, kein .NET nötig)

```cmd
cd src\BBFon
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Die fertige EXE liegt danach unter:
```
src\BBFon\bin\Release\net8.0-windows\win-x64\publish\BBFon.exe
```

> **Hinweis:** Bei Self-Contained-Publish liegt die `appsettings.json` ebenfalls im `publish`-Ordner und muss dort angepasst werden.

---

## 3. Konfiguration (appsettings.json)

Die `appsettings.json` liegt **neben der EXE** und wird beim Start geladen. Änderungen werden **ohne Neustart** übernommen (Hot Reload).

```json
{
  "Threshold": 300.0,
  "CooldownSeconds": 60,
  "Message": "Lärm erkannt!",
  "Provider": "Telegram",
  "FfmpegPath": "ffmpeg.exe",
  "AudioDevice": "",
  "Startup": {
    "Enabled": false,
    "Message": "ich wache"
  },
  "Analysis": {
    "Enabled": false,
    "WindowSeconds": 10,
    "MinTriggerCount": 3
  },
  "Recording": {
    "Enabled": false,
    "DurationSeconds": 10,
    "MaxFiles": 0,
    "MaxAgeDays": 0,
    "SendAttachments": false
  },
  "Compression": {
    "Enabled": false,
    "Format": "opus",
    "BitrateKbps": 24,
    "KeepWavAudio": false
  },
  "Camera": {
    "Enabled": false,
    "DeviceName": "",
    "Format": "mp4",
    "MuxWithAudio": false,
    "KeepMuxAudio": false,
    "ScaleWidth": 320
  },
  "Battery": {
    "Enabled": false,
    "ThresholdPercent": 20,
    "CheckIntervalSeconds": 60,
    "Message": "Batterie niedrig"
  },
  "Signal": {
    "CliPath": "signal-cli.bat",
    "Sender": "+4912345678",
    "Recipient": "+4987654321"
  },
  "Telegram": {
    "BotToken": "1234567890:ABC-DEIN-TOKEN-HIER",
    "ChatId": "987654321"
  }
}
```

### Alle Felder im Überblick

| Feld | Typ | Beschreibung |
|---|---|---|
| `Threshold` | `float` (0–1000) | Lautstärke-Schwellwert. `0` = Stille, `1000` = maximale Lautstärke. Empfehlung: `20`–`100`. Mit `--calibrate` bestimmen. |
| `CooldownSeconds` | `int` | Mindestabstand in Sekunden zwischen zwei Nachrichten. |
| `Message` | `string` | Text der gesendeten Nachricht. |
| `Provider` | `string` | Aktiver Dienst: `"Telegram"`, `"Signal"` oder `"WhatsApp"` (Groß-/Kleinschreibung egal). |
| `FfmpegPath` | `string` | Pfad zu `ffmpeg.exe`. Relativ zur EXE oder absolut. Wird von Kamera und Komprimierung genutzt. |
| `AudioDevice` | `string` | Name des Mikrofons (Teilstring genügt). Leer = Standard-Mikrofon. Mit `--list-audio` anzeigen. |
| `Startup.Enabled` | `bool` | Beim Start eine Nachricht senden. |
| `Startup.Message` | `string` | Text der Startnachricht. Standard: `"ich wache"`. |
| `Analysis.Enabled` | `bool` | Analyse aktivieren. Bei `false`: jeder einzelne Trigger löst sofort aus. |
| `Analysis.WindowSeconds` | `int` | Beobachtungsfenster in Sekunden. |
| `Analysis.MinTriggerCount` | `int` | Mindestanzahl Trigger innerhalb von `WindowSeconds` für Alarm. |
| `Recording.Enabled` | `bool` | Audio-Aufnahme bei Alarm aktivieren (WAV neben EXE). |
| `Recording.DurationSeconds` | `int` | Maximale Aufnahmedauer in Sekunden. Gilt auch für Kamera. Standard: 10. |
| `Recording.MaxFiles` | `int` | Maximale Anzahl Aufnahme-Dateien. Älteste werden gelöscht. `0` = unbegrenzt. |
| `Recording.MaxAgeDays` | `int` | Dateien älter als N Tage werden gelöscht. `0` = unbegrenzt. |
| `Recording.SendAttachments` | `bool` | Aufnahmen nach Fertigstellung als Datei-Anhang senden. |
| `Compression.Enabled` | `bool` | WAV-Aufnahmen nach Alarm komprimieren (benötigt FFmpeg). |
| `Compression.Format` | `string` | Zielformat: `opus`, `mp3`, `aac`. |
| `Compression.BitrateKbps` | `int` | Ziel-Bitrate in kbps. |
| `Compression.KeepWavAudio` | `bool` | Original-WAV nach Komprimierung behalten (`false` = löschen). |
| `Camera.Enabled` | `bool` | Kamera-Aufnahme bei Alarm aktivieren (benötigt FFmpeg). |
| `Camera.DeviceName` | `string` | DirectShow-Gerätename. Leer = automatisch (erstes Gerät). Mit `--list-video` anzeigen. |
| `Camera.Format` | `string` | Videoformat: `mp4`, `avi`, `mkv`, `gif`. |
| `Camera.MuxWithAudio` | `bool` | WAV-Audio in Video einbetten. Erfordert `Recording.Enabled: true`. |
| `Camera.KeepMuxAudio` | `bool` | WAV nach erfolgreichem Muxing behalten (`false` = löschen). |
| `Camera.ScaleWidth` | `int` | Videobreite in Pixel. `0` = keine Skalierung. Standard: 320. |
| `Battery.Enabled` | `bool` | Batterie-Überwachung aktivieren. |
| `Battery.ThresholdPercent` | `int` | Ladestand in Prozent (0–100), unterhalb dessen eine Warnung gesendet wird. |
| `Battery.CheckIntervalSeconds` | `int` | Prüfintervall in Sekunden. Standard: 60. |
| `Battery.Message` | `string` | Text der Batterie-Warnung. Aktueller Ladestand wird angehängt: `"Batterie niedrig (18%)"`. |
| `Signal.CliPath` | `string` | Pfad zu `signal-cli.bat`. Relativ zur EXE oder absolut. |
| `Signal.Sender` | `string` | Handynummer des Absenders (Format: `+49...`). |
| `Signal.Recipient` | `string` | Ziel-Handynummer (Format: `+49...`). |
| `Telegram.BotToken` | `string` | Token des Telegram-Bots (von BotFather). Wird durch `--link` automatisch gesetzt. |
| `Telegram.ChatId` | `string` | Chat-ID des Empfängers. Wird durch `--link` automatisch gesetzt. |
| `WhatsApp.CliPath` | `string` | Pfad zu `mudslide.exe`. Relativ zur EXE oder absolut. Standard: `"mudslide.exe"`. |
| `WhatsApp.Sender` | `string` | Eigene WhatsApp-Nummer (Format: `+49...`). Wird durch `--link` automatisch gesetzt. |
| `WhatsApp.Recipient` | `string` | Ziel-WhatsApp-Nummer (Format: `+49...`). Wird durch `--link` automatisch gesetzt. |

### Schwellwert bestimmen

Starte BBFon und beobachte die Anzeige im Konsolenfenster:

```
[12:34:01] Lautstärke: 12.345
[12:34:01] Lautstärke: 287.123
```

Die Skala geht von `0` (Stille) bis `1000` (maximale Lautstärke). Empfehlung: `--calibrate` nutzen, um den Hintergrundlärm zu messen und einen Schwellwert vorgeschlagen zu bekommen.

---

## 4. Telegram einrichten


Detaillierte Anleitung: [TELEGRAM.md](TELEGRAM.md)

### Schritt 1 – Bot erstellen

1. Telegram öffnen und `@BotFather` suchen
2. `/newbot` senden
3. Namen und Benutzernamen vergeben (muss auf `bot` enden, z. B. `MeinBBFonBot`)
4. BotFather antwortet mit dem **Bot-Token**: `1234567890:ABC-xyz...`

### Schritt 2 – Chat-ID automatisch ermitteln

1. Den eigenen Bot in Telegram suchen und eine Nachricht schreiben (z. B. `/start`)
2. BBFon mit `--link` und dem Bot-Token aufrufen:

```cmd
BBFon.exe --link 1234567890:ABC-xyz...
```

BBFon findet die Chat-ID und speichert **Token + Chat-ID automatisch** in `appsettings.json`:

```
[BBFon] Chat-ID gefunden: 987654321  (Max Mustermann @maxmuster)
[BBFon] appsettings.json aktualisiert: BotToken + ChatId (987654321).
```

Danach ist BBFon direkt einsatzbereit.

> **Keine Nachrichten gefunden?** Erst eine Nachricht an den Bot schreiben, dann `--link` erneut ausführen.

### Nachrichten an eine Gruppe senden

1. Bot zur Gruppe hinzufügen
2. Eine Nachricht in der Gruppe schreiben
3. `--link` aufrufen – Gruppen-Chat-IDs sind **negativ**, z. B. `-1001234567890`

---

## 5. Signal einrichten

Signal hat keine offizielle API. BBFon nutzt **signal-cli**, ein Open-Source-Tool, das das Signal-Protokoll implementiert.

### Schritt 1 – Java installieren

signal-cli benötigt Java 17 oder neuer.

- Download: https://adoptium.net/ (Eclipse Temurin, Windows Installer)
- Nach der Installation testen: `java -version`

### Schritt 2 – signal-cli herunterladen

1. Aktuelle Version von https://github.com/AsamK/signal-cli/releases herunterladen
   - Datei: `signal-cli-x.x.x.tar.gz`
2. Entpacken, z. B. nach `C:\tools\signal-cli\`
3. Im entpackten Ordner liegt `bin\signal-cli.bat`

### Schritt 3 – signal-cli neben die BBFon.exe legen

Damit BBFon `signal-cli.bat` per relativem Pfad findet, am einfachsten so:

```
BBFon\
├── BBFon.exe
├── appsettings.json
└── signal-cli\
    ├── bin\
    │   └── signal-cli.bat   ← wird aufgerufen
    └── lib\
        └── ...
```

Dann in `appsettings.json`:

```json
"Signal": {
  "CliPath": "signal-cli\\bin\\signal-cli.bat",
  ...
}
```

Oder alternativ: einen Wrapper `signal-cli.bat` direkt neben der EXE anlegen:

```bat
@echo off
"C:\tools\signal-cli\bin\signal-cli.bat" %*
```

Dann bleibt `"CliPath": "signal-cli.bat"` der Standard.

### Schritt 4 – Mit BBFon verknüpfen (empfohlen)

Der einfachste Weg: Signal bereits auf dem Smartphone nutzen und BBFon als verknüpftes Gerät einrichten. Nummer, Provider und Verlinkung in einem Schritt:

```cmd
BBFon.exe --provider Signal --link +4917612345678
```

BBFon trägt die Nummer automatisch in `appsettings.json` ein (Sender + Recipient) und zeigt den QR-Code an. QR-Code in der Signal-App scannen:
`Einstellungen → Verknüpfte Geräte → (+) Gerät hinzufügen`

Danach ist BBFon direkt einsatzbereit.

### Schritt 4 (alternativ) – Neue Nummer registrieren

Wenn du eine eigene Absender-Nummer verwenden möchtest (z. B. alte SIM-Karte):

```cmd
signal-cli.bat -u +4912345678 register
signal-cli.bat -u +4912345678 verify 123456
```

Dann Provider und Nummer setzen:

```cmd
BBFon.exe --provider Signal
```

Und `appsettings.json` manuell befüllen:

```json
"Signal": {
  "CliPath": "signal-cli\\bin\\signal-cli.bat",
  "Sender": "+4912345678",
  "Recipient": "+4987654321"
}
```

### Schritt 5 – Verbindung testen

```cmd
BBFon.exe --test
```

---

## 6. WhatsApp einrichten

Detaillierte Anleitung: [WHATSAPP.md](WHATSAPP.md)

### Schritt 1 – mudslide.exe herunterladen

1. Aktuelle Version von https://github.com/robvanderleek/mudslide/releases herunterladen
2. `mudslide.exe` neben die `BBFon.exe` legen

### Schritt 2 – Mit BBFon verknüpfen

```cmd
BBFon.exe --provider WhatsApp --link +4917612345678
```

BBFon trägt die Nummer automatisch in `appsettings.json` ein (Sender + Recipient) und startet `mudslide login`. Den angezeigten QR-Code in der WhatsApp-App scannen:
`Einstellungen → Verknüpfte Geräte → Gerät hinzufügen`

### Schritt 3 – Verbindung testen

```cmd
BBFon.exe --test
```

---

## 7. Starten und Bedienen

### Parameter-Übersicht

| Parameter | Kurzform | Beschreibung |
|---|---|---|
| `--debug` | `-d` | Debug-Modus: keine Nachrichten, ausführliche Konsolenausgabe |
| `--test` | – | Sendet sofort eine Testnachricht und beendet sich |
| `--calibrate` | – | Misst 10s Hintergrundrauschen, schlägt `Threshold`-Wert vor und zeigt Balkendiagramm |
| `--provider <Signal\|Telegram\|WhatsApp>` | – | Setzt den aktiven Provider in `appsettings.json` und beendet sich (kombinierbar mit `--link` / `--test`) |
| `--link [Wert]` | – | **Signal/WhatsApp:** `--link +4917612345678` – Nummer in appsettings.json speichern und QR-Code zur Verlinkung anzeigen. **Telegram:** `--link <TOKEN>` – Chat-ID ermitteln und Token + Chat-ID in appsettings.json speichern |
| `--list-audio` | – | Verfügbare Mikrofon-/Audio-Eingabegeräte anzeigen |
| `--list-video` | – | Verfügbare DirectShow-Kamerageräte anzeigen (benötigt FFmpeg) |

### Audio-Eingabegerät wählen

```cmd
BBFon.exe --list-audio
```

Ausgabe:
```
[BBFon] Verfügbare Audio-Eingabegeräte:
  [0] "Mikrofon (Realtek High Definition Audio)"
  [1] "Headset-Mikrofon (USB Audio)"
[BBFon] Trage den gewünschten Namen als AudioDevice in appsettings.json ein.
```

Den gewünschten Namen (oder einen eindeutigen Teilstring) in `appsettings.json` eintragen:

```json
"AudioDevice": "Headset"
```

Leer lassen für das Standard-Mikrofon von Windows.

### Starten

```cmd
BBFon.exe
```

Aufnahme und Kamera werden über `appsettings.json` gesteuert (`Recording.Enabled`, `Camera.Enabled`).

### Debug-Modus (zum Testen)

```cmd
BBFon.exe --debug
```

Im Debug-Modus:
- **Keine Nachricht wird gesendet** – stattdessen Konsolenausgabe: `[DEBUG] Würde senden: "Lärm erkannt!"`
- Lautstärke-Zeile zeigt `[!]` wenn Schwellwert überschritten
- Wenn Cooldown einen Alarm blockiert: `[Cooldown: 45s]` in der Zeile sichtbar
- Batterie-Checks werden jedes Mal protokolliert
- Beim Start und bei jeder Dateiänderung werden die aktuellen Einstellungen angezeigt

### Hot Reload der Konfiguration

Die `appsettings.json` wird **bei jeder Dateiänderung automatisch neu geladen** – kein Neustart nötig. In der Konsole erscheint:

```
[BBFon] Konfiguration neu geladen.
[BBFon] --- Einstellungen ---
...
```

### Kalibrierung (--calibrate)

```cmd
BBFon.exe --calibrate
```

BBFon misst 10 Sekunden lang den Hintergrundlärm und gibt danach einen empfohlenen Schwellwert aus – inklusive Balkendiagramm mit allen 100ms-Intervallen:

```
[BBFon] Verlauf (100ms Intervalle):
  [0.0s] ████████░|░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░    18.234
  [0.1s] ██████████████|████░░░░░░░░░░░░░░░░░░░░░░░░░    45.678 !
  [0.2s] ██████|░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░    12.100
  ...
        ^ 32.000 (Schwellwert)

[BBFon] Trage in appsettings.json ein:
  "Threshold": 32.000
```

Der `|` im Balken markiert den empfohlenen Schwellwert. Werte über dem Schwellwert erscheinen rot mit `!`.

### Konsolenausgabe im Betrieb

```
[12:34:01] Lautstärke: 12.345
[12:34:01] Lautstärke: 287.890
[12:34:05] ALARM! Lautstärke 312.450 >= 300.000. Sende Nachricht...
[12:34:05] Audio-Aufnahme gestartet: 2026-03-05_12-34-05.wav
[12:34:05] Nachricht gesendet. Cooldown: 60s
[12:34:15] Audio-Aufnahme beendet (10s).
```

### Beenden

`Strg+C`, `Q` oder `Esc` drücken.

---

## 8. Projektstruktur

```
bbfon/
├── SETUP.md                          ← diese Datei
├── TELEGRAM.md                       ← Telegram-Einrichtung (Details)
├── WHATSAPP.md                       ← WhatsApp-Einrichtung (Details)
├── FFMPEG.md                         ← FFmpeg-Installation & Kamera-Konfiguration
├── SIGNAL-CLI.md                     ← Signal-CLI-Einrichtung (Details)
└── src/
    └── BBFon/
        ├── BBFon.csproj              ← Projektdatei (Target: net8.0-windows)
        ├── Program.cs                ← Einstiegspunkt, Konfiguration, CLI-Parameter
        ├── AppConfig.cs              ← Konfigurationsmodell (inkl. ThresholdScale-Konstante)
        ├── appsettings.json          ← Benutzer-Konfiguration
        └── Services/
            ├── INotificationService.cs           ← Interface (SendAsync mit Anhängen)
            ├── AudioMonitorService.cs            ← Mikrofon-Überwachung (NAudio)
            ├── CameraRecorderService.cs          ← Kamera-Aufnahme via FFmpeg/DirectShow
            ├── AudioCompressorService.cs         ← Audio-Komprimierung via FFmpeg
            ├── SignalNotificationService.cs      ← Signal-Versand via signal-cli
            ├── TelegramNotificationService.cs    ← Telegram-Versand via Bot API
            ├── RetryNotificationService.cs       ← Retry + Netzwerk-Wartelogik
            ├── DebugNotificationService.cs       ← Konsolen-Ausgabe für Debug-Modus
            ├── LinkService.cs                    ← Signal-Verlinkung (QR-Code)
            ├── TelegramLinkService.cs            ← Telegram Chat-ID-Ermittlung & appsettings-Update
            ├── BatteryMonitorService.cs          ← Batterie-Überwachung (Win32 API)
            ├── CalibrateService.cs               ← Threshold-Kalibrierung mit Balkendiagramm
            ├── SleepPreventionService.cs         ← Schlafmodus verhindern
            ├── ConfigValidator.cs                ← Konfigurationsvalidierung
            └── ConsoleLog.cs                     ← Thread-sichere farbige Ausgabe
```

---

## 9. Technische Details

### Lautstärke-Berechnung (RMS)

BBFon berechnet den **Root Mean Square (RMS)** der eingehenden PCM-Audiodaten:

```
RMS = sqrt( (1/N) * Σ(sample²) )
```

Jeder 16-Bit-PCM-Sample wird auf den Bereich `[-1.0, 1.0]` normiert. Der Rohwert (0.0–1.0) wird intern mit `1000` multipliziert für die Anzeige und Konfiguration (`ThresholdScale = 1000`).

- Aufnahmeformat: 16.000 Hz, Mono, 16 Bit
- Puffergröße: 100 ms (1.600 Samples pro Buffer)

### Batterie-Überwachung

Wenn `Battery.Enabled = true`, prüft BBFon alle `CheckIntervalSeconds` Sekunden den Akkuladestand des Geräts.

**Fallende Flanke:** Eine Benachrichtigung wird **nur einmal** gesendet, wenn der Ladestand den Schwellwert von oben nach unten kreuzt. Solange der Akku unterhalb bleibt, kommt keine weitere Warnung.

**Verhalten beim Start:** Ist der Akku beim Start bereits unter dem Schwellwert, wird kein Alarm ausgelöst (kein Fehlstart).

**Desktop-PCs ohne Akku:** BBFon erkennt das und meldet es in der Konsole.

**Nachrichtenformat:** `"<Message> (<Prozent>%)"`, z. B. `"Batterie niedrig (18%)"`.

### Analyse-Mechanismus

Wenn `Analysis.Enabled = true`, wird nicht jeder einzelne Lautstärke-Peak sofort gemeldet. BBFon zählt, wie oft der Pegel innerhalb eines gleitenden Zeitfensters überschritten wurde:

```
Zeitfenster: 10s | MinTriggerCount: 3

t=0s  Pegel 410   → Trigger 1/3
t=2s  Pegel 390   → Trigger 2/3
t=5s  Pegel 440   → Trigger 3/3 ✓ → ALARM
t=13s Pegel 400   → Trigger 1/3  (t=0s und t=2s sind aus dem Fenster gefallen)
```

Die Konsole zeigt den aktuellen Zähler live:
```
[12:34:07] Lautstärke: 412.345  Trigger: 2/3 (letzte 10s)
```

**Empfohlene Werte:**

| Szenario | WindowSeconds | MinTriggerCount |
|---|---|---|
| Kurzer, einmaliger Knall ignorieren | 5 | 3 |
| Nur bei anhaltendem Lärm melden | 15 | 8 |
| Empfindlich, aber nicht bei Einzelgeräuschen | 10 | 3 |

### Audio-Aufnahme

Wenn `Recording.Enabled: true` gesetzt ist, startet bei jedem Alarm eine WAV-Aufnahme:

- Der erste Audio-Buffer, der den Alarm ausgelöst hat, ist Bestandteil der Aufnahme
- Aufgenommen wird maximal `Recording.DurationSeconds` Sekunden (Standard: 10)
- Dateiname: `yyyy-MM-dd_HH-mm-ss.wav`
- Speicherort: selber Ordner wie die `BBFon.exe`
- Format: WAV, 16.000 Hz, Mono, 16 Bit (~320 KB)
- Nach der Aufnahme: optionale Komprimierung → optional Versand als Anhang → Bereinigung alter Dateien

### Kamera-Aufnahme

Wenn `Camera.Enabled: true` gesetzt ist, startet bei jedem Alarm eine Kamera-Aufnahme (benötigt FFmpeg):

- Läuft **parallel** zur Audio-Aufnahme, blockiert keine Meldungen
- Dauer: `Recording.DurationSeconds` Sekunden
- Dateiname: `yyyy-MM-dd_HH-mm-ss_cam.mp4`
- Kamera wird beim ersten Alarm automatisch erkannt (`--list-video` für manuelle Auswahl)
- Format `gif`: erst MP4 aufnehmen, dann zu GIF konvertieren, MP4 wird gelöscht
- `MuxWithAudio: true`: WAV als Tonspur in das Video einbetten (erfordert `Recording.Enabled: true`)

**Reihenfolge bei aktivem Muxing:**
1. Audio + Video parallel aufnehmen
2. WAV in Video einbetten
3. Audio komprimieren (falls aktiv)
4. Anhänge senden (falls aktiv)
5. Alte Dateien bereinigen

### Anhänge senden

Mit `Recording.SendAttachments: true` werden die fertigen Aufnahme-Dateien nach Abschluss aller Nachbearbeitungsschritte automatisch gesendet:

- Telegram: per `sendDocument`-API
- Signal: per `--attachment`-Flag an signal-cli
- Bei aktiver Komprimierung: komprimierte Datei statt WAV

### Cooldown-Mechanismus

Nach jeder gesendeten Nachricht wird der Timestamp gespeichert. Eine neue Nachricht wird nur verschickt, wenn seit der letzten mindestens `CooldownSeconds` vergangen sind.

### Provider-Auswahl

Die Auswahl erfolgt per `switch` auf `Provider` (case-insensitiv). Beide Provider implementieren `INotificationService` mit `SendAsync(string message, IReadOnlyList<string>? attachments)`. Der `RetryNotificationService` umschließt den gewählten Provider mit Retry-Logik und Netzwerk-Warten.

### Signal-Integration

Der Aufruf erfolgt als externer Prozess. `signal-cli` wird mit `send -m "<message>" -u <sender> <recipient>` aufgerufen. Bei einem Fehler-Exitcode wird die Fehlermeldung aus `stderr` ausgegeben.

### Telegram-Integration

Alarm-Nachricht per HTTP POST:
```
POST https://api.telegram.org/bot<TOKEN>/sendMessage
{ "chat_id": "...", "text": "..." }
```

Datei-Anhang per Multipart-Upload:
```
POST https://api.telegram.org/bot<TOKEN>/sendDocument
document: <binäre Datei>
```

Weitere Details: [TELEGRAM.md](TELEGRAM.md)

---

## 10. Fehlerbehebung

### Kein Mikrofon erkannt / falsches Gerät

- Mit `--list-audio` alle verfügbaren Geräte anzeigen
- `AudioDevice` in `appsettings.json` auf den gewünschten Gerätenamen setzen (Teilstring genügt)
- Mikrofon-Zugriff in den Windows-Datenschutzeinstellungen erlauben:
  `Einstellungen → Datenschutz → Mikrofon → Desktop-Apps Zugriff erlauben`

### Telegram: Nachricht wird nicht gesendet

- Bot-Token korrekt? Kein Leerzeichen, kein Zeilenumbruch
- Chat-ID korrekt? Bot muss vorher eine Nachricht vom Nutzer erhalten haben
- Firewall / Proxy blockiert Zugriff auf `api.telegram.org`?

### Signal: signal-cli startet nicht

- Java installiert und im PATH? `java -version` im CMD testen – mind. Java 17, empfohlen Java 21: https://adoptium.net/
- BBFon zeigt beim Start automatisch eine Warnung, wenn Java fehlt oder die Version zu alt ist
- Pfad zu `signal-cli.bat` korrekt? Standard bei Installation neben der EXE: `signal-cli\bin\signal-cli.bat`
- Nummer registriert und verifiziert? Oder als verknüpftes Gerät eingerichtet (`BBFon.exe --link +49...`)?

### Lautstärke immer 0.000

- Mikrofon stumm geschaltet oder Pegel auf 0?
- Falsches Aufnahmegerät? Mit `--list-audio` prüfen und `AudioDevice` setzen

### Batterie-Warnung wird nicht gesendet

- `Battery.Enabled` auf `true` gesetzt?
- War der Akku beim Start bereits unter dem Schwellwert? → Erst aufladen und wieder entladen
- Desktop-PC ohne Akku: Funktion ist nicht nutzbar

### Zu viele / zu wenige Alarme

- `--calibrate` ausführen und empfohlenen `Threshold`-Wert übernehmen
- `CooldownSeconds` erhöhen, um Nachrichten-Häufigkeit zu reduzieren
- `Analysis` aktivieren, um Einzelgeräusche zu ignorieren

### Aufnahme-Datei ist leer oder kaputt

- Passiert, wenn BBFon sofort nach dem Alarm beendet wird – die Aufnahme wird bei Programmende sauber abgeschlossen
- Prüfen, ob der Zielordner (neben der EXE) Schreibrechte hat
