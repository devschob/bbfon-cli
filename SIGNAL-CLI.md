# signal-cli – Installationsanleitung & Systemanforderungen

## Was ist signal-cli?

signal-cli ist ein inoffizielles Open-Source-Tool (Java), das das Signal-Protokoll direkt implementiert. Es gibt **keine offizielle Signal-API** – signal-cli ist die einzige stabile Möglichkeit, Signal programmatisch zu nutzen.

- GitHub: https://github.com/AsamK/signal-cli
- Lizenz: GPLv3
- Maintainer: AsamK (aktiv gepflegt, regelmäßige Releases)

---

## Systemanforderungen

| Anforderung | Details |
|---|---|
| **Java** | **Java 21** (LTS, empfohlen) oder Java 17 – mind. Java 17 |
| **OS** | Windows 10/11 (x64), Linux, macOS |
| **RAM** | ~200–400 MB beim Senden (JVM-Overhead) |
| **Speicher** | ~150 MB für signal-cli + Abhängigkeiten |
| **Netzwerk** | Ausgehende HTTPS-Verbindungen zu Signal-Servern |
| **Telefonnummer** | Eine echte Mobilnummer, die SMS empfangen kann |

> **Wichtig zu VoIP-Nummern:** Signal blockiert viele VoIP-Anbieter (z.B. Google Voice, Skype-Nummern). Eine echte SIM-Karte (auch alte, deaktivierbar nach Registrierung) ist am zuverlässigsten.

---

## Java installieren

1. Eclipse Temurin (empfohlen, kostenlos): https://adoptium.net/
2. **Windows Installer (.msi)** herunterladen → Java 21 (LTS)
3. "Add to PATH" während Installation aktivieren
4. Prüfen:
   ```cmd
   java -version
   ```
   Ausgabe sollte sein: `openjdk version "21.x.x" ...`

---

## signal-cli herunterladen

1. Aktuelle Version unter https://github.com/AsamK/signal-cli/releases finden
2. Datei **`signal-cli-x.x.x.tar.gz`** herunterladen (nicht die `-lib`-Variante)
3. Entpacken, z.B. nach `C:\tools\signal-cli\`

Ordnerstruktur nach dem Entpacken:
```
C:\tools\signal-cli\
├── bin\
│   ├── signal-cli        ← Linux/macOS
│   └── signal-cli.bat    ← Windows  ← das ist die Datei die BBFon aufruft
└── lib\
    └── signal-cli-x.x.x-all.jar
```

4. Testen (Versionsnummer ausgeben):
   ```cmd
   C:\tools\signal-cli\bin\signal-cli.bat --version
   ```

---

## Methode A – Als verknüpftes Gerät einrichten (empfohlen)

Das ist der einfachste Weg, wenn du Signal bereits auf deinem Smartphone nutzt. signal-cli wird wie die Signal Desktop-App als **sekundäres Gerät** mit deiner bestehenden Nummer verknüpft. Eine zweite SIM-Karte ist **nicht nötig**.

BBFon sendet dann Nachrichten an deine eigene Nummer → sie landen im **Notizen-Chat** (Note to Self) in Signal.

```json
"Signal": {
  "Sender":    "+4917612345678",
  "Recipient": "+4917612345678"
}
```

### Mit BBFon-Parameter --link (einfachste Methode)

BBFon übernimmt den gesamten Prozess und zeigt den QR-Code direkt in der Konsole an:

```cmd
BBFon.exe --link
```

Ausgabe:
```
[BBFon] Starte Signal-Verlinkung...
[BBFon] signal-cli wird gestartet, bitte warten...

██████████████████████████████████
██                              ██
██  ████████████████████████  ████
... (QR-Code)

[BBFon] Scanne den QR-Code jetzt mit deiner Signal-App:
[BBFon]   Einstellungen → Verknüpfte Geräte → (+) Gerät hinzufügen
[BBFon] Warte auf Verlinkung...

[BBFon] Erfolgreich verknüpft! BBFon kann jetzt Signal nutzen.
```

Danach in `appsettings.json` Sender und Recipient auf die eigene Nummer setzen, dann BBFon normal starten.

### Manuell (ohne --link)

Falls du den QR-Code lieber selbst generieren möchtest:

1. URL von signal-cli ausgeben lassen:
   ```cmd
   signal-cli.bat link -n "BBFon"
   ```
   Ausgabe: `sgnl://linkdevice?uuid=...&pub_key=...`

2. URL in einen QR-Code umwandeln (z.B. auf https://www.qr-code-generator.com)

3. In Signal-App scannen:
   `Einstellungen → Verknüpfte Geräte → (+) Gerät hinzufügen`

4. Verbindung testen:
   ```cmd
   signal-cli.bat -u +4917612345678 send -m "BBFon Test" +4917612345678
   ```
   Die Nachricht erscheint im Notizen-Chat in Signal.

---

## Methode B – Neue Nummer registrieren

Das ist der aufwendigste Schritt. Du benötigst eine **eigene Absender-Nummer** – das ist die Nummer, von der BBFon Nachrichten *sendet*. Das kann eine alte SIM-Karte sein.

### Schritt 1 – Captcha lösen (seit 2022 Pflicht)

Signal verlangt bei Neuregistrierungen ein Captcha gegen Spam.

1. Im Browser öffnen: `https://signalcaptchas.org/registration/generate.html`
2. Captcha lösen (Checkbox anklicken)
3. Anschließend erscheint im Browser eine URL die beginnt mit `signalcaptcha://`
4. Diese URL komplett kopieren (z.B. per F12 → Konsole → `window.location.href` eingeben oder in der Adressleiste sehen)

Die URL sieht so aus:
```
signalcaptcha://signal-recaptcha-v2.ILoveSignal.03AGdBq25...langer Token...
```

### Schritt 2 – Registrierung starten

```cmd
signal-cli.bat -u +4915112345678 register --captcha signalcaptcha://signal-recaptcha-v2.ILoveSignal.03AGdBq25...
```

Signal sendet jetzt einen **6-stelligen SMS-Code** an `+4915112345678`.

> **Alternativ per Anruf** (falls SMS nicht klappt):
> ```cmd
> signal-cli.bat -u +4915112345678 register --voice --captcha signalcaptcha://...
> ```

### Schritt 3 – Verifizieren

```cmd
signal-cli.bat -u +4915112345678 verify 123456
```

(123456 = empfangener Code)

Bei Erfolg: keine Ausgabe oder `Verification successful`.

---

## Verbindung testen

Testmessage an die Zielnummer senden:

```cmd
signal-cli.bat send -m "Hallo von BBFon!" -u +4915112345678 +4917687654321
```

- `-u` = Absender (deine registrierte Nummer)
- letztes Argument = Empfänger

Wenn die Nachricht ankommt: alles fertig.

---

## Für BBFon einrichten

**Option A – signal-cli neben der EXE (empfohlen):**

```
BBFon\
├── BBFon.exe
├── appsettings.json
└── signal-cli\
    └── bin\
        └── signal-cli.bat
```

`appsettings.json`:
```json
"Signal": {
  "CliPath": "signal-cli\\bin\\signal-cli.bat",
  "Sender": "+4915112345678",
  "Recipient": "+4917687654321"
}
```

**Option B – Wrapper-Datei** (wenn signal-cli woanders installiert ist):

Datei `signal-cli.bat` neben `BBFon.exe` anlegen:
```bat
@echo off
"C:\tools\signal-cli\bin\signal-cli.bat" %*
```

`appsettings.json`:
```json
"Signal": {
  "CliPath": "signal-cli.bat",
  "Sender": "+4915112345678",
  "Recipient": "+4917687654321"
}
```

---

## Wo speichert signal-cli seine Daten?

Nach der Registrierung legt signal-cli die Schlüssel und Kontodaten ab unter:

```
C:\Users\<Name>\.local\share\signal-cli\
```

Diese Daten müssen **nicht** neben die EXE kopiert werden. signal-cli findet sie automatisch über den Windows-Benutzerpfad.

> Wenn BBFon auf einem anderen PC genutzt werden soll, muss die Registrierung dort wiederholt werden (oder der Datenordner kopiert werden).

---

## Häufige Fehler

| Fehler | Ursache | Lösung |
|---|---|---|
| `java` not found | Java nicht im PATH | Java neu installieren mit PATH-Option, oder absoluten Pfad in `.bat` eintragen |
| `Invalid captcha token` | Captcha abgelaufen | Neues Captcha lösen (Token ist nur kurze Zeit gültig) |
| `Rate limit exceeded` | Zu viele Versuche | 24h warten, dann neu versuchen |
| `Invalid code` | Falscher/abgelaufener SMS-Code | `register` erneut ausführen, neuen Code anfordern |
| Nummer bereits registriert | Nummer ist noch in Signal-App aktiv | In der Signal-App: Einstellungen → Konto → Verknüpfte Geräte (oder Nummer zuerst in der App abmelden) |
| Langsamer Start | JVM-Startzeit | Normal, dauert 2–5 Sekunden – BBFon wartet auf Prozessende |

---

## Hinweis: Gleichzeitige Nutzung mit Signal-App

Eine Handynummer kann **nicht gleichzeitig** in der Signal-App auf dem Smartphone **und** in signal-cli als primäres Konto registriert sein. Optionen:

- Alte SIM / zweite Nummer nur für signal-cli verwenden *(empfohlen)*
- signal-cli als verknüpftes Gerät betreiben (komplexer, aber möglich):
  ```cmd
  signal-cli.bat link -n "BBFon"
  ```
  Das gibt einen QR-Code-Link aus, der in der Signal-App unter *Verknüpfte Geräte* gescannt wird.
