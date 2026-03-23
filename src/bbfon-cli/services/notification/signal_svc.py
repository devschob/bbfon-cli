"""
Signal-Benachrichtigungsdienst via signal-cli.
"""

from __future__ import annotations

import os
import subprocess

from config import SignalConfig
from services import console_log as log
from services.notification.interface import NotificationService

_SCRIPT_DIR = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
_TIMEOUT = 15


class SignalNotificationService(NotificationService):
    def __init__(self, config: SignalConfig) -> None:
        self._config = config

    def _resolve_cli(self) -> str:
        if os.path.isabs(self._config.cli_path):
            return self._config.cli_path
        return os.path.join(_SCRIPT_DIR, self._config.cli_path)

    async def send(self, message: str, attachments: list[str] | None = None) -> None:
        log.info("[BBFon] Sende Signal...")
        cli = self._resolve_cli()

        # receive (optional, Fehler ignorieren)
        try:
            self._run(
                cli,
                ["-u", self._config.sender, "receive", "--timeout", "1", "--ignore-attachments"],
                timeout=5,
            )
        except Exception:
            pass

        send_args = ["-u", self._config.sender, "send", "-m", message, self._config.recipient]
        if attachments:
            for a in attachments:
                send_args += ["--attachment", os.path.abspath(a)]

        self._run(cli, send_args, timeout=_TIMEOUT)

    def _run(self, cli: str, args: list[str], timeout: int = _TIMEOUT) -> None:
        cmd = [cli] + args
        try:
            result = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                timeout=timeout,
            )
        except FileNotFoundError:
            raise RuntimeError(
                f"signal-cli konnte nicht gestartet werden – Pfad prüfen: \"{cli}\"\n"
                f"signal-cli herunterladen: https://github.com/AsamK/signal-cli/releases"
            )
        except subprocess.TimeoutExpired:
            raise RuntimeError(
                f"signal-cli hat nach {timeout}s nicht geantwortet (Timeout)."
            )

        if result.returncode != 0:
            raise RuntimeError(
                f"signal-cli Fehler (Exit {result.returncode}): {result.stderr}"
            )
