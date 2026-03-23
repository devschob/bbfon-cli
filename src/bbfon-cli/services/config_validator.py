"""
Konfigurationsvalidierung – gleiche Prüfungen wie C# ConfigValidator.
"""

from __future__ import annotations

from config import AppConfig, THRESHOLD_SCALE


def validate(config: AppConfig) -> list[str]:
    """Validiert die Konfiguration und gibt eine Liste von Fehlern zurück."""
    errors: list[str] = []

    if len(config.triggers) == 0:
        errors.append("Mindestens ein Trigger muss in der Triggers-Liste konfiguriert sein")

    for i, t in enumerate(config.triggers):
        p = f"Triggers[{i}]"

        if t.threshold <= 0.0 or t.threshold > THRESHOLD_SCALE:
            errors.append(
                f"{p}.Threshold muss zwischen 0.001 und {THRESHOLD_SCALE:.0f} liegen "
                f"(aktuell: {t.threshold:.3f})"
            )

        if t.cooldown_seconds < 0:
            errors.append(f"{p}.CooldownSeconds darf nicht negativ sein")

        if not t.message or not t.message.strip():
            errors.append(f"{p}.Message darf nicht leer sein")

        if t.analysis.enabled:
            if t.analysis.window_seconds <= 0:
                errors.append(f"{p}.Analysis.WindowSeconds muss größer als 0 sein")
            if t.analysis.min_trigger_count <= 0:
                errors.append(f"{p}.Analysis.MinTriggerCount muss größer als 0 sein")

    provider = config.provider.lower()

    if provider == "telegram":
        if not config.telegram.bot_token or not config.telegram.bot_token.strip():
            errors.append("Telegram.BotToken ist leer")
        if not config.telegram.chat_id or not config.telegram.chat_id.strip():
            errors.append("Telegram.ChatId ist leer")

    elif provider == "signal":
        if not config.signal.sender or not config.signal.sender.strip():
            errors.append("Signal.Sender ist leer")
        if not config.signal.recipient or not config.signal.recipient.strip():
            errors.append("Signal.Recipient ist leer")
        if not config.signal.cli_path or not config.signal.cli_path.strip():
            errors.append("Signal.CliPath ist leer")

    elif provider == "whatsapp":
        if not config.whatsapp.cli_path or not config.whatsapp.cli_path.strip():
            errors.append("WhatsApp.CliPath ist leer")
        if not config.whatsapp.sender or not config.whatsapp.sender.strip():
            errors.append("WhatsApp.Sender ist leer")
        if not config.whatsapp.recipient or not config.whatsapp.recipient.strip():
            errors.append("WhatsApp.Recipient ist leer")

    else:
        errors.append(
            f'Unbekannter Provider "{config.provider}". Erlaubt: Signal, Telegram, WhatsApp'
        )

    if config.battery.enabled:
        if config.battery.threshold_percent <= 0 or config.battery.threshold_percent > 100:
            errors.append(
                f"Battery.ThresholdPercent muss zwischen 1 und 100 liegen "
                f"(aktuell: {config.battery.threshold_percent})"
            )
        if config.battery.check_interval_seconds <= 0:
            errors.append("Battery.CheckIntervalSeconds muss größer als 0 sein")

    if config.recording.max_files < 0:
        errors.append("Recording.MaxFiles darf nicht negativ sein")
    if config.recording.max_age_days < 0:
        errors.append("Recording.MaxAgeDays darf nicht negativ sein")

    return errors
