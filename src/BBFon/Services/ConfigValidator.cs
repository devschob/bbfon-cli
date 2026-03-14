namespace BBFon.Services;

public static class ConfigValidator
{
    public static List<string> Validate(AppConfig cfg)
    {
        var errors = new List<string>();

        if (cfg.Triggers.Count == 0)
            errors.Add("Mindestens ein Trigger muss in der Triggers-Liste konfiguriert sein");

        for (int i = 0; i < cfg.Triggers.Count; i++)
        {
            var t = cfg.Triggers[i];
            var p = $"Triggers[{i}]";

            if (t.Threshold <= 0f || t.Threshold > AppConfig.ThresholdScale)
                errors.Add($"{p}.Threshold muss zwischen 0.001 und {AppConfig.ThresholdScale} liegen (aktuell: {t.Threshold:F3})");

            if (t.CooldownSeconds < 0)
                errors.Add($"{p}.CooldownSeconds darf nicht negativ sein");

            if (string.IsNullOrWhiteSpace(t.Message))
                errors.Add($"{p}.Message darf nicht leer sein");

            if (t.Analysis.Enabled)
            {
                if (t.Analysis.WindowSeconds <= 0)
                    errors.Add($"{p}.Analysis.WindowSeconds muss größer als 0 sein");
                if (t.Analysis.MinTriggerCount <= 0)
                    errors.Add($"{p}.Analysis.MinTriggerCount muss größer als 0 sein");
            }
        }

        switch (cfg.Provider.ToLowerInvariant())
        {
            case "telegram":
                if (string.IsNullOrWhiteSpace(cfg.Telegram.BotToken))
                    errors.Add("Telegram.BotToken ist leer");
                if (string.IsNullOrWhiteSpace(cfg.Telegram.ChatId))
                    errors.Add("Telegram.ChatId ist leer");
                break;

            case "signal":
                if (string.IsNullOrWhiteSpace(cfg.Signal.Sender))
                    errors.Add("Signal.Sender ist leer");
                if (string.IsNullOrWhiteSpace(cfg.Signal.Recipient))
                    errors.Add("Signal.Recipient ist leer");
                if (string.IsNullOrWhiteSpace(cfg.Signal.CliPath))
                    errors.Add("Signal.CliPath ist leer");
                break;

            case "whatsapp":
                if (string.IsNullOrWhiteSpace(cfg.WhatsApp.CliPath))
                    errors.Add("WhatsApp.CliPath ist leer");
                if (string.IsNullOrWhiteSpace(cfg.WhatsApp.Sender))
                    errors.Add("WhatsApp.Sender ist leer");
                if (string.IsNullOrWhiteSpace(cfg.WhatsApp.Recipient))
                    errors.Add("WhatsApp.Recipient ist leer");
                break;

            default:
                errors.Add($"Unbekannter Provider \"{cfg.Provider}\". Erlaubt: Signal, Telegram, WhatsApp");
                break;
        }

        if (cfg.Battery.Enabled)
        {
            if (cfg.Battery.ThresholdPercent <= 0 || cfg.Battery.ThresholdPercent > 100)
                errors.Add($"Battery.ThresholdPercent muss zwischen 1 und 100 liegen (aktuell: {cfg.Battery.ThresholdPercent})");
            if (cfg.Battery.CheckIntervalSeconds <= 0)
                errors.Add("Battery.CheckIntervalSeconds muss größer als 0 sein");
        }

        if (cfg.Recording.MaxFiles < 0)
            errors.Add("Recording.MaxFiles darf nicht negativ sein");
        if (cfg.Recording.MaxAgeDays < 0)
            errors.Add("Recording.MaxAgeDays darf nicht negativ sein");

        return errors;
    }
}
