"""
Signal-Verlinkung via signal-cli mit QR-Code-Anzeige im Terminal.
"""

from __future__ import annotations

import os
import subprocess
import threading

import qrcode

from config import SignalConfig, save_config
from services import console_log as log

_SCRIPT_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


class SignalLinkService:
    def __init__(self, config: SignalConfig, appsettings_path: str) -> None:
        self._config = config
        self._appsettings_path = appsettings_path

    def _resolve_cli(self) -> str:
        if os.path.isabs(self._config.cli_path):
            return self._config.cli_path
        return os.path.join(_SCRIPT_DIR, self._config.cli_path)

    def run(self, phone_number: str | None = None) -> None:
        if phone_number:
            self._save_number(phone_number)

        cli = self._resolve_cli()
        log.info("[BBFon] Starte Signal-Verlinkung...")
        log.info("[BBFon] signal-cli wird gestartet, bitte warten...\n")

        try:
            process = subprocess.Popen(
                [cli, "link", "-n", "BBFon"],
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                text=True,
            )
        except FileNotFoundError:
            log.error(f'[BBFon] signal-cli konnte nicht gestartet werden – Pfad prüfen: "{cli}"')
            log.error("[BBFon] signal-cli herunterladen: https://github.com/AsamK/signal-cli/releases")
            return

        url_event = threading.Event()
        found_url: list[str] = []

        def read_stream(stream):
            for line in stream:
                line = line.rstrip()
                if line and "sgnl://" in line and not url_event.is_set():
                    found_url.append(line.strip())
                    url_event.set()

        t_out = threading.Thread(target=read_stream, args=(process.stdout,), daemon=True)
        t_err = threading.Thread(target=read_stream, args=(process.stderr,), daemon=True)
        t_out.start()
        t_err.start()

        got_url = url_event.wait(timeout=30)

        if not got_url:
            log.error("[BBFon] Fehler: signal-cli hat keine Verlinkungs-URL geliefert (Timeout 30s).")
            log.error("[BBFon] Prüfe ob signal-cli korrekt installiert ist und Java verfügbar ist.")
            process.kill()
            return

        url = found_url[0]
        self._display_qr(url)

        log.info(f"[BBFon] URL: {url}\n")
        log.info("[BBFon] Scanne den QR-Code jetzt mit deiner Signal-App:")
        log.info("[BBFon]   Einstellungen → Verknüpfte Geräte → (+) Gerät hinzufügen")
        log.info("[BBFon] Warte auf Verlinkung...\n")

        process.wait()

        if process.returncode == 0:
            log.success("[BBFon] Erfolgreich verknüpft! BBFon kann jetzt Signal nutzen.")
        else:
            log.error(
                f"[BBFon] Verlinkung fehlgeschlagen (Exit {process.returncode}). "
                "Signal-App nochmal versuchen."
            )

    def _display_qr(self, url: str) -> None:
        qr = qrcode.QRCode(border=2, error_correction=qrcode.constants.ERROR_CORRECT_M)
        qr.add_data(url)
        qr.make(fit=True)

        matrix = qr.get_matrix()
        lines = []
        for row in matrix:
            line = "".join("██" if cell else "  " for cell in row)
            lines.append(line)
        print("\n".join(lines))
        print()

    def _save_number(self, phone: str) -> None:
        save_config(self._appsettings_path, {
            "Signal": {
                "Sender": phone,
                "Recipient": phone,
            }
        })
        log.info(f"[BBFon] Nummer {phone} in appsettings.json eingetragen (Sender + Recipient).")
