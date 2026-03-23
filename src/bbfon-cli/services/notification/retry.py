"""
Retry-Wrapper für Benachrichtigungsdienste mit Netzwerk-Warten.
"""

from __future__ import annotations

import asyncio
import socket

from services import console_log as log
from services.notification.interface import NotificationService

_MAX_ATTEMPTS = 3
_RETRY_DELAY = 5
_NETWORK_WAIT_SECONDS = 30


def _is_network_available() -> bool:
    """Prüft ob Netzwerk verfügbar ist via TCP-Verbindung zu 8.8.8.8."""
    try:
        sock = socket.create_connection(("8.8.8.8", 53), timeout=3)
        sock.close()
        return True
    except OSError:
        return False


async def _wait_for_network() -> None:
    """Wartet auf Netzwerkverfügbarkeit."""
    if _is_network_available():
        return

    log.warning("[BBFon] Kein Netzwerk erkannt, warte auf Verbindung...")
    loop = asyncio.get_running_loop()
    deadline = loop.time() + _NETWORK_WAIT_SECONDS

    while not _is_network_available():
        remaining = deadline - loop.time()
        if remaining <= 0:
            raise RuntimeError(f"Netzwerk nach {_NETWORK_WAIT_SECONDS}s nicht verfügbar.")
        await asyncio.sleep(2)

    log.success("[BBFon] Netzwerk verfügbar.")


class RetryNotificationService(NotificationService):
    def __init__(self, inner: NotificationService) -> None:
        self._inner = inner

    async def send(self, message: str, attachments: list[str] | None = None) -> None:
        for attempt in range(1, _MAX_ATTEMPTS + 1):
            await _wait_for_network()

            try:
                await self._inner.send(message, attachments)
                return
            except Exception as ex:
                if attempt == _MAX_ATTEMPTS:
                    raise

                log.warning(
                    f"[BBFon] Senden fehlgeschlagen (Versuch {attempt}/{_MAX_ATTEMPTS}): {ex}"
                )
                log.warning(f"[BBFon] Nächster Versuch in {_RETRY_DELAY}s...")
                await asyncio.sleep(_RETRY_DELAY)
