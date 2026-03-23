"""
BBFon Konfigurationsmodelle – Pydantic v2, PascalCase JSON-Keys.
"""

from __future__ import annotations

import json
import os
from typing import List

from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_pascal

THRESHOLD_SCALE = 1000.0


class _Base(BaseModel):
    model_config = ConfigDict(
        alias_generator=to_pascal,
        populate_by_name=True,
    )


class AnalysisConfig(_Base):
    enabled: bool = False
    window_seconds: int = 10
    min_trigger_count: int = 3


class TriggerConfig(_Base):
    threshold: float = 300.0
    cooldown_seconds: int = 60
    message: str = "Lärm erkannt!"
    is_recording: bool = False
    suppress_if_higher_fires: bool = False
    min_duration_seconds: int = 0
    analysis: AnalysisConfig = AnalysisConfig()


class StartupConfig(_Base):
    enabled: bool = False
    message: str = "ich wache"


class RecordingConfig(_Base):
    duration_seconds: int = 10
    max_files: int = 0
    max_age_days: int = 30
    send_attachments: bool = False


class CompressionConfig(_Base):
    enabled: bool = False
    format: str = "opus"
    bitrate_kbps: int = 24
    keep_wav_audio: bool = False


class CameraConfig(_Base):
    enabled: bool = False
    device_name: str = ""
    format: str = "mp4"
    mux_with_audio: bool = False
    keep_mux_audio: bool = False
    scale_width: int = 320
    extra_args: str = "-c:v libx264 -profile:v baseline -pix_fmt yuv420p -movflags +faststart"


class BatteryConfig(_Base):
    enabled: bool = False
    threshold_percent: int = 20
    check_interval_seconds: int = 60
    message: str = "Batterie niedrig"


class SignalConfig(_Base):
    cli_path: str = "signal-cli/bin/signal-cli"
    sender: str = ""
    recipient: str = ""


class TelegramConfig(_Base):
    bot_token: str = ""
    chat_id: str = ""


class WhatsAppConfig(_Base):
    cli_path: str = "mudslide"
    sender: str = ""
    recipient: str = ""


class AppConfig(_Base):
    provider: str = "Telegram"
    ffmpeg_path: str = "ffmpeg"
    audio_device: str = ""
    triggers: List[TriggerConfig] = []
    startup: StartupConfig = StartupConfig()
    recording: RecordingConfig = RecordingConfig()
    compression: CompressionConfig = CompressionConfig()
    camera: CameraConfig = CameraConfig()
    battery: BatteryConfig = BatteryConfig()
    signal: SignalConfig = SignalConfig()
    telegram: TelegramConfig = TelegramConfig()
    whatsapp: WhatsAppConfig = WhatsAppConfig()


def load_config(path: str) -> AppConfig:
    """Liest appsettings.json und gibt ein AppConfig-Objekt zurück."""
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)
    return AppConfig.model_validate(data)


def save_config(path: str, data: dict) -> None:
    """Liest appsettings.json, merged das dict und schreibt es zurück."""
    if os.path.exists(path):
        with open(path, "r", encoding="utf-8") as f:
            existing = json.load(f)
    else:
        existing = {}

    def deep_merge(base: dict, update: dict) -> dict:
        result = dict(base)
        for k, v in update.items():
            if isinstance(v, dict) and isinstance(result.get(k), dict):
                result[k] = deep_merge(result[k], v)
            else:
                result[k] = v
        return result

    merged = deep_merge(existing, data)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(merged, f, indent=2, ensure_ascii=False)
