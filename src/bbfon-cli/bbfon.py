#!/usr/bin/env python3
"""
BBFon – Babyfon / Geräusch-Monitor
Haupt-Einstiegspunkt.
"""

from __future__ import annotations

import argparse
import asyncio
import os
import subprocess
import sys
import threading

# Projektverzeichnis zum Python-Suchpfad hinzufügen
_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _SCRIPT_DIR)

from config import AppConfig, load_config, save_config
from services import console_log as log
from services.config_validator import validate

VERSION = "1.0.0"
APPSETTINGS_PATH = os.path.join(_SCRIPT_DIR, "appsettings.json")


# ---------------------------------------------------------------------------
# Hilfsfunktionen
# ---------------------------------------------------------------------------

def _print_settings(cfg: AppConfig) -> None:
    log.info("[BBFon] --- Einstellungen ---")
    log.info(f"[BBFon]   Provider:       {cfg.provider}")
    audio_device = "Standard" if not cfg.audio_device else f'"{cfg.audio_device}"'
    log.info(f"[BBFon]   Audio-Eingang:  {audio_device}")
    log.info(
        f"[BBFon]   Startnachricht: "
        + (f'"{cfg.startup.message}"' if cfg.startup.enabled else "inaktiv")
    )
    log.info(f"[BBFon]   Triggers ({len(cfg.triggers)}):")
    for i, t in enumerate(cfg.triggers):
        analyse = (
            f"Analyse {t.analysis.min_trigger_count}x/{t.analysis.window_seconds}s"
            if t.analysis.enabled
            else "direkt"
        )
        rec = " | Aufnahme" if t.is_recording else ""
        log.info(
            f'[BBFon]     T{i + 1}: {t.threshold:.3f} | Cooldown {t.cooldown_seconds}s | '
            f'{analyse}{rec} → "{t.message}"'
        )

    any_rec = any(t.is_recording for t in cfg.triggers)
    rec_parts = []
    if cfg.recording.max_files > 0 and any_rec:
        rec_parts.append(f"max. {cfg.recording.max_files} Dateien")
    if cfg.recording.max_age_days > 0 and any_rec:
        rec_parts.append(f"max. {cfg.recording.max_age_days} Tage")
    if cfg.recording.send_attachments and any_rec:
        rec_parts.append("Anhänge senden")
    rec_info = ", ".join(rec_parts) if rec_parts else "keine (reine Lautstärken-Analyse)"
    log.info(f"[BBFon]   Aufnahme:       {rec_info}")

    comp_info = (
        f"aktiv ({cfg.compression.format.upper()}, {cfg.compression.bitrate_kbps}kbps, "
        f"WAV behalten: {cfg.compression.keep_wav_audio})"
        if cfg.compression.enabled
        else "inaktiv"
    )
    log.info(f"[BBFon]   Komprimierung:  {comp_info}")

    cam_device = "auto" if not cfg.camera.device_name else f'"{cfg.camera.device_name}"'
    cam_scale  = f", {cfg.camera.scale_width}px" if cfg.camera.scale_width > 0 else ""
    cam_mux    = (
        f", mit Audio{' (WAV behalten)' if cfg.camera.keep_mux_audio else ''}"
        if cfg.camera.mux_with_audio
        else ""
    )
    cam_info = (
        f"aktiv ({cfg.recording.duration_seconds}s, {cfg.camera.format.upper()}"
        f"{cam_scale}{cam_mux}, Gerät: {cam_device})"
        if cfg.camera.enabled
        else "inaktiv"
    )
    log.info(f"[BBFon]   Kamera:         {cam_info}")

    bat_info = (
        f"aktiv (< {cfg.battery.threshold_percent}%, alle {cfg.battery.check_interval_seconds}s)"
        if cfg.battery.enabled
        else "inaktiv"
    )
    log.info(f"[BBFon]   Batterie:       {bat_info}")
    log.info("[BBFon] -------------------------")


def _check_java_version() -> None:
    try:
        result = subprocess.run(
            ["java", "-version"],
            capture_output=True,
            text=True,
            timeout=10,
        )
        # java -version schreibt auf stderr
        output = result.stderr or result.stdout
        import re
        match = re.search(r'version "(\d+)(?:\.(\d+))?', output)
        if not match:
            _warn_java_not_found()
            return
        major = int(match.group(1))
        if major == 1 and match.group(2):
            major = int(match.group(2))
        if major < 25:
            log.warning(
                f"[BBFon] Java {major} erkannt – signal-cli benötigt mindestens Java 25 "
                "(empfohlen: Java 25)."
            )
            log.warning("[BBFon] Java 25 installieren: https://adoptium.net/")
    except (FileNotFoundError, subprocess.TimeoutExpired):
        _warn_java_not_found()


def _warn_java_not_found() -> None:
    log.warning("[BBFon] Java nicht gefunden – signal-cli wird nicht funktionieren.")
    log.warning("[BBFon] Java 25 installieren: https://adoptium.net/")


def _build_notification_service(cfg: AppConfig, debug_mode: bool):
    """Erstellt den passenden Benachrichtigungsdienst."""
    from services.notification.debug import DebugNotificationService
    from services.notification.retry import RetryNotificationService

    if debug_mode:
        return DebugNotificationService()

    provider = cfg.provider.lower()
    if provider == "signal":
        from services.notification.signal_svc import SignalNotificationService
        base = SignalNotificationService(cfg.signal)
    elif provider == "telegram":
        from services.notification.telegram import TelegramNotificationService
        base = TelegramNotificationService(cfg.telegram)
    elif provider == "whatsapp":
        from services.notification.whatsapp import WhatsAppNotificationService
        base = WhatsAppNotificationService(cfg.whatsapp)
    else:
        raise ValueError(f'Unbekannter Provider "{cfg.provider}".')

    return RetryNotificationService(base)


# ---------------------------------------------------------------------------
# Haupt-Logik
# ---------------------------------------------------------------------------

def main() -> None:
    # --version und --help vor argparse behandeln
    if "--version" in sys.argv or "-v" in sys.argv:
        print(f"BBFon {VERSION}")
        return

    if "--help" in sys.argv or "-h" in sys.argv:
        print("""BBFon – Babyfon / Geräusch-Monitor

Verwendung:
  bbfon [Optionen]

Optionen:
  --provider <Signal|Telegram|WhatsApp>   Benachrichtigungs-Provider setzen
  --link [Telefonnummer]                  Signal/WhatsApp-Gerät verknüpfen / Telegram-Token setzen
  --test                         Testnachricht senden und beenden
  --calibrate                    Hintergrundlärm messen, Threshold vorschlagen
  --list-video                   Verfügbare Kamera-Geräte auflisten
  --list-audio                   Verfügbare Audio-Eingabegeräte auflisten
  --debug, -d                    Debug-Modus (kein echtes Senden)
  --version, -v                  Programmversion anzeigen
  --help, -h                     Diese Hilfe anzeigen

Beispiele:
  bbfon --provider Signal --link +4912345678
  bbfon --provider Telegram --link <BOT_TOKEN>
  bbfon --provider WhatsApp --link +4912345678
  bbfon --test
  bbfon --debug

Requirements:
  Signal:   Java 25+       https://adoptium.net/
            signal-cli 0.14.1  https://github.com/AsamK/signal-cli/releases
  Telegram: Bot-Token von @BotFather (t.me/BotFather)
              → /newbot eingeben, Name & Username wählen
              → Token aus der Antwort kopieren
              → bbfon --provider Telegram --link <TOKEN>
  WhatsApp: mudslide   https://github.com/robvanderleek/mudslide/releases
  Video:    ffmpeg 8+      https://ffmpeg.org/download.html
""")
        return

    # Argparse
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--provider")
    parser.add_argument("--link", nargs="?", const=True, default=None)
    parser.add_argument("--test", action="store_true")
    parser.add_argument("--calibrate", action="store_true")
    parser.add_argument("--list-video", action="store_true")
    parser.add_argument("--list-audio", action="store_true")
    parser.add_argument("--debug", "-d", action="store_true")
    parser.add_argument("--version", "-v", action="store_true")
    parser.add_argument("--help", "-h", action="store_true")

    args = parser.parse_args()

    debug_mode      = args.debug
    link_mode       = args.link is not None
    # link kann True (kein Wert) oder ein String sein
    link_token      = args.link if isinstance(args.link, str) else None
    test_mode       = args.test
    calibrate_mode  = args.calibrate
    list_cameras    = args.list_video
    list_audio      = args.list_audio
    provider_arg    = args.provider

    # --provider setzen
    if provider_arg is not None:
        if provider_arg.lower() not in ("signal", "telegram", "whatsapp"):
            log.error(
                f'[BBFon] Unbekannter Provider "{provider_arg}". '
                "Erlaubt: Signal, Telegram, WhatsApp"
            )
            return
        save_config(APPSETTINGS_PATH, {"Provider": provider_arg})
        log.success(f'[BBFon] Provider auf "{provider_arg}" gesetzt.')
        if not link_mode and not test_mode:
            return

    # Konfiguration laden
    if not os.path.exists(APPSETTINGS_PATH):
        log.error(f"[BBFon] appsettings.json nicht gefunden: {APPSETTINGS_PATH}")
        log.error("[BBFon] Bitte appsettings.json im Programmverzeichnis anlegen.")
        return

    try:
        cfg = load_config(APPSETTINGS_PATH)
    except Exception as ex:
        log.error(f"[BBFon] appsettings.json konnte nicht geladen werden: {ex}")
        return

    # Java-Prüfung für Signal
    if cfg.provider.lower() == "signal":
        _check_java_version()

    # --link
    if link_mode:
        provider = cfg.provider.lower()
        if provider == "signal":
            from services.link.signal_link import SignalLinkService
            svc = SignalLinkService(cfg.signal, APPSETTINGS_PATH)
            svc.run(link_token)
            return

        if provider == "telegram":
            token = link_token or cfg.telegram.bot_token
            if not token or not token.strip():
                log.error("[BBFon] Kein Bot-Token angegeben.")
                log.error(
                    "[BBFon] Verwendung: bbfon --link <BOT_TOKEN>  oder  "
                    "BotToken in appsettings.json setzen."
                )
                return
            from services.link.telegram_link import TelegramLinkService
            svc = TelegramLinkService(APPSETTINGS_PATH)
            asyncio.run(svc.run(token))
            return

        if provider == "whatsapp":
            from services.link.whatsapp_link import WhatsAppLinkService
            svc = WhatsAppLinkService(cfg.whatsapp, APPSETTINGS_PATH)
            svc.run(link_token)
            return

        log.error(
            f'[BBFon] --link ist nur für Provider "Signal", "Telegram" oder "WhatsApp" '
            f'verfügbar (aktuell: "{cfg.provider}").'
        )
        return

    # --calibrate
    if calibrate_mode:
        from services.calibrate import CalibrateService
        CalibrateService().run()
        return

    # --list-video
    if list_cameras:
        from services.camera_recorder import CameraRecorderService
        cam_svc = CameraRecorderService(cfg.camera, cfg.recording, cfg.ffmpeg_path, _SCRIPT_DIR)
        devices = cam_svc.list_devices()
        if not devices:
            log.warning("[BBFon] Keine V4L2-Videogeräte gefunden. Ist ffmpeg/v4l2-ctl installiert?")
        else:
            log.info("[BBFon] Verfügbare Kamera-Geräte:")
            for d in devices:
                log.info(f'  - "{d}"')
            log.info('[BBFon] Trage den gewünschten Namen als Camera.DeviceName in appsettings.json ein.')
        return

    # --list-audio
    if list_audio:
        from services.audio_monitor import AudioMonitorService
        devices = AudioMonitorService.list_devices()
        if not devices:
            log.warning("[BBFon] Keine Audio-Eingabegeräte gefunden.")
        else:
            log.info("[BBFon] Verfügbare Audio-Eingabegeräte:")
            for i, d in enumerate(devices):
                log.info(f'  [{i}] "{d}"')
            log.info('[BBFon] Trage den gewünschten Namen als AudioDevice in appsettings.json ein.')
        return

    # Konfigurationsvalidierung
    errors = validate(cfg)
    if errors:
        log.error("[BBFon] Konfigurationsfehler – bitte appsettings.json prüfen:")
        for err in errors:
            log.error(f"  ! {err}")
        return

    log.info(
        f"[BBFon] Starte... | Provider: {cfg.provider}"
        + (" | DEBUG-Modus" if debug_mode else "")
    )
    _print_settings(cfg)

    # Benachrichtigungsdienst aufbauen
    try:
        notification = _build_notification_service(cfg, debug_mode)
    except Exception as ex:
        log.error(f"[BBFon] Fehler beim Erstellen des Benachrichtigungsdienstes: {ex}")
        return

    # Startnachricht
    if cfg.startup.enabled and not debug_mode:
        try:
            asyncio.run(notification.send(cfg.startup.message))
            log.success(f'[BBFon] Startnachricht gesendet: "{cfg.startup.message}"')
        except Exception as ex:
            log.warning(f"[BBFon] Startnachricht fehlgeschlagen: {ex}")

    # --test
    if test_mode:
        log.info("[BBFon] Sende Testnachricht...")
        try:
            asyncio.run(notification.send("BBFon Test – Konfiguration funktioniert!"))
            log.success("[BBFon] Testnachricht erfolgreich gesendet.")
        except Exception as ex:
            log.error(f"[BBFon] Fehler: {ex}")
        return

    # Kamera-Dienst
    camera = None
    if cfg.camera.enabled:
        from services.camera_recorder import CameraRecorderService
        camera = CameraRecorderService(cfg.camera, cfg.recording, cfg.ffmpeg_path, _SCRIPT_DIR)

    # Stop-Event
    stop_event = threading.Event()

    def on_sigint(signum, frame):
        stop_event.set()

    import signal
    signal.signal(signal.SIGINT, on_sigint)
    signal.signal(signal.SIGTERM, on_sigint)

    # Tastendruck-Thread (Q / Esc zum Beenden)
    def key_watcher():
        try:
            import termios
            import tty

            fd = sys.stdin.fileno()
            old_settings = termios.tcgetattr(fd)
            try:
                tty.setraw(fd)
                while not stop_event.is_set():
                    import select
                    r, _, _ = select.select([sys.stdin], [], [], 0.1)
                    if r:
                        ch = sys.stdin.read(1)
                        if ch in ("q", "Q", "\x1b"):  # Q oder Esc
                            stop_event.set()
                            break
            finally:
                termios.tcsetattr(fd, termios.TCSADRAIN, old_settings)
        except Exception:
            pass  # Stdin nicht verfügbar (z.B. Dienst-Modus)

    key_thread = threading.Thread(target=key_watcher, daemon=True)
    key_thread.start()

    # Schlafmodus verhindern
    from services.sleep_prevention import SleepPreventionService

    with SleepPreventionService():
        # Batterie-Überwachung
        if cfg.battery.enabled:
            from services.battery_monitor import BatteryMonitorService
            battery_monitor = BatteryMonitorService(cfg.battery, notification, debug_mode)
            bat_thread = threading.Thread(
                target=battery_monitor.run,
                args=(stop_event,),
                daemon=True,
            )
            bat_thread.start()

        # Audio-Überwachung starten
        from services.audio_monitor import AudioMonitorService
        monitor = AudioMonitorService(cfg, notification, debug_mode, camera, _SCRIPT_DIR)
        monitor.start(stop_event, APPSETTINGS_PATH)

    log.info("\n[BBFon] Beendet.")


if __name__ == "__main__":
    main()
