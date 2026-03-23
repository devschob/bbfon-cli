"""
Telegram-Verlinkung: liest Chat-ID via getUpdates ab und speichert sie.
"""

from __future__ import annotations

import httpx

from config import save_config
from services import console_log as log


class TelegramLinkService:
    def __init__(self, appsettings_path: str) -> None:
        self._appsettings_path = appsettings_path

    async def run(self, bot_token: str) -> None:
        log.info("[BBFon] Rufe Telegram getUpdates ab...")

        url = f"https://api.telegram.org/bot{bot_token}/getUpdates"
        try:
            async with httpx.AsyncClient(timeout=15.0) as client:
                response = await client.get(url)
                body = response.json()
        except Exception as ex:
            log.error(f"[BBFon] Verbindung fehlgeschlagen: {ex}")
            return

        if not response.is_success:
            description = body.get("description", str(body))
            log.error(f"[BBFon] Telegram API Fehler: {description}")
            return

        result = body.get("result", [])
        if not result:
            log.warning("[BBFon] Keine Nachrichten gefunden.")
            log.info("[BBFon] Senden Sie zuerst eine Nachricht an den Bot, dann erneut ausführen.")
            return

        found: list[tuple[int, str]] = []
        seen: set[int] = set()

        for update in result:
            message = update.get("message")
            if not message:
                continue
            chat = message.get("chat")
            if not chat:
                continue
            chat_id = chat.get("id")
            if chat_id is None:
                continue
            if chat_id in seen:
                continue
            seen.add(chat_id)
            found.append((chat_id, self._get_chat_name(chat)))

        if not found:
            log.warning("[BBFon] Keine auswertbaren Nachrichten im Ergebnis.")
            log.info("[BBFon] Senden Sie zuerst eine Nachricht an den Bot, dann erneut ausführen.")
            return

        for chat_id, name in found:
            log.success(f"[BBFon] Chat-ID gefunden: {chat_id}  ({name})")

        save_id, _ = found[0]
        if len(found) > 1:
            log.info(f"[BBFon] Mehrere Chats gefunden – erste ID wird gespeichert: {save_id}")

        save_config(self._appsettings_path, {
            "Telegram": {
                "BotToken": bot_token,
                "ChatId": str(save_id),
            }
        })
        log.success(f"[BBFon] appsettings.json aktualisiert: BotToken + ChatId ({save_id}).")

    @staticmethod
    def _get_chat_name(chat: dict) -> str:
        chat_type = chat.get("type", "?")
        if chat_type == "private":
            first = chat.get("first_name", "")
            last = chat.get("last_name", "")
            user = chat.get("username", "")
            user_str = f"@{user}" if user else ""
            return " ".join(p for p in [first, last, user_str] if p).strip()
        title = chat.get("title", "")
        return f"{chat_type}: {title}".strip()
