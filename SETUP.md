# BBFon – Mikrofon-Überwachung mit Signal/Telegram-Benachrichtigung

BBFon überwacht das Standard-Mikrofon des Windows-PCs und sendet eine Nachricht über Signal oder Telegram, sobald die Lautstärke einen konfigurierbaren Schwellwert überschreitet. Zwischen zwei Nachrichten gilt ein Cooldown-Zeitraum, um Spam zu verhindern.

---

## Inhaltsverzeichnis

1. [Voraussetzungen](#1-voraussetzungen)
2. [Projekt kompilieren](#2-projekt-kompilieren)
3. [Konfiguration (appsettings.json)](#3-konfiguration-appsettingsjson)
4. [Telegram einrichten](#4-telegram-einrichten)
5. [Signal einrichten](#5-signal-einrichten)
6. [Starten und Bedienen](#6-starten-und-bedienen)
7. [Projektstruktur](#7-projektstruktur)
8. [Technische Details](#8-technische-details)
9. [Fehlerbehebung](#9-fehlerbehebung)

---

## 1. Voraussetzungen

| Anforderung | Details |
|---|---|
| Betriebssystem | Windows 10 / 11 |
| .NET SDK | .NET 8 SDK ([download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)) |
| Mikrofon | Standard-Aufnahmegerät in Windows konfiguriert |
| Für Signal | Java 17+ (für signal-cli) |
| Für Telegram | Internetzugang, Telegram-Account |

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

Die `appsettings.json` liegt **neben der EXE** und wird beim Start geladen.

```json
{
  "Threshold": 0.3,
  "CooldownSeconds": 60,
  "Message": "Lärm erkannt!",
  "Provider": "Telegram",
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
| `Threshold` | `float` (0.0–1.0) | Lautstärke-Schwellwert. `0.0` = Stille, `1.0` = maximale Lautstärke. Empfehlung: `0.2`–`0.4` |
| `CooldownSeconds` | `int` | Mindestabstand in Sekunden zwischen zwei Nachrichten. Verhindert Spam. |
| `Message` | `string` | Text der gesendeten Nachricht. |
| `Provider` | `string` | Aktiver Dienst: `"Telegram"` oder `"Signal"` (Groß-/Kleinschreibung egal). |
| `Analysis.Enabled` | `bool` | Analyse aktivieren (`true`) oder deaktivieren (`false`). Bei `false`: jeder einzelne Trigger löst sofort aus. |
| `Analysis.WindowSeconds` | `int` | Beobachtungsfenster in Sekunden. Nur Trigger innerhalb dieser Zeitspanne werden gezählt. |
| `Analysis.MinTriggerCount` | `int` | Mindestanzahl an Pegeln >= Schwellwert innerhalb von `WindowSeconds`, bevor eine Nachricht gesendet wird. |
| `Recording.MaxFiles` | `int` | Maximale Anzahl WAV-Dateien, die behalten werden. Älteste werden gelöscht. `0` = unbegrenzt. |
| `Recording.MaxAgeDays` | `int` | Dateien älter als diese Anzahl Tage werden gelöscht. `0` = unbegrenzt. |
| `Battery.Enabled` | `bool` | Batterie-Überwachung aktivieren. |
| `Battery.ThresholdPercent` | `int` | Ladestand in Prozent (0–100), unterhalb dessen eine Warnung gesendet wird. |
| `Battery.CheckIntervalSeconds` | `int` | Prüfintervall in Sekunden. Standard: 60. |
| `Battery.Message` | `string` | Text der Batterie-Warnung. Der aktuelle Ladestand wird automatisch angehängt: `"Batterie niedrig (18%)"`. |
| `Signal.CliPath` | `string` | Pfad zu `signal-cli.bat`. Relativ zur EXE oder absoluter Pfad. |
| `Signal.Sender` | `string` | Handynummer, mit der signal-cli registriert ist (Format: `+49...`). |
| `Signal.Recipient` | `string` | Ziel-Handynummer (Format: `+49...`). |
| `Telegram.BotToken` | `string` | Token des Telegram-Bots (von BotFather). |
| `Telegram.ChatId` | `string` | Chat-ID des Empfängers (Person oder Gruppe). |

### Schwellwert bestimmen

Starte BBFon und beobachte die Anzeige im Konsolenfenster:

```
[12:34:01] Lautstärke: 0.012
[12:34:01] Lautstärke: 0.287
```

Sprich normal in das Mikrofon und notiere typische Werte. Setze `Threshold` etwas höher als der Hintergrundlärm.

---

## 4. Telegram einrichten

### Schritt 1 – Bot erstellen

1. Telegram öffnen und `@BotFather` suchen
2. `/newbot` senden
3. Einen Namen und einen Benutzernamen vergeben (muss auf `bot` enden, z. B. `MeinBBFonBot`)
4. BotFather antwortet mit dem **Bot-Token**: `1234567890:ABC-xyz...`

### Schritt 2 – Chat-ID herausfinden

1. Den eigenen Bot in Telegram suchen und **eine beliebige Nachricht senden** (wichtig, sonst funktioniert getUpdates nicht)
2. Im Browser folgende URL öffnen (Token einsetzen):

   ```
   https://api.telegram.org/bot<TOKEN>/getUpdates
   ```

3. In der JSON-Antwort die `chat.id` ablesen:

   ```json
   {
     "result": [{
       "message": {
         "chat": {
           "id": 987654321
         }
       }
     }]
   }
   ```

4. Diese Zahl als `ChatId` in `appsettings.json` eintragen.

### Schritt 3 – appsettings.json befüllen

```json
"Provider": "Telegram",
"Telegram": {
  "BotToken": "1234567890:ABC-xyz...",
  "ChatId": "987654321"
}
```

### Nachrichten an eine Gruppe senden

1. Bot zur Gruppe hinzufügen
2. Eine Nachricht in der Gruppe schreiben
3. `getUpdates` aufrufen – Gruppen-Chat-IDs sind **negativ**, z. B. `-1001234567890`

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

### Schritt 4 – Absender-Nummer registrieren

BBFon benötigt eine **eigene Handynummer als Absender** (z. B. eine alte SIM-Karte oder eine VoIP-Nummer).

```cmd
signal-cli.bat -u +4912345678 register
```

Signal sendet einen SMS-Code an diese Nummer:

```cmd
signal-cli.bat -u +4912345678 verify 123456
```

### Schritt 5 – Verbindung testen

```cmd
signal-cli.bat send -m "Testmessage" -u +4912345678 +4987654321
```

Wenn die Nachricht ankommt, ist alles korrekt konfiguriert.

### Schritt 6 – appsettings.json befüllen

```json
"Provider": "Signal",
"Signal": {
  "CliPath": "signal-cli.bat",
  "Sender": "+4912345678",
  "Recipient": "+4987654321"
}
```

---

## 6. Starten und Bedienen

### Parameter-Übersicht

| Parameter | Kurzform | Beschreibung |
|---|---|---|
| `--record` | `-r` | Alarm-Aufnahme aktivieren (WAV neben EXE) |
| `--debug` | `-d` | Debug-Modus: keine Nachrichten, ausführliche Konsolenausgabe |
| `--test` | – | Sendet sofort eine Testnachricht und beendet sich – zum Prüfen der Konfiguration |
| `--calibrate` | – | Misst 10s Hintergrundrauschen und schlägt automatisch einen `Threshold`-Wert vor |
| `--link` | – | Signal-Verlinkung: QR-Code in Konsole anzeigen und auf Scan warten (nur bei Provider = Signal) |

Parameter können kombiniert werden:

```cmd
BBFon.exe --record --debug
```

### Starten (ohne Aufnahme)

```cmd
BBFon.exe
```

### Starten mit Alarm-Aufnahme

```cmd
BBFon.exe --record
```

Kurzform:

```cmd
BBFon.exe -r
```

### Debug-Modus (zum Testen)

```cmd
BBFon.exe --debug
```

Im Debug-Modus:
- **Keine Nachricht wird gesendet** – stattdessen Konsolenausgabe: `[DEBUG] Würde senden: "Lärm erkannt!"`
- Lautstärke-Zeile zeigt `[!]` wenn Schwellwert überschritten
- Wenn Cooldown einen Alarm blockiert: `[Cooldown: 45s]` in der Zeile sichtbar
- Batterie-Checks werden jedes Mal protokolliert mit aktuellem Ladestand und Flanken-Status
- Beim Start und bei jeder Dateiänderung werden die aktuellen Einstellungen angezeigt

### Hot Reload der Konfiguration

Die `appsettings.json` wird **bei jeder Dateiänderung automatisch neu geladen** – kein Neustart nötig. Alle Services (Audio, Batterie) übernehmen die neuen Werte sofort beim nächsten Zyklus. In der Konsole erscheint:

```
[BBFon] Konfiguration neu geladen.
[BBFon] --- Einstellungen ---
[BBFon]   Provider:       Telegram
...
```

### Konsolenausgabe beim Start

Ohne Aufnahme:
```
[BBFon] Starte... Schwellwert: 0.30 | Provider: Telegram
[BBFon] Überwache Standard-Mikrofon... (Schwellwert: 0.30)
[BBFon] Strg+C zum Beenden.

[12:34:01] Lautstärke: 0.012
```

Mit Aufnahme (`--record`):
```
[BBFon] Starte... Schwellwert: 0.30 | Provider: Telegram
[BBFon] Überwache Standard-Mikrofon... (Schwellwert: 0.30)
[BBFon] Aufnahme bei Alarm: aktiv (max. 10s, WAV neben EXE)
[BBFon] Strg+C zum Beenden.

[12:34:01] Lautstärke: 0.012
```

### Alarm ausgelöst (mit Aufnahme)

```
[12:34:05] Lautstärke: 0.412
[12:34:05] ALARM! Lautstärke 0.412 >= 0.30. Sende Nachricht...
[12:34:05] Aufnahme gestartet: 2026-03-03_12-34-05.wav
[12:34:05] Nachricht gesendet. Cooldown: 60s
[12:34:15] Aufnahme beendet (10s).
```

Die WAV-Datei liegt danach neben der `BBFon.exe`:
```
BBFon\
├── BBFon.exe
├── appsettings.json
├── 2026-03-03_12-34-05.wav   ← Aufnahme
└── ...
```

### Beenden

`Strg+C` drücken. Eine laufende Aufnahme wird sauber abgeschlossen:

```
[BBFon] Beendet.
```

---

## 7. Projektstruktur

```
bbfon/
├── SETUP.md                          ← diese Datei
└── src/
    └── BBFon/
        ├── BBFon.csproj              ← Projektdatei (Target: net8.0-windows)
        ├── Program.cs                ← Einstiegspunkt, Konfiguration laden
        ├── AppConfig.cs              ← Konfigurationsmodell
        ├── appsettings.json          ← Benutzer-Konfiguration
        └── Services/
            ├── INotificationService.cs          ← Interface für Benachrichtigungen
            ├── AudioMonitorService.cs           ← Mikrofon-Überwachung (NAudio)
            ├── SignalNotificationService.cs     ← Signal-Versand via signal-cli
            └── TelegramNotificationService.cs  ← Telegram-Versand via Bot API
```

---

## 8. Technische Details

### Lautstärke-Berechnung (RMS)

BBFon berechnet den **Root Mean Square (RMS)** der eingehenden PCM-Audiodaten:

```
RMS = sqrt( (1/N) * Σ(sample²) )
```

Jeder 16-Bit-PCM-Sample wird auf den Bereich `[-1.0, 1.0]` normiert. Der RMS-Wert gibt damit einen guten Eindruck der wahrgenommenen Lautstärke (im Gegensatz zum simplen Maximalwert).

- Aufnahmeformat: 16.000 Hz, Mono, 16 Bit
- Puffergröße: 100 ms (1.600 Samples pro Buffer)

### Batterie-Überwachung

Wenn `Battery.Enabled = true`, prüft BBFon alle `CheckIntervalSeconds` Sekunden den Akkuladestand des Geräts.

**Fallende Flanke:** Eine Benachrichtigung wird **nur einmal** gesendet, wenn der Ladestand den Schwellwert von oben nach unten kreuzt. Solange der Akku unterhalb bleibt, kommt keine weitere Warnung. Erst wenn er wieder aufgeladen wird (über den Schwellwert) und erneut darunter fällt, wird wieder gesendet.

**Verhalten beim Start:** BBFon liest den aktuellen Ladestand beim Start und merkt sich, ob er über oder unter dem Schwellwert liegt. Ist der Akku beim Start bereits unter dem Schwellwert, wird kein Alarm ausgelöst (kein Fehlstart).

**Desktop-PCs ohne Akku:** BBFon erkennt das und meldet es in der Konsole. Die Überwachung läuft weiter, sendet aber keine Nachrichten.

**Nachrichtenformat:** `"<Message> (<Prozent>%)"`, z. B. `"Batterie niedrig (18%)"`.

### Analyse-Mechanismus

Wenn `Analysis.Enabled = true`, wird nicht jeder einzelne Lautstärke-Peak sofort gemeldet. Stattdessen zählt BBFon, wie oft der Pegel innerhalb eines gleitenden Zeitfensters überschritten wurde:

```
Zeitfenster: 10s | MinTriggerCount: 3

t=0s  Pegel 0.41  → Trigger 1/3
t=2s  Pegel 0.39  → Trigger 2/3
t=5s  Pegel 0.44  → Trigger 3/3 ✓ → ALARM, Nachricht senden
t=13s Pegel 0.40  → Trigger 1/3  (t=0s und t=2s sind aus dem Fenster gefallen)
```

Die Konsole zeigt den aktuellen Zähler live:
```
[12:34:07] Lautstärke: 0.412  Trigger: 2/3 (letzte 10s)
```

Nach einem Alarm wird die Triggerliste geleert, damit der nächste Alarm von vorne zählt.

**Empfohlene Werte:**

| Szenario | WindowSeconds | MinTriggerCount |
|---|---|---|
| Kurzer, einmaliger Knall ignorieren | 5 | 3 |
| Nur bei anhaltendem Lärm melden | 15 | 8 |
| Empfindlich, aber nicht bei Einzelgeräuschen | 10 | 3 |

### Alarm-Aufnahme

Wenn `--record` / `-r` übergeben wird, startet bei jedem Alarm eine WAV-Aufnahme:

- Das Audio-Buffer, der den Alarm ausgelöst hat, ist der erste Chunk der Datei (kein Aussetzer am Anfang)
- Nach jeder Aufnahme werden automatisch alte Dateien bereinigt (sofern `MaxFiles` oder `MaxAgeDays` gesetzt)
- Aufgenommen wird maximal **10 Sekunden**
- Dateiname: `yyyy-MM-dd_HH-mm-ss.wav` (z. B. `2026-03-03_12-34-05.wav`)
- Speicherort: selber Ordner wie die `BBFon.exe`
- Format: WAV, 16.000 Hz, Mono, 16 Bit (ca. 320 KB pro Aufnahme)
- Während des Cooldowns wird keine neue Aufnahme gestartet

### Cooldown-Mechanismus

Nach jeder gesendeten Nachricht wird der Timestamp gespeichert. Eine neue Nachricht wird nur verschickt, wenn seit der letzten mindestens `CooldownSeconds` vergangen sind. Das verhindert Flut bei anhaltendem Lärm.

### Provider-Auswahl

Die Auswahl des Notification-Providers erfolgt in `Program.cs` per `switch`-Expression auf `Provider` (case-insensitiv). Beide Provider implementieren `INotificationService` mit einer einzigen Methode `SendAsync(string message)`.

### Signal-Integration

Der Aufruf erfolgt als externer Prozess (`System.Diagnostics.Process`). `signal-cli` wird mit den Parametern `send -m "<message>" -u <sender> <recipient>` aufgerufen. Bei einem Fehler-Exitcode wird die Fehlermeldung aus `stderr` ausgegeben.

### Telegram-Integration

Einfacher HTTP POST an die Telegram Bot API:

```
POST https://api.telegram.org/bot<TOKEN>/sendMessage
Content-Type: application/json

{ "chat_id": "...", "text": "..." }
```

---

## 9. Fehlerbehebung

### Kein Mikrofon erkannt

- Sicherstellen, dass in den Windows-Soundeinstellungen ein Standard-Aufnahmegerät gesetzt ist
- Mikrofon-Zugriff in den Windows Datenschutzeinstellungen erlauben:
  `Einstellungen → Datenschutz → Mikrofon → Desktop-Apps Zugriff erlauben`

### Telegram: Nachricht wird nicht gesendet

- Bot-Token korrekt? Kein Leerzeichen, kein Zeilenumbruch
- Chat-ID korrekt? Bot muss vorher eine Nachricht vom Nutzer erhalten haben
- Firewall / Proxy blockiert Zugriff auf `api.telegram.org`?

### Signal: signal-cli startet nicht

- Java installiert und im PATH? `java -version` im CMD testen
- Pfad zu `signal-cli.bat` korrekt? Absoluten Pfad zum Testen verwenden
- Nummer registriert und verifiziert?

### Lautstärke immer 0.000

- Mikrofon stumm geschaltet oder Pegel auf 0?
- Falsches Aufnahmegerät als Standard gesetzt?

### Batterie-Warnung wird nicht gesendet

- `Battery.Enabled` auf `true` gesetzt?
- War der Akku beim Start bereits unter dem Schwellwert? → Erst aufladen und wieder entladen
- Desktop-PC ohne Akku: Funktion ist nicht nutzbar

### Zu viele / zu wenige Alarme

- `Threshold` anpassen: Werte in der Konsole beobachten und sinnvollen Schwellwert ermitteln
- `CooldownSeconds` erhöhen, um Nachrichten-Häufigkeit zu reduzieren

### Aufnahme-Datei ist leer oder kaputt

- Passiert, wenn BBFon sofort nach dem Alarm per Strg+C beendet wird – die Aufnahme wird aber auch bei Programmende sauber abgeschlossen
- Prüfen, ob der Zielordner (neben der EXE) Schreibrechte hat
