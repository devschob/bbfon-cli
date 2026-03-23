"""
Abstrakte Basisklasse für Benachrichtigungsdienste.
"""

from __future__ import annotations

from abc import ABC, abstractmethod


class NotificationService(ABC):
    @abstractmethod
    async def send(self, message: str, attachments: list[str] | None = None) -> None:
        """Sendet eine Nachricht, optional mit Datei-Anhängen."""
        ...
