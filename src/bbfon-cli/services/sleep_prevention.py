"""
Schlafmodus-Verhinderung via systemd-inhibit (Linux).
"""

from __future__ import annotations

import subprocess
from types import TracebackType

from services import console_log as log


class SleepPreventionService:
    def __init__(self) -> None:
        self._process: subprocess.Popen | None = None

    def __enter__(self) -> "SleepPreventionService":
        try:
            self._process = subprocess.Popen(
                [
                    "systemd-inhibit",
                    "--what=sleep:idle",
                    "--who=BBFon",
                    "--why=Babyfon",
                    "--mode=block",
                    "sleep", "infinity",
                ],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
            )
        except FileNotFoundError:
            log.warning(
                "[BBFon] systemd-inhibit nicht gefunden – Schlafmodus-Verhinderung nicht aktiv."
            )
            self._process = None
        except Exception as ex:
            log.warning(f"[BBFon] Schlafmodus-Verhinderung fehlgeschlagen: {ex}")
            self._process = None
        return self

    def __exit__(
        self,
        exc_type: type[BaseException] | None,
        exc_val: BaseException | None,
        exc_tb: TracebackType | None,
    ) -> None:
        if self._process is not None:
            try:
                self._process.kill()
                self._process.wait(timeout=5)
            except Exception:
                pass
            self._process = None
