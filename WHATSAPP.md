# WhatsApp – Einrichtung & Nutzung mit BBFon

## Wie funktioniert WhatsApp in BBFon?

BBFon nutzt **mudslide** – ein Open-Source-Kommandozeilenwerkzeug, das das WhatsApp-Protokoll implementiert. mudslide läuft als eigenständiges Binary neben der BBFon.exe und wird bei jedem Alarm aufgerufen, um Nachrichten und Dateien zu senden.

Voraussetzungen:
- Ein WhatsApp-Account (Smartphone)
- `mudslide.exe` neben der BBFon.exe (einmaliger Download)
- Einmalige Verknüpfung via QR-Code (wie bei Signal)

---

## Systemanforderungen

| Anforderung | Details |
|---|---|
| **WhatsApp-Account** | Auf dem Smartphone aktiv |
| **mudslide.exe** | Neben `BBFon.exe` ablegen ([Download](https://github.com/robvanderleek/mudslide/releases)) |
| **Netzwerk** | Ausgehende HTTPS-Verbindung zu WhatsApp-Servern |
| **Session-Daten** | Werden von mudslide in `%APPDATA%\Local\mudslide\Data` gespeichert |

---

## Schritt 1 – mudslide.exe herunterladen

1. Aktuelle Version von https://github.com/robvanderleek/mudslide/releases herunterladen
2. Die `mudslide.exe` **neben die `BBFon.exe`** legen:

```
BBFon\
├── BBFon.exe
├── appsettings.json
├── mudslide.exe        ← hier ablegen
└── ...
```

Alternativ kann ein beliebiger Pfad in `appsettings.json` angegeben werden:

```json
"WhatsApp": {
  "CliPath": "C:\\tools\\mudslide.exe",
  ...
}
```

---

## Schritt 2 – Mit BBFon verknüpfen

Der einfachste Weg: eigene Nummer angeben und BBFon als verknüpftes Gerät einrichten (sendet an sich selbst, ideal für Babyfon).

```cmd
BBFon.exe --provider WhatsApp --link +4917612345678
```

BBFon führt dabei automatisch folgende Schritte aus:

1. Nummer in `appsettings.json` eintragen (Sender + Recipient)
2. `mudslide login` starten
3. QR-Code in der Konsole anzeigen

Den QR-Code in der **WhatsApp-App** scannen:
`WhatsApp → Einstellungen → Verknüpfte Geräte → Gerät hinzufügen`

Nach erfolgreichem Scan meldet BBFon:
```
[BBFon] Erfolgreich verknüpft! BBFon kann jetzt WhatsApp nutzen.
```

---

## Schritt 3 – appsettings.json prüfen

Nach `--link` ist `appsettings.json` bereits vollständig befüllt:

```json
"Provider": "WhatsApp",
"WhatsApp": {
  "CliPath": "mudslide.exe",
  "Sender": "+4917612345678",
  "Recipient": "+4917612345678"
}
```

Um an eine **andere Nummer** zu senden, einfach `Recipient` in `appsettings.json` anpassen.

---

## Schritt 4 – Verbindung testen

```cmd
BBFon.exe --test
```

Eine Testnachricht wird sofort gesendet. Kommt sie an, ist BBFon einsatzbereit.

---

## Nachrichten an eine andere Nummer senden

`Sender` bleibt die eigene Nummer (eingeloggter Account), `Recipient` wird auf die Zielnummer geändert:

```json
"WhatsApp": {
  "CliPath": "mudslide.exe",
  "Sender": "+4917612345678",
  "Recipient": "+4312345678"
}
```

Unterstützte Formate für `Recipient`:

| Format | Beispiel | Beschreibung |
|---|---|---|
| Internationale Nummer | `+4917612345678` | Normale WhatsApp-Nummer |
| Gruppen-ID | `123456789@g.us` | WhatsApp-Gruppe (ID via `mudslide groups`) |

---

## Session-Verwaltung

mudslide speichert die Anmeldedaten (Session) lokal:

- **Windows:** `%APPDATA%\Local\mudslide\Data`
- **Linux/macOS:** `~/.local/share/mudslide`

Die Session bleibt dauerhaft gültig, solange das Gerät in WhatsApp verknüpft bleibt. Im Normalbetrieb ist **kein erneutes Einloggen nötig**.

### Session abgelaufen / Gerät wurde entfernt

Wenn WhatsApp die Verknüpfung aufhebt (z.B. nach manueller Entfernung in der App), muss `--link` erneut ausgeführt werden:

```cmd
BBFon.exe --provider WhatsApp --link +4917612345678
```

---

## Häufige Fehler

| Fehler | Ursache | Lösung |
|---|---|---|
| `mudslide konnte nicht gestartet werden` | `mudslide.exe` nicht gefunden | `mudslide.exe` neben `BBFon.exe` legen oder `CliPath` in appsettings.json anpassen |
| Nachricht kommt nicht an | Session abgelaufen oder Gerät entfernt | `BBFon.exe --provider WhatsApp --link +49...` erneut ausführen |
| QR-Code erscheint nicht | mudslide-Fehler beim Start | In der Konsole nach Fehlermeldungen suchen, ggf. `mudslide login` manuell testen |
| Timeout beim Senden | Netzwerkproblem oder WhatsApp-Server nicht erreichbar | Netzwerk prüfen; BBFon wiederholt den Versuch automatisch (RetryNotificationService) |
| Anhang wird nicht gesendet | Dateipfad ungültig oder Datei fehlt | Prüfen ob Aufnahmedatei vorhanden ist; `Recording.SendAttachments` in appsettings.json |

---

## WhatsApp vs. Signal vs. Telegram – Vergleich für BBFon

| Kriterium | WhatsApp | Signal | Telegram |
|---|---|---|---|
| Einrichtungsaufwand | Gering (nur mudslide + QR) | Hoch (Java, signal-cli) | Gering (nur Bot + Chat-ID) |
| Extra Software | mudslide.exe | Java 17+, signal-cli | Keine |
| Zweite Nummer nötig | Nein (eigene Nummer) | Ja (Absender-Nummer) | Nein |
| Startzeit beim Senden | ~1–2s | ~2–5s (JVM-Start) | ~0,5s (HTTP) |
| Ende-zu-Ende-Verschlüsselung | Ja | Ja | Nur in Secret Chats |
| Verbreitung | Sehr hoch | Mittel | Hoch |
| Offizielle API | Nein (inoffiziell) | Nein (inoffiziell) | Ja (offiziell) |
| Empfehlung für BBFon | Wenn alle WhatsApp nutzen | Wenn Datenschutz Priorität hat | Für einfachsten Einstieg |
