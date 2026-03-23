"""
Batterie-Überwachungsdienst via psutil.
"""

from __future__ import annotations

import threading
import time

import psutil

from config import BatteryConfig
from services import console_log as log
from services.notification.interface import NotificationService


class BatteryMonitorService:
    def __init__(
        self,
        config: BatteryConfig,
        notification: NotificationService,
        debug_mode: bool,
    ) -> None:
        self._config = config
        self._notification = notification
        self._debug_mode = debug_mode

        initial = self._read_battery_percent()
        self._was_above_threshold = initial is None or initial >= self._config.threshold_percent

        if initial is None:
            log.info("[BBFon] Batterie: kein Akku erkannt (Desktop?). Überwachung läuft trotzdem.")
        else:
            log.info(
                f"[BBFon] Batterie: aktuell {initial:.0f}% | "
                f"Schwellwert: {self._config.threshold_percent}% | "
                f"Prüfintervall: {self._config.check_interval_seconds}s"
            )

    def run(self, stop_event: threading.Event) -> None:
        """Hauptschleife – läuft in eigenem Thread."""
        import asyncio

        while not stop_event.wait(timeout=self._config.check_interval_seconds):
            percent = self._read_battery_percent()
            if percent is None:
                continue

            is_below = percent < self._config.threshold_percent

            if self._debug_mode:
                log.debug(
                    f"\n[DEBUG] Batterie-Check: {percent:.0f}% "
                    f"(Schwellwert: {self._config.threshold_percent}%, "
                    f"{'UNTER' if is_below else 'über'} Schwellwert, "
                    f"fallende Flanke: {'ja' if is_below and self._was_above_threshold else 'nein'})"
                )

            if is_below and self._was_above_threshold:
                self._was_above_threshold = False
                log.alarm(
                    f"\n[BBFon] Batterie unter {self._config.threshold_percent}%! "
                    f"Aktuell: {percent:.0f}%. Sende Nachricht..."
                )
                # asyncio.run in einem eigenen Thread aufrufen
                try:
                    asyncio.run(self._send(percent))
                except Exception as ex:
                    log.error(f"[BBFon] Fehler beim Senden der Batterie-Warnung: {ex}")
            elif not is_below:
                self._was_above_threshold = True

    async def _send(self, percent: float) -> None:
        try:
            await self._notification.send(f"{self._config.message} ({percent:.0f}%)")
            log.success("[BBFon] Batterie-Warnung gesendet.")
        except Exception as ex:
            log.error(f"[BBFon] Fehler beim Senden der Batterie-Warnung: {ex}")

    @staticmethod
    def _read_battery_percent() -> float | None:
        """Gibt den Batteriestand in % zurück oder None wenn kein Akku."""
        try:
            battery = psutil.sensors_battery()
            if battery is None:
                return None
            return battery.percent
        except Exception:
            return None
