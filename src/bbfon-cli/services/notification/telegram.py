"""
Telegram-Benachrichtigungsdienst.
"""

from __future__ import annotations

import os

import httpx

from config import TelegramConfig
from services import console_log as log
from services.notification.interface import NotificationService


class TelegramNotificationService(NotificationService):
    def __init__(self, config: TelegramConfig) -> None:
        self._config = config

    async def send(self, message: str, attachments: list[str] | None = None) -> None:
        log.info("[BBFon] Sende Telegram...")

        async with httpx.AsyncClient(timeout=30.0) as client:
            if message:
                await self._send_text(client, message)

            if attachments:
                for file_path in attachments:
                    await self._send_document(client, file_path)

    async def _send_text(self, client: httpx.AsyncClient, message: str) -> None:
        url = f"https://api.telegram.org/bot{self._config.bot_token}/sendMessage"
        payload = {"chat_id": self._config.chat_id, "text": message}
        response = await client.post(url, json=payload)

        if not response.is_success:
            raise RuntimeError(
                f"Telegram Fehler ({response.status_code}): {response.text}"
            )

    async def _send_document(self, client: httpx.AsyncClient, file_path: str) -> None:
        url = f"https://api.telegram.org/bot{self._config.bot_token}/sendDocument"
        file_path = os.path.abspath(file_path)

        with open(file_path, "rb") as f:
            file_data = f.read()

        files = {
            "document": (os.path.basename(file_path), file_data),
        }
        data = {"chat_id": self._config.chat_id}
        response = await client.post(url, data=data, files=files)

        if not response.is_success:
            raise RuntimeError(
                f"Telegram sendDocument Fehler ({response.status_code}): {response.text}"
            )
