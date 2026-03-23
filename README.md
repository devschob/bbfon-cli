# BBFon

Mikrofon-Überwachung mit Benachrichtigung über **Telegram**, **Signal** oder **WhatsApp**.

BBFon überwacht ein Mikrofon und sendet eine Nachricht, sobald die Lautstärke einen konfigurierbaren Schwellwert überschreitet.

 - Minimal invasiv: Prüft nur den Pegel, kein dauerndes Lauschen.
 - Aufnahme nur on demand.
 
 ```bat
  BBFon.exe --provider Signal --link +4917612345678
  
  ```

---

## BBFon (C#, Windows)

Vollständige Dokumentation: [SETUP.md](SETUP.md)

## Messaging-Provider einrichten

| Provider | Anleitung |
|---|---|
| Telegram | [TELEGRAM.md](TELEGRAM.md) |
| Signal | [SIGNAL-CLI.md](SIGNAL-CLI.md) |
| WhatsApp | [WHATSAPP.md](WHATSAPP.md) |
| FFmpeg (Aufnahmen) | [FFMPEG.md](FFMPEG.md) |


## Voraussetzungen

| Anforderung | Details |
|---|---|
| Betriebssystem | Windows 10 / 11 |
| .NET SDK | .NET 8 SDK |
| Mikrofon | Aufnahmegerät in Windows konfiguriert |
| Für Signal | Java 17+ (für signal-cli) |
| Für Telegram | Internetzugang, Telegram-Account |
| Für WhatsApp | mudslide.exe ([Download](https://github.com/robvanderleek/mudslide/releases)), WhatsApp-Account |
| Für Komprimierung / Kamera | FFmpeg |

---


### Parameter


| Parameter | Kurzform | Beschreibung |
|---|---|---|
| `--debug` | `-d` | Debug-Modus: keine Nachrichten, ausführliche Konsolenausgabe |
| `--test` | – | Sendet sofort eine Testnachricht und beendet sich |
| `--calibrate` | – | Misst 10s Hintergrundrauschen, schlägt `Threshold`-Wert vor und zeigt Balkendiagramm |
| `--provider <Signal\|Telegram\|WhatsApp>` | – | Setzt den aktiven Provider in `appsettings.json` und beendet sich (kombinierbar mit `--link` / `--test`) |
| `--link [Wert]` | – | **Signal/WhatsApp:** `--link +4917612345678` – Nummer in appsettings.json speichern und QR-Code zur Verlinkung anzeigen. **Telegram:** `--link <TOKEN>` – Chat-ID ermitteln und Token + Chat-ID in appsettings.json speichern |
| `--list-audio` | – | Verfügbare Mikrofon-/Audio-Eingabegeräte anzeigen |
| `--list-video` | – | Verfügbare DirectShow-Kamerageräte anzeigen (benötigt FFmpeg) |

### Konfiguration (`appsettings.json`)

```json
{
  "Provider": "Telegram",
  "Triggers": [
    {
      "Threshold": 30,
      "CooldownSeconds": 60,
      "Message": "gugu gaga",
      "IsRecording": true,
      "Analysis": {
        "Enabled": true,
        "WindowSeconds": 10,
        "MinTriggerCount": 3
      }
    }
  ],
  "Startup": {
    "Enabled": true,
    "Message": "hearing..."
  },
  "Telegram": {
    "BotToken": "123:ABC",
    "ChatId": "1234"
  }
}
```

---



## bbfon-cli (Python)

### Voraussetzungen

- Python 3.10+
- `pip install -r requirements.txt`

Abhängigkeiten: `sounddevice`, `numpy`, `httpx`, `pydantic>=2.0`, `qrcode`, `psutil`

Für Aufnahmen / Kamera: **FFmpeg** im PATH oder Pfad in `appsettings.json`

### Starten

```bash
python bbfon.py
```

