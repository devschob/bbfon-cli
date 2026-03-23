"""
Kalibrierungsdienst – misst Hintergrundlärm und schlägt einen Threshold vor.
"""

from __future__ import annotations

import time

import numpy as np
import sounddevice as sd

from config import THRESHOLD_SCALE
from services import console_log as log

_SAMPLE_RATE = 16000
_BLOCKSIZE = 1600  # 100ms
_DURATION = 10.0   # Sekunden


class CalibrateService:
    def run(self) -> None:
        log.info("[BBFon] Kalibrierung startet in 3 Sekunden...")
        log.info("[BBFon] Bitte STILLE halten – kein Sprechen, keine Geräusche.\n")
        time.sleep(3)

        samples: list[float] = []

        def callback(indata: np.ndarray, frames: int, time_info, status) -> None:
            rms = float(np.sqrt(np.mean(indata.astype(np.float32) ** 2))) / 32768.0
            samples.append(rms)
            elapsed = len(samples) * 0.1
            color = "\033[33m" if rms > 0.05 else "\033[90m"
            log.inline(
                f"\r{color}[BBFon] Messe... {elapsed:.1f}s / {_DURATION:.0f}s   "
                f"Pegel: {rms * THRESHOLD_SCALE:.3f}   \033[0m"
            )

        with sd.InputStream(
            samplerate=_SAMPLE_RATE,
            channels=1,
            dtype="int16",
            blocksize=_BLOCKSIZE,
            callback=callback,
        ):
            time.sleep(_DURATION)

        print()  # Neue Zeile nach der rollenden Ausgabe

        if not samples:
            log.error("[BBFon] Keine Samples aufgenommen. Mikrofon verfügbar?")
            return

        arr = np.array(samples, dtype=np.float32)
        mean = float(arr.mean())
        stddev = float(arr.std())
        maximum = float(arr.max())
        suggested = mean + 3 * stddev
        suggested = max(suggested, 0.05)
        suggested_scaled = round(suggested * THRESHOLD_SCALE, 3)

        log.info("\n[BBFon] Kalibrierungsergebnis:")
        log.info(f"  Durchschnittspegel:    {mean * THRESHOLD_SCALE:.3f}")
        log.info(f"  Maximaler Pegel:       {maximum * THRESHOLD_SCALE:.3f}")
        log.info(f"  Standardabweichung:    {stddev * THRESHOLD_SCALE:.3f}")
        log.success(f"  Empfohlener Threshold: {suggested_scaled:.3f}")
        log.info(f'\n[BBFon] Trage in appsettings.json ein:')
        log.info(f'  "Threshold": {suggested_scaled:.3f}')

        if maximum > suggested:
            log.warning(
                f"\n[BBFon] Hinweis: Es gab Spitzen bis {maximum * THRESHOLD_SCALE:.3f} "
                "während der Stille. War es wirklich ruhig?"
            )

        self._draw_bar_chart(samples, suggested)

    @staticmethod
    def _draw_bar_chart(samples: list[float], threshold: float) -> None:
        bar_width = 42
        max_val = max(samples) * THRESHOLD_SCALE
        threshold_scaled = threshold * THRESHOLD_SCALE
        scale = max(max_val, threshold_scaled) * 1.1
        thresh_pos = min(int(threshold_scaled / scale * bar_width), bar_width - 1)

        log.info("\n[BBFon] Verlauf (100ms Intervalle):")

        for i, s in enumerate(samples):
            val = s * THRESHOLD_SCALE
            filled = int(val / scale * bar_width)
            over = val >= threshold_scaled

            bar = []
            for j in range(bar_width):
                if j == thresh_pos:
                    bar.append("|")
                elif j < filled:
                    bar.append("█")
                else:
                    bar.append("░")

            bar_str = "".join(bar)
            color = "\033[31m" if over else "\033[90m"
            over_str = " !" if over else ""
            log.inline(f"  [{i * 0.1:.1f}s] {color}{bar_str}  {val:7.3f}{over_str}\033[0m\n")

        # Legende
        padding = " " * (8 + thresh_pos)
        log.info(f"  {padding}^ {threshold_scaled:.3f} (Schwellwert)")
