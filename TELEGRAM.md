# Telegram – Einrichtung & Nutzung mit BBFon

## Wie funktioniert Telegram in BBFon?

BBFon nutzt die offizielle **Telegram Bot API** – einen kostenlosen HTTP-REST-Dienst von Telegram. Es wird kein extra Tool installiert. BBFon sendet direkt einen HTTP POST an Telegram, wenn ein Alarm ausgelöst wird.

Voraussetzungen:
- Ein Telegram-Account (Smartphone oder Desktop)
- Internetzugang vom Windows-PC aus zu `api.telegram.org`

---

## Systemanforderungen

| Anforderung | Details |
|---|---|
| **Telegram-Account** | Für die Einrichtung des Bots |
| **Netzwerk** | Ausgehende HTTPS-Verbindung zu `api.telegram.org` (Port 443) |
| **Firewall** | `api.telegram.org` muss erreichbar sein |
| **Kein extra Tool** | Alles läuft über .NET `HttpClient` direkt in BBFon |

---

## Schritt 1 – Bot erstellen (BotFather)

Telegram-Bots werden über den offiziellen `@BotFather` erstellt.

1. Telegram öffnen und `@BotFather` suchen (verifizierter Account mit blauem Haken)
2. `/newbot` senden
3. BotFather fragt nach einem **Anzeigenamen** (frei wählbar, z.B. `BBFon Alarm`)
4. BotFather fragt nach einem **Benutzernamen** – muss eindeutig sein und auf `bot` enden, z.B. `MeinBBFonBot`
5. BotFather antwortet mit dem **Bot-Token**:

```
Alles klar. Hier ist dein Bot-Token:

1234567890:ABCDefGhIJKlmNoPQRsTUVwXyz-1234567

Bewahre diesen Token sicher auf.
```

> **Sicherheitshinweis:** Der Token ist wie ein Passwort. Wer ihn kennt, kann Nachrichten im Namen des Bots senden. Nicht in öffentliche Repositories einchecken.

---

## Schritt 2 – Chat-ID herausfinden und speichern

Die Chat-ID identifiziert, **an wen** der Bot schreibt. BBFon kann das automatisch erledigen.

### Automatisch mit --link (empfohlen)

1. Den eigenen Bot in Telegram suchen (nach `@MeinBBFonBot` suchen)
2. Auf **Start** klicken oder eine beliebige Nachricht schreiben – damit getUpdates einen Eintrag hat
3. BBFon mit `--link` und dem Bot-Token aufrufen:

```cmd
BBFon.exe --link 1234567890:ABCDefGhIJKlmNoPQRsTUVwXyz-1234567
```

BBFon ruft automatisch `getUpdates` ab, zeigt die gefundene Chat-ID an und speichert **Token und Chat-ID direkt in `appsettings.json`**:

```
[BBFon] Rufe Telegram getUpdates ab...
[BBFon] Chat-ID gefunden: 987654321  (Max Mustermann @maxmuster)
[BBFon] appsettings.json aktualisiert: BotToken + ChatId (987654321).
```

Danach ist BBFon sofort einsatzbereit – kein manuelles Eintragen nötig.

> **Keine Nachrichten gefunden?** → `"Senden Sie zuerst eine Nachricht an den Bot, dann erneut ausführen."` – Schritt 2 oben wiederholen.

> **Mehrere Chats gefunden** (z. B. Bot ist in einer Gruppe)? Alle werden angezeigt, die erste ID wird gespeichert. Ggf. `ChatId` in `appsettings.json` manuell auf die gewünschte ID setzen.

---

### Manuell (Alternative)

Falls `--link` nicht verwendet werden soll:

1. Den eigenen Bot in Telegram suchen und `/start` schreiben
2. Im Browser folgende URL aufrufen (Token einsetzen):

```
https://api.telegram.org/bot1234567890:ABCDefGhIJKlmNoPQRsTUVwXyz-1234567/getUpdates
```

3. Den Wert von `chat.id` aus der JSON-Antwort ablesen und manuell in `appsettings.json` eintragen.

> **Leere Antwort (`"result": []`)?** Schreibe erst `/start` an den Bot, dann erneut aufrufen.

---

## Schritt 3 – appsettings.json prüfen

Nach `--link` ist `appsettings.json` bereits vollständig befüllt:

```json
"Provider": "Telegram",
"Telegram": {
  "BotToken": "1234567890:ABCDefGhIJKlmNoPQRsTUVwXyz-1234567",
  "ChatId": "987654321"
}
```

---

## Nachrichten an eine Gruppe senden

So kann BBFon z.B. in einen Familien- oder Team-Chat schreiben.

1. Den Bot zur Gruppe hinzufügen (Gruppeninfo → Mitglieder → Mitglied hinzufügen → Botname suchen)
2. Eine beliebige Nachricht in der Gruppe schreiben (damit getUpdates einen Eintrag erzeugt)
3. `getUpdates`-URL aufrufen (s.o.)
4. Gruppen-Chat-IDs sind **negativ**, z.B. `-1001234567890`
5. Diese ID als `ChatId` eintragen

> **Gruppe findet sich nicht in getUpdates?** Sicherstellen, dass der Bot Nachrichten in der Gruppe lesen darf. Bei Supergroups ggf. Datenschutzeinstellungen des Bots über BotFather anpassen: `/setprivacy` → `Disable`.

---

## Nachrichten an einen Kanal senden

1. Bot als **Administrator** zum Kanal hinzufügen
2. Kanal-Username als ChatId verwenden: `@MeinKanalName`
   ```json
   "ChatId": "@MeinKanalName"
   ```
   Oder die numerische Kanal-ID (beginnt mit `-100...`), die ebenfalls über getUpdates ermittelt werden kann.

---

## Verbindung testen

Direkt im Browser oder per curl testen, ob Bot-Token und Chat-ID korrekt sind:

```
https://api.telegram.org/bot<TOKEN>/sendMessage?chat_id=<CHAT_ID>&text=Testmessage
```

Antwort bei Erfolg:
```json
{ "ok": true, "result": { "message_id": 42, ... } }
```

Antwort bei falschem Token:
```json
{ "ok": false, "error_code": 401, "description": "Unauthorized" }
```

Antwort bei falscher Chat-ID:
```json
{ "ok": false, "error_code": 400, "description": "Bad Request: chat not found" }
```

---

## Häufige Fehler

| Fehler | Ursache | Lösung |
|---|---|---|
| `401 Unauthorized` | Bot-Token falsch oder abgelaufen | Token bei BotFather prüfen: `/mybots` → Bot auswählen → API Token |
| `400 chat not found` | Chat-ID falsch | getUpdates erneut aufrufen, ID prüfen. Bei Gruppen: Bot muss Mitglied sein |
| `403 Forbidden` | Bot wurde aus Chat entfernt oder blockiert | Bot erneut hinzufügen / entsperren |
| `getUpdates` leer | Bot hat keine Nachricht erhalten | `/start` an den Bot schreiben |
| Keine Verbindung | Firewall blockiert `api.telegram.org` | Port 443 (HTTPS) für `api.telegram.org` freigeben |
| Nachricht kommt nicht an | Gruppe: Bot kann keine Nachrichten lesen/schreiben | Botfather: `/setprivacy` → `Disable` für die Gruppe |

---

## Bot-Token erneuern (falls kompromittiert)

Falls der Token in falsche Hände geraten ist:

1. BotFather öffnen
2. `/mybots` → Bot auswählen → **API Token** → **Revoke current token**
3. Neuen Token in `appsettings.json` eintragen
4. BBFon neu starten (oder Datei speichern → Hot Reload greift automatisch)

---

## Telegram vs. Signal – Vergleich für BBFon

| Kriterium | Telegram | Signal |
|---|---|---|
| Einrichtungsaufwand | Gering (nur Bot + Chat-ID) | Hoch (Java, signal-cli, Registrierung) |
| Extra Software | Keine | Java 17+, signal-cli |
| Zweite Nummer nötig | Nein | Ja (Absender-Nummer) |
| Startzeit beim Senden | ~0,5s (HTTP) | ~2–5s (JVM-Start) |
| Ende-zu-Ende-Verschlüsselung | Nur in Secret Chats | Immer |
| Empfehlung für BBFon | Für einfachen Einstieg | Wenn Datenschutz Priorität hat |
