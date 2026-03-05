namespace BBFon.Services;

public static class ConfigValidator
{
    public static List<string> Validate(AppConfig cfg)
    {
        var errors = new List<string>();

        if (cfg.Threshold <= 0f || cfg.Threshold > AppConfig.ThresholdScale)
            errors.Add($"Threshold muss zwischen 0.001 und {AppConfig.ThresholdScale} liegen (aktuell: {cfg.Threshold:F3})");

        if (cfg.CooldownSeconds < 0)
            errors.Add("CooldownSeconds darf nicht negativ sein");

        if (string.IsNullOrWhiteSpace(cfg.Message))
            errors.Add("Message darf nicht leer sein");

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

            default:
                errors.Add($"Unbekannter Provider \"{cfg.Provider}\". Erlaubt: Signal, Telegram");
                break;
        }

        if (cfg.Analysis.Enabled)
        {
            if (cfg.Analysis.WindowSeconds <= 0)
                errors.Add("Analysis.WindowSeconds muss größer als 0 sein");
            if (cfg.Analysis.MinTriggerCount <= 0)
                errors.Add("Analysis.MinTriggerCount muss größer als 0 sein");
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
