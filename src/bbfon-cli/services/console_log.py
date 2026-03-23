"""
Thread-sicherer, farbiger Konsolenoutput mit Zeitstempel.
"""

from __future__ import annotations

import sys
import threading
from datetime import datetime

_lock = threading.Lock()

# ANSI-Farbcodes – als Modul-Konstanten exportiert, damit andere Module sie nutzen können
RESET  = "\033[0m"
WHITE  = "\033[37m"
YELLOW = "\033[33m"
RED    = "\033[31m"
GREEN  = "\033[32m"
CYAN   = "\033[36m"
GRAY   = "\033[90m"

# Private Aliase (Rückwärtskompatibilität)
_RESET  = RESET
_WHITE  = WHITE
_YELLOW = YELLOW
_RED    = RED
_GREEN  = GREEN
_CYAN   = CYAN
_GRAY   = GRAY


def _timestamp() -> str:
    return datetime.now().strftime("%H:%M:%S")


def _write(msg: str, color: str) -> None:
    with _lock:
        sys.stdout.write(f"{color}[{_timestamp()}] {msg}{RESET}\n")
        sys.stdout.flush()


def info(msg: str) -> None:
    _write(msg, WHITE)


def warning(msg: str) -> None:
    _write(msg, YELLOW)


def error(msg: str) -> None:
    _write(msg, RED)


def success(msg: str) -> None:
    _write(msg, GREEN)


def alarm(msg: str) -> None:
    _write(msg, RED)


def debug(msg: str) -> None:
    _write(msg, CYAN)


def volume(msg: str, color: str = GRAY) -> None:
    """Überschreibt die aktuelle Konsolzeile via \\r (ohne Zeilenumbruch).

    ``msg`` sollte NICHT mit \\r beginnen – der wird hier vorangestellt.
    """
    with _lock:
        sys.stdout.write(f"\r{color}{msg}{RESET}")
        sys.stdout.flush()


def inline(msg: str) -> None:
    """Schreibt ``msg`` direkt auf stdout (ohne Timestamp, ohne Zeilenumbruch).

    ANSI-Codes und \\r müssen vollständig im ``msg`` enthalten sein.
    """
    with _lock:
        sys.stdout.write(msg)
        sys.stdout.flush()
