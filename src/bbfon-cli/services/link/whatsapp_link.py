"""
WhatsApp-Verlinkung via mudslide login.
"""

from __future__ import annotations

import os
import subprocess

from config import WhatsAppConfig, save_config
from services import console_log as log

_SCRIPT_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


class WhatsAppLinkService:
    def __init__(self, config: WhatsAppConfig, appsettings_path: str) -> None:
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
        log.info("[BBFon] Starte WhatsApp-Verlinkung...")
        log.info("[BBFon] mudslide wird gestartet, bitte warten...\n")

        log.info("[BBFon] Scanne den QR-Code mit deiner WhatsApp-App:")
        log.info("[BBFon]   WhatsApp → Einstellungen → Verknüpfte Geräte → Gerät hinzufügen")
        log.info("[BBFon] Warte auf Verlinkung...\n")

        try:
            # Kein Redirect – mudslide schreibt QR-Code direkt in die Konsole
            process = subprocess.Popen(
                [cli, "login"],
                stdout=None,
                stderr=None,
            )
        except FileNotFoundError:
            log.error(f'[BBFon] mudslide konnte nicht gestartet werden – Pfad prüfen: "{cli}"')
            log.error("[BBFon] mudslide herunterladen: https://github.com/robvanderleek/mudslide/releases")
            return

        process.wait()

        if process.returncode == 0:
            log.success("[BBFon] Erfolgreich verknüpft! BBFon kann jetzt WhatsApp nutzen.")
        else:
            log.error(
                f"[BBFon] Verlinkung fehlgeschlagen (Exit {process.returncode}). "
                "WhatsApp-App nochmal versuchen."
            )

    def _save_number(self, phone: str) -> None:
        save_config(self._appsettings_path, {
            "WhatsApp": {
                "Sender": phone,
                "Recipient": phone,
            }
        })
        log.info(f"[BBFon] Nummer {phone} in appsettings.json eingetragen (Sender + Recipient).")
