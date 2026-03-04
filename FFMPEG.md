# FFmpeg – Installation & Einrichtung für BBFon

## Wofür wird FFmpeg in BBFon verwendet?

BBFon nutzt FFmpeg optional für drei Funktionen:

| Funktion | Aktivierung | Beschreibung |
|---|---|---|
| Audio-Komprimierung | `Compression.Enabled: true` | WAV-Aufnahmen in MP3, AAC oder Opus konvertieren |
| Kamera-Aufnahme | `-v` oder `Camera.Enabled: true` | Webcam-Video bei Alarm aufnehmen |
| GIF-Konvertierung | `Camera.Format: "gif"` | Video-Aufnahmen als animiertes GIF speichern |

FFmpeg ist **nicht erforderlich**, wenn weder Komprimierung noch Kamera-Aufnahme aktiviert ist.

---

## Installation

### Option 1 – ffmpeg.exe neben BBFon.exe legen (empfohlen)

Kein PATH-Eintrag nötig, keine Systemänderungen.

1. FFmpeg-Build herunterladen: https://github.com/BtbN/FFmpeg-Builds/releases
   → Datei: **`ffmpeg-master-latest-win64-gpl.zip`**
2. ZIP entpacken, die Datei `ffmpeg.exe` aus dem `bin\`-Ordner extrahieren
3. `ffmpeg.exe` direkt neben `BBFon.exe` ablegen:

```
BBFon\
├── BBFon.exe
├── appsettings.json
└── ffmpeg.exe
```

`FfmpegPath` in `appsettings.json` bleibt auf dem Standard `"ffmpeg.exe"`.

---

### Option 2 – winget (Windows Package Manager)

```cmd
winget install Gyan.FFmpeg
```

Nach der Installation ist `ffmpeg` global verfügbar. In `appsettings.json`:
```json
"FfmpegPath": "ffmpeg"
```

---

### Option 3 – Chocolatey

```cmd
choco install ffmpeg
```

---

### Option 4 – Manuell mit PATH-Eintrag

1. FFmpeg herunterladen und entpacken, z. B. nach `C:\tools\ffmpeg\`
2. `C:\tools\ffmpeg\bin` zum Windows PATH hinzufügen:
   `Systemsteuerung → System → Erweiterte Systemeinstellungen → Umgebungsvariablen → PATH`
3. Neues CMD öffnen, dann testen: `ffmpeg -version`

---

## Installation prüfen

```cmd
ffmpeg -version
```

Erwartete erste Zeile der Ausgabe:
```
ffmpeg version 7.x ...
```

---

## Kamera-Gerät ermitteln

BBFon erkennt die erste verfügbare Kamera automatisch (DirectShow). Mit `--list-cameras` alle Geräte anzeigen:

```cmd
BBFon.exe --list-cameras
```

Beispielausgabe:
```
[BBFon] Verfügbare Kamera-Geräte:
  - "Integrated Camera"
  - "OBS Virtual Camera"
[BBFon] Trage den gewünschten Namen als Camera.DeviceName in appsettings.json ein.
```

Wenn ein bestimmtes Gerät verwendet werden soll (statt automatischer Erkennung):
```json
"Camera": {
  "DeviceName": "Integrated Camera"
}
```

---

## Konfiguration in appsettings.json

### Audio-Komprimierung

```json
"Compression": {
  "Enabled": true,
  "FfmpegPath": "ffmpeg.exe",
  "Format": "opus",
  "BitrateKbps": 24,
  "DeleteWavAfterCompress": true
}
```

| Format | Dateiendung | Empfohlene Bitrate | Hinweis |
|---|---|---|---|
| `opus` | `.ogg` | 24 kbps | Beste Qualität/Größe-Verhältnis |
| `mp3`  | `.mp3` | 64–128 kbps | Maximale Kompatibilität |
| `aac`  | `.m4a` | 48–96 kbps | Gut für Apple-Geräte |

---

### Kamera-Aufnahme

```json
"Camera": {
  "Enabled": true,
  "FfmpegPath": "ffmpeg.exe",
  "DeviceName": "",
  "DurationSeconds": 10,
  "Format": "mp4",
  "MuxWithAudio": false
}
```

| Format | Beschreibung |
|---|---|
| `mp4` | Standard, breite Kompatibilität |
| `avi` | Älteres Format |
| `mkv` | Matroska, flexibel |
| `gif` | Animiertes GIF (320px, 10fps) – kein Audio möglich |

**`MuxWithAudio: true`** – bettet die gleichzeitig aufgenommene WAV-Datei als Tonspur in das Video ein. Erfordert aktive Audio-Aufnahme (`-r`). Gilt nicht bei `Format: "gif"`.

---

## Dateigröße (Richtwerte, 10 Sekunden)

| Typ | Format | Größe ca. |
|---|---|---|
| WAV (unkomprimiert) | – | 320 KB |
| Opus 24 kbps | `.ogg` | 30 KB |
| MP3 128 kbps | `.mp3` | 160 KB |
| MP4 Webcam-Video | `.mp4` | 1–5 MB |
| GIF 320px 10fps | `.gif` | 2–8 MB |

> GIF ist für Videos ineffizient. Für kürzere Sequenzen oder Übersicht geeignet; für längere Aufnahmen lieber `mp4` verwenden.

---

## Häufige Fehler

| Fehler | Ursache | Lösung |
|---|---|---|
| `ffmpeg konnte nicht gestartet werden` | `ffmpeg.exe` nicht gefunden | `FfmpegPath` prüfen oder `ffmpeg.exe` neben BBFon.exe legen |
| `Kein DirectShow-Videogerät gefunden` | Keine Kamera / Treiber fehlt | Kamera in der Windows-Kameraapp testen; `--list-cameras` ausführen |
| Kamera-Aufnahme: `Exit 1` | Gerätename falsch | `--list-cameras` ausführen, exakten Namen als `DeviceName` eintragen |
| GIF-Datei sehr groß | GIF-Format ist ineffizient | `DurationSeconds` reduzieren oder auf `mp4` wechseln |
| `Komprimierung fehlgeschlagen` | Codec nicht verfügbar | `ffmpeg -codecs` prüfen; ggf. einen anderen Build herunterladen |
