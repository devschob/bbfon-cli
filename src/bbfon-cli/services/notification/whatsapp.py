"""
WhatsApp-Benachrichtigungsdienst via mudslide.
"""

from __future__ import annotations

import os
import subprocess

from config import WhatsAppConfig
from services import console_log as log
from services.notification.interface import NotificationService

_SCRIPT_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
_TIMEOUT = 30


class WhatsAppNotificationService(NotificationService):
    def __init__(self, config: WhatsAppConfig) -> None:
        self._config = config

    def _resolve_cli(self) -> str:
        if os.path.isabs(self._config.cli_path):
            return self._config.cli_path
        return os.path.join(_SCRIPT_DIR, self._config.cli_path)

    async def send(self, message: str, attachments: list[str] | None = None) -> None:
        log.info("[BBFon] Sende WhatsApp...")
        cli = self._resolve_cli()

        if message:
            self._run_mudslide(cli, ["send", self._config.recipient, message])

        if attachments:
            for a in attachments:
                self._run_mudslide(cli, ["send-file", self._config.recipient, os.path.abspath(a)])

    def _run_mudslide(self, cli: str, args: list[str]) -> None:
        cmd = [cli] + args
        try:
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=_TIMEOUT,
            )
        except FileNotFoundError:
            raise RuntimeError(
                f"mudslide konnte nicht gestartet werden – Pfad prüfen: \"{cli}\"\n"
                f"mudslide herunterladen: https://github.com/robvanderleek/mudslide/releases"
            )
        except subprocess.TimeoutExpired:
            raise RuntimeError(
                f"mudslide hat nach {_TIMEOUT}s nicht geantwortet (Timeout)."
            )

        if result.returncode != 0:
            raise RuntimeError(
                f"mudslide Fehler (Exit {result.returncode}): {result.stderr}"
            )
