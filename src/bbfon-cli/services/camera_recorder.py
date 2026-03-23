"""
Kamera-Aufnahmedienst via ffmpeg (V4L2 auf Linux).
"""

from __future__ import annotations

import os
import subprocess

from config import CameraConfig, RecordingConfig
from services import console_log as log

_SCRIPT_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


class CameraRecorderService:
    def __init__(
        self,
        config: CameraConfig,
        recording_config: RecordingConfig,
        ffmpeg_path: str,
        output_dir: str,
    ) -> None:
        self._config = config
        self._recording_config = recording_config
        self._output_dir = output_dir
        if os.path.isabs(ffmpeg_path):
            self._ffmpeg = ffmpeg_path
        else:
            self._ffmpeg = os.path.join(_SCRIPT_DIR, ffmpeg_path)
        self._resolved_device: str | None = None

    def record(self, timestamp: str) -> str | None:
        """Nimmt Video auf. Gibt den Pfad der fertigen Datei zurück (None bei Fehler)."""
        device = self._resolve_device()
        if device is None:
            return None

        make_gif = self._config.format.lower() == "gif"
        video_ext = "mp4" if make_gif else self._config.format.lstrip(".").lower()
        video_name = f"{timestamp}_cam_tmp.mp4" if make_gif else f"{timestamp}_cam.{video_ext}"
        video_path = os.path.join(self._output_dir, video_name)

        scale_filter = ""
        if not make_gif and self._config.scale_width > 0:
            scale_filter = f"-vf scale={self._config.scale_width}:-1"

        extra_args = self._config.extra_args.strip()

        args = [self._ffmpeg, "-f", "v4l2", "-i", device]
        if scale_filter:
            args += scale_filter.split()
        if extra_args:
            args += extra_args.split()
        args += ["-t", str(self._recording_config.duration_seconds), "-y", video_path]

        ok = self._run_ffmpeg(args, timeout=self._recording_config.duration_seconds + 15, label="Kamera-Aufnahme")
        if not ok:
            return None

        if not make_gif:
            return video_path

        # MP4 → GIF
        gif_path = os.path.join(self._output_dir, f"{timestamp}_cam.gif")
        gif_scale = self._config.scale_width if self._config.scale_width > 0 else 320
        gif_args = [
            self._ffmpeg, "-i", video_path,
            "-vf", f"fps=10,scale={gif_scale}:-1:flags=lanczos",
            "-y", gif_path,
        ]
        gif_ok = self._run_ffmpeg(gif_args, timeout=60, label="GIF-Konvertierung")
        try:
            os.remove(video_path)
        except OSError:
            pass
        return gif_path if gif_ok else None

    def mux_audio(self, video_path: str, audio_path: str) -> str | None:
        """Bettet WAV-Audio in eine Videodatei ein."""
        if self._config.format.lower() == "gif":
            log.warning("[BBFon] Muxing übersprungen: GIF unterstützt kein Audio.")
            return None

        if not os.path.exists(video_path) or not os.path.exists(audio_path):
            return None

        ext = os.path.splitext(video_path)[1]
        temp_path = video_path + ".mux_tmp" + ext

        try:
            os.rename(video_path, temp_path)
        except OSError as ex:
            log.warning(f"[BBFon] Muxing vorbereitung fehlgeschlagen: {ex}")
            return None

        args = [
            self._ffmpeg,
            "-i", temp_path,
            "-i", audio_path,
            "-c:v", "copy",
            "-c:a", "aac",
            "-shortest",
            "-y", video_path,
        ]
        ok = self._run_ffmpeg(args, timeout=60, label="Audio-Muxing")

        try:
            os.remove(temp_path)
        except OSError:
            pass

        if not ok:
            return None

        if not self._config.keep_mux_audio:
            try:
                os.remove(audio_path)
            except OSError:
                pass

        return video_path

    def list_devices(self) -> list[str]:
        """Listet verfügbare V4L2-Videogeräte auf."""
        return self._query_devices()

    def _resolve_device(self) -> str | None:
        if self._config.device_name:
            return self._config.device_name

        if self._resolved_device is not None:
            return self._resolved_device

        devices = self._query_devices()
        if not devices:
            log.warning("[BBFon] Kamera: Kein V4L2-Videogerät gefunden.")
            return None

        self._resolved_device = devices[0]
        log.info(f'[BBFon] Kamera: Gerät automatisch erkannt: "{self._resolved_device}"')
        return self._resolved_device

    def _query_devices(self) -> list[str]:
        """Listet V4L2-Geräte via v4l2-ctl auf."""
        devices: list[str] = []

        # Methode 1: v4l2-ctl --list-devices
        try:
            result = subprocess.run(
                ["v4l2-ctl", "--list-devices"],
                capture_output=True,
                text=True,
                timeout=10,
            )
            if result.returncode == 0:
                current_name: str | None = None
                for line in result.stdout.splitlines():
                    if line and not line.startswith("\t"):
                        current_name = line.split("(")[0].strip()
                    elif line.startswith("\t/dev/video"):
                        dev_path = line.strip()
                        if current_name and dev_path not in devices:
                            devices.append(dev_path)
                if devices:
                    return devices
        except (FileNotFoundError, subprocess.TimeoutExpired):
            pass

        # Methode 2: /dev/video* direkt suchen
        import glob
        video_devs = sorted(glob.glob("/dev/video*"))
        for dev in video_devs:
            # Nur capture-Geräte (nicht metadata, etc.)
            try:
                result = subprocess.run(
                    ["v4l2-ctl", "-d", dev, "--info"],
                    capture_output=True,
                    text=True,
                    timeout=5,
                )
                if "Video Capture" in result.stdout:
                    devices.append(dev)
            except Exception:
                if os.path.exists(dev):
                    devices.append(dev)

        return devices

    def _run_ffmpeg(self, args: list[str], timeout: int, label: str) -> bool:
        try:
            result = subprocess.run(
                args,
                capture_output=True,
                text=True,
                timeout=timeout,
            )
            if result.returncode != 0:
                raise RuntimeError(f"Exit {result.returncode}: {result.stderr.strip()}")
            return True
        except Exception as ex:
            log.warning(f"[BBFon] {label} fehlgeschlagen: {ex}")
            return False
