"""
Debug-Benachrichtigungsdienst – schreibt nur auf die Konsole.
"""

from __future__ import annotations

import os

from services.notification.interface import NotificationService


class DebugNotificationService(NotificationService):
    async def send(self, message: str, attachments: list[str] | None = None) -> None:
        if message:
            print(f'[DEBUG] Würde senden: "{message}"')
        if attachments:
            for a in attachments:
                print(f"[DEBUG] Würde Anhang senden: {os.path.basename(a)}")
