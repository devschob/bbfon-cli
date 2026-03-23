"""
Audio-Überwachungsdienst – Kernkomponente von BBFon.
"""

from __future__ import annotations

import asyncio
import glob
import os
import threading
import time
import wave
from dataclasses import dataclass, field
from datetime import datetime
from typing import TYPE_CHECKING

import numpy as np
import sounddevice as sd

from config import AppConfig, THRESHOLD_SCALE
from services import console_log as log

if TYPE_CHECKING:
    from services.camera_recorder import CameraRecorderService
    from services.notification.interface import NotificationService

_SAMPLE_RATE = 16000
_BLOCKSIZE = 1600   # 100ms


# ANSI-Farben für Konsolenzeile
_RED    = "\033[31m"
_YELLOW = "\033[33m"
_GRAY   = "\033[90m"
_RESET  = "\033[0m"


@dataclass
class TriggerState:
    timestamps: list[float] = field(default_factory=list)
    last_sent: float = 0.0
    above_threshold_since: float | None = None

    # Aufnahme
    is_recording: bool = False
    recording_buffers: list[np.ndarray] = field(default_factory=list)
    recording_start: float = 0.0
    recording_path: str | None = None


class AudioMonitorService:
    def __init__(
        self,
        config: AppConfig,
        notification: "NotificationService",
        debug_mode: bool,
        camera: "CameraRecorderService | None" = None,
        output_dir: str = "",
    ) -> None:
        self._config = config
        self._notification = notification
        self._debug_mode = debug_mode
        self._camera = camera
        # Standardmäßig neben bbfon.py (eine Ebene über services/)
        self._output_dir = output_dir or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        self._stopping = False
        self._stream: sd.InputStream | None = None
        self._trigger_states: list[TriggerState] = [
            TriggerState() for _ in config.triggers
        ]
        # Globaler Aufnahme-Status (nur eine gleichzeitig)
        self._recording_buffers: list[np.ndarray] = []
        self._recording_path: str | None = None
        self._recording_stop_at: float = 0.0
        self._is_recording: bool = False
        self._camera_thread: threading.Thread | None = None
        self._camera_result: str | None = None

        # Hot-Reload: mtime der appsettings.json beobachten
        self._config_path: str | None = None
        self._config_mtime: float = 0.0

    # ------------------------------------------------------------------
    # Öffentliche Methoden
    # ------------------------------------------------------------------

    def start(self, stop_event: threading.Event, config_path: str | None = None) -> None:
        """Startet die Überwachung und blockiert bis stop_event gesetzt wird."""
        self._config_path = config_path
        if config_path and os.path.exists(config_path):
            self._config_mtime = os.path.getmtime(config_path)

        device_index = self._find_device()
        device_label = (
            "Standard-Mikrofon"
            if not self._config.audio_device
            else f'"{self._config.audio_device}"'
        )

        log.info(f"[BBFon] Überwache {device_label}...")
        self._print_trigger_info()
        if self._debug_mode:
            log.debug("[BBFon] DEBUG-Modus: Nachrichten werden NICHT gesendet.")
        log.info("[BBFon] Beenden: Strg+C, Q oder Esc\n")

        # Hot-Reload-Thread
        if config_path:
            reload_thread = threading.Thread(
                target=self._hot_reload_loop, args=(stop_event,), daemon=True
            )
            reload_thread.start()

        while not stop_event.is_set():
            try:
                with sd.InputStream(
                    device=device_index,
                    samplerate=_SAMPLE_RATE,
                    channels=1,
                    dtype="int16",
                    blocksize=_BLOCKSIZE,
                    callback=self._audio_callback,
                ):
                    stop_event.wait()
            except Exception as ex:
                if self._stopping or stop_event.is_set():
                    break
                log.warning(f"\n[BBFon] Mikrofon getrennt: {ex}")
                attempt = 0
                while not stop_event.is_set():
                    attempt += 1
                    stop_event.wait(timeout=3)
                    if stop_event.is_set():
                        break
                    log.info(f"[BBFon] Mikrofon-Reconnect, Versuch {attempt}...")
                    try:
                        device_index = self._find_device()
                        log.success("[BBFon] Mikrofon wiederverbunden.")
                        break
                    except Exception as ex2:
                        log.warning(
                            f"[BBFon] Mikrofon nicht verfügbar ({ex2}), nächster Versuch in 3s..."
                        )

        self._stopping = True

    @staticmethod
    def list_devices() -> list[str]:
        """Gibt eine Liste aller Audio-Eingabegeräte zurück."""
        devices = sd.query_devices()
        result = []
        for d in devices:
            if d["max_input_channels"] > 0:
                result.append(d["name"])
        return result

    # ------------------------------------------------------------------
    # Interne Methoden
    # ------------------------------------------------------------------

    def _find_device(self) -> int | None:
        """Findet ein Gerät per Namenssubstring oder gibt None (Standard) zurück."""
        if not self._config.audio_device:
            return None
        devices = sd.query_devices()
        for i, d in enumerate(devices):
            if (
                d["max_input_channels"] > 0
                and self._config.audio_device.lower() in d["name"].lower()
            ):
                return i
        log.warning(
            f'[BBFon] Audio-Gerät "{self._config.audio_device}" nicht gefunden – '
            "verwende Standardgerät."
        )
        return None

    def _print_trigger_info(self) -> None:
        for i, t in enumerate(self._config.triggers):
            parts = []
            if t.analysis.enabled:
                parts.append(
                    f"Analyse: mind. {t.analysis.min_trigger_count}x in {t.analysis.window_seconds}s"
                )
            if t.min_duration_seconds > 0:
                parts.append(f"min. {t.min_duration_seconds}s Dauer")
            analyse_info = ", ".join(parts) if parts else "direkt"
            rec_info = ", Aufnahme" if t.is_recording else ""
            log.info(
                f'[BBFon]   T{i + 1}: Schwellwert {t.threshold:.3f}, '
                f'Cooldown {t.cooldown_seconds}s, {analyse_info}{rec_info} → "{t.message}"'
            )

        any_rec = any(t.is_recording for t in self._config.triggers)
        if any_rec:
            limits = []
            if self._config.recording.max_files > 0:
                limits.append(f"max. {self._config.recording.max_files} Dateien")
            if self._config.recording.max_age_days > 0:
                limits.append(f"max. {self._config.recording.max_age_days} Tage")
            limit_info = f", Bereinigung: {', '.join(limits)}" if limits else ""
            log.info(
                f"[BBFon] Audio-Aufnahme bei Alarm: aktiv "
                f"(max. {self._config.recording.duration_seconds}s, WAV{limit_info})"
            )

        if self._camera is not None:
            fmt = self._config.camera.format.upper()
            mux = (
                ", mit Audio"
                if self._config.camera.mux_with_audio and any_rec and self._config.camera.enabled
                else ""
            )
            log.info(
                f"[BBFon] Kamera-Aufnahme bei Alarm: aktiv "
                f"({self._config.recording.duration_seconds}s, {fmt}{mux})"
            )

    def _audio_callback(
        self, indata: np.ndarray, frames: int, time_info, status
    ) -> None:
        """Sounddevice-Callback – wird aus einem Hintergrundthread aufgerufen."""
        now = time.time()

        # RMS berechnen (int16 → float, * THRESHOLD_SCALE)
        samples_float = indata[:, 0].astype(np.float32) / 32768.0
        rms = float(np.sqrt(np.mean(samples_float ** 2))) * THRESHOLD_SCALE

        # Hot-Reload: Trigger-Zustände anpassen wenn Anzahl geändert
        n = len(self._config.triggers)
        while len(self._trigger_states) < n:
            self._trigger_states.append(TriggerState())
        if len(self._trigger_states) > n:
            self._trigger_states = self._trigger_states[:n]

        # Laufende globale Aufnahme bedienen
        if self._is_recording:
            if now <= self._recording_stop_at:
                self._recording_buffers.append(indata[:, 0].copy())
            else:
                self._stop_recording(now)

        # Trigger-Analyse
        above_threshold = [False] * n
        trigger_count   = [0] * n
        duration_secs   = [0.0] * n
        analysis_ok     = [False] * n
        cooldown_ok     = [False] * n

        for i in range(n):
            trigger = self._config.triggers[i]
            state   = self._trigger_states[i]
            thr     = trigger.threshold  # bereits auf THRESHOLD_SCALE Basis

            above_threshold[i] = rms >= thr

            # Kontinuierliche Dauer tracken
            if above_threshold[i]:
                if state.above_threshold_since is None:
                    state.above_threshold_since = now
            else:
                state.above_threshold_since = None

            if state.above_threshold_since is not None:
                duration_secs[i] = now - state.above_threshold_since

            if trigger.analysis.enabled:
                if above_threshold[i]:
                    state.timestamps.append(now)
                # Timestamps außerhalb des Fensters entfernen
                cutoff = now - trigger.analysis.window_seconds
                state.timestamps = [t for t in state.timestamps if t > cutoff]
                trigger_count[i] = len(state.timestamps)

            count_ok    = (not trigger.analysis.enabled) or trigger_count[i] >= trigger.analysis.min_trigger_count
            duration_ok = trigger.min_duration_seconds <= 0 or duration_secs[i] >= trigger.min_duration_seconds
            analysis_ok[i]  = count_ok and duration_ok
            cooldown_ok[i]  = (now - state.last_sent) >= trigger.cooldown_seconds

        # Rollende Konsolenzeile
        any_alarm_ok = any(analysis_ok)
        any_above    = any(above_threshold)
        line_color = _RED if any_alarm_ok else (_YELLOW if any_above else _GRAY)

        ts = datetime.fromtimestamp(now).strftime("%H:%M:%S")
        line = f"\r[{ts}] Lautstärke: {rms:7.3f}"
        for i in range(n):
            trigger = self._config.triggers[i]
            state   = self._trigger_states[i]
            if trigger.analysis.enabled:
                line += f"  T{i + 1}:{trigger_count[i]}/{trigger.analysis.min_trigger_count}"
            elif above_threshold[i]:
                line += f"  T{i + 1}:[!]"
            if trigger.min_duration_seconds > 0 and above_threshold[i]:
                line += f"({duration_secs[i]:.1f}s/{trigger.min_duration_seconds}s)"
            if self._debug_mode and analysis_ok[i] and not cooldown_ok[i]:
                cd_remaining = trigger.cooldown_seconds - (now - state.last_sent)
                line += f"(cd:{cd_remaining:.0f}s)"
        line += "   "

        log.inline(f"{line_color}{line}{_RESET}")

        # Alarm auslösen
        alarm_timestamp: str | None = None
        for i in range(n):
            if not analysis_ok[i] or not cooldown_ok[i]:
                continue

            trigger = self._config.triggers[i]

            # Unterdrücken wenn Trigger mit höherem Threshold ebenfalls feuert
            if trigger.suppress_if_higher_fires:
                higher_fires = any(
                    j != i
                    and analysis_ok[j]
                    and cooldown_ok[j]
                    and self._config.triggers[j].threshold > trigger.threshold
                    for j in range(n)
                )
                if higher_fires:
                    continue

            state = self._trigger_states[i]
            state.last_sent = now
            state.timestamps.clear()

            ts2 = datetime.fromtimestamp(now).strftime("%H:%M:%S")
            log.alarm(
                f"\n[{ts2}] ALARM T{i + 1}! "
                f"Lautstärke {rms:.3f} >= {trigger.threshold:.3f}. Sende Nachricht..."
            )

            if trigger.is_recording:
                if alarm_timestamp is None:
                    alarm_timestamp = datetime.fromtimestamp(now).strftime("%Y-%m-%d_%H-%M-%S")

                # Kamera starten (nur eine gleichzeitig)
                if self._camera is not None and (
                    self._camera_thread is None or not self._camera_thread.is_alive()
                ):
                    ts_cam = alarm_timestamp
                    self._camera_result = None
                    cam_thread = threading.Thread(
                        target=self._run_camera, args=(ts_cam,), daemon=True
                    )
                    cam_thread.start()
                    self._camera_thread = cam_thread

                # Audio-Aufnahme starten (nur eine gleichzeitig)
                if not self._is_recording:
                    self._start_recording(indata[:, 0].copy(), alarm_timestamp)

            # Notification in eigenem Thread mit asyncio.run
            msg = trigger.message
            cooldown_secs = trigger.cooldown_seconds
            notif_thread = threading.Thread(
                target=self._send_notification_sync,
                args=(msg, cooldown_secs),
                daemon=True,
            )
            notif_thread.start()

    def _start_recording(self, first_buffer: np.ndarray, timestamp: str) -> None:
        filename = f"{timestamp}.wav"
        path = os.path.join(self._output_dir, filename)
        self._recording_path = path
        self._recording_buffers = [first_buffer]
        self._recording_stop_at = time.time() + self._config.recording.duration_seconds
        self._is_recording = True
        ts = datetime.now().strftime("%H:%M:%S")
        log.info(f"\n[{ts}] Audio-Aufnahme gestartet: {filename}")

    def _stop_recording(self, now: float) -> None:
        self._is_recording = False
        buffers = self._recording_buffers.copy()
        path = self._recording_path
        self._recording_buffers = []
        self._recording_path = None

        ts = datetime.now().strftime("%H:%M:%S")
        log.info(
            f"\n[{ts}] Audio-Aufnahme beendet ({self._config.recording.duration_seconds}s)."
        )

        # Post-Processing in eigenem Thread
        cam_thread = self._camera_thread
        post_thread = threading.Thread(
            target=self._post_process_recording,
            args=(buffers, path, cam_thread),
            daemon=True,
        )
        post_thread.start()

    def _post_process_recording(
        self,
        buffers: list[np.ndarray],
        wav_path: str | None,
        cam_thread: threading.Thread | None,
    ) -> None:
        # WAV schreiben
        if wav_path and buffers:
            self._write_wav(buffers, wav_path)

        # Kamera-Aufnahme abwarten
        video_path: str | None = None
        if cam_thread is not None:
            cam_thread.join(timeout=self._config.recording.duration_seconds + 30)
            video_path = self._camera_result
            if video_path:
                ts = datetime.now().strftime("%H:%M:%S")
                log.success(f"[{ts}] Kamera-Aufnahme: {os.path.basename(video_path)}")

        # Muxing: WAV in Video einbetten
        if (
            self._camera is not None
            and video_path is not None
            and self._config.camera.mux_with_audio
            and wav_path is not None
            and os.path.exists(wav_path)
        ):
            muxed = self._camera.mux_audio(video_path, wav_path)
            if muxed:
                log.success(f"[BBFon] Audio eingebettet: {os.path.basename(muxed)}")

        # Komprimierung
        final_audio_path = wav_path
        if self._config.compression.enabled and wav_path and os.path.exists(wav_path):
            from services.audio_compressor import AudioCompressorService
            compressor = AudioCompressorService(
                self._config.compression, self._config.ffmpeg_path
            )
            result = compressor.compress(wav_path)
            if result:
                log.success(f"[BBFon] Komprimiert: {os.path.basename(result)}")
                final_audio_path = result

        # Anhänge senden
        attachments: list[str] = []
        if final_audio_path and os.path.exists(final_audio_path):
            attachments.append(final_audio_path)
        if video_path and os.path.exists(video_path):
            attachments.append(video_path)

        if self._config.recording.send_attachments and attachments:
            asyncio.run(self._send_attachments(attachments))

        self._cleanup_recordings()

    def _write_wav(self, buffers: list[np.ndarray], path: str) -> None:
        try:
            audio = np.concatenate(buffers, axis=0).astype(np.int16)
            with wave.open(path, "wb") as wf:
                wf.setnchannels(1)
                wf.setsampwidth(2)  # 16-bit
                wf.setframerate(_SAMPLE_RATE)
                wf.writeframes(audio.tobytes())
        except Exception as ex:
            log.error(f"[BBFon] WAV schreiben fehlgeschlagen: {ex}")

    def _run_camera(self, timestamp: str) -> None:
        try:
            result = self._camera.record(timestamp)
            self._camera_result = result
        except Exception as ex:
            log.warning(f"[BBFon] Kamera-Fehler: {ex}")
            self._camera_result = None

    def _send_notification_sync(self, message: str, cooldown_seconds: int) -> None:
        try:
            asyncio.run(self._notification.send(message))
            ts = datetime.now().strftime("%H:%M:%S")
            log.success(f"[{ts}] Nachricht gesendet. Cooldown: {cooldown_seconds}s")
        except Exception as ex:
            ts = datetime.now().strftime("%H:%M:%S")
            log.error(f"[{ts}] Fehler beim Senden: {ex}")

    async def _send_attachments(self, attachments: list[str]) -> None:
        try:
            await self._notification.send("", attachments)
            log.success(f"[BBFon] {len(attachments)} Anhang/Anhänge gesendet.")
        except Exception as ex:
            log.error(f"[BBFon] Anhänge senden fehlgeschlagen: {ex}")

    def _cleanup_recordings(self) -> None:
        cfg = self._config.recording
        if cfg.max_files <= 0 and cfg.max_age_days <= 0:
            return

        patterns = [
            "????-??-??_??-??-??.wav",
            "????-??-??_??-??-??.mp3",
            "????-??-??_??-??-??.ogg",
            "????-??-??_??-??-??.m4a",
            "????-??-??_??-??-??_cam.mp4",
            "????-??-??_??-??-??_cam.avi",
            "????-??-??_??-??-??_cam.mkv",
            "????-??-??_??-??-??_cam.gif",
        ]

        files = []
        for pattern in patterns:
            full_pattern = os.path.join(self._output_dir, pattern)
            files.extend(glob.glob(full_pattern))

        # Sortieren nach Erstellungszeit (neuste zuerst)
        files.sort(key=lambda f: os.path.getctime(f), reverse=True)

        to_delete: set[str] = set()

        if cfg.max_age_days > 0:
            cutoff = time.time() - cfg.max_age_days * 86400
            for f in files:
                if os.path.getctime(f) < cutoff:
                    to_delete.add(f)

        if cfg.max_files > 0:
            for f in files[cfg.max_files:]:
                to_delete.add(f)

        for path in to_delete:
            try:
                os.remove(path)
                log.info(f"[BBFon] Aufnahme gelöscht: {os.path.basename(path)}")
            except Exception as ex:
                log.warning(
                    f"[BBFon] Löschen fehlgeschlagen ({os.path.basename(path)}): {ex}"
                )

    def _hot_reload_loop(self, stop_event: threading.Event) -> None:
        """Beobachtet appsettings.json und lädt bei Änderung neu."""
        from config import load_config

        while not stop_event.is_set():
            stop_event.wait(timeout=2)
            if stop_event.is_set() or not self._config_path:
                break
            try:
                mtime = os.path.getmtime(self._config_path)
                if mtime != self._config_mtime:
                    self._config_mtime = mtime
                    new_cfg = load_config(self._config_path)
                    # Felder des Config-Objekts in-place aktualisieren
                    for field_name in AppConfig.model_fields:
                        setattr(self._config, field_name, getattr(new_cfg, field_name))
                    log.warning("\n[BBFon] Konfiguration neu geladen.")
            except Exception as ex:
                log.warning(f"[BBFon] Hot-Reload fehlgeschlagen: {ex}")
