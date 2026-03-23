"""
Audio-Komprimierungsdienst via ffmpeg.
"""

from __future__ import annotations

import os
import subprocess

from config import CompressionConfig
from services import console_log as log

_SCRIPT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


class AudioCompressorService:
    def __init__(self, config: CompressionConfig, ffmpeg_path: str) -> None:
        self._config = config
        if os.path.isabs(ffmpeg_path):
            self._ffmpeg = ffmpeg_path
        else:
            self._ffmpeg = os.path.join(_SCRIPT_DIR, ffmpeg_path)

    def compress(self, wav_path: str) -> str | None:
        """Komprimiert eine WAV-Datei. Gibt den Ausgabepfad zurück oder None bei Fehler."""
        fmt = self._config.format.lower()

        ext_map = {"mp3": ".mp3", "aac": ".m4a", "opus": ".ogg"}
        ext = ext_map.get(fmt, f".{fmt}")

        codec_map = {"mp3": "libmp3lame", "aac": "aac", "opus": "libopus"}
        codec = codec_map.get(fmt, fmt)

        out_path = os.path.splitext(wav_path)[0] + ext

        args = [
            self._ffmpeg, "-y",
            "-i", wav_path,
            "-c:a", codec,
            "-b:a", f"{self._config.bitrate_kbps}k",
            out_path,
        ]

        try:
            result = subprocess.run(
                args,
                capture_output=True,
                text=True,
                timeout=30,
            )

            if result.returncode != 0:
                raise RuntimeError(f"ffmpeg Exit {result.returncode}: {result.stderr.strip()}")

            if not self._config.keep_wav_audio:
                try:
                    os.remove(wav_path)
                except OSError:
                    pass

            return out_path

        except Exception as ex:
            log.warning(
                f"[BBFon] Komprimierung fehlgeschlagen ({os.path.basename(wav_path)}): {ex}"
            )
            return None
