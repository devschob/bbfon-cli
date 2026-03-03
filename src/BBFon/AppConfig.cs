namespace BBFon;

public class AppConfig
{
    public float Threshold { get; set; } = 0.3f;
    public int CooldownSeconds { get; set; } = 60;
    public string Message { get; set; } = "Lärm erkannt!";
    public string Provider { get; set; } = "Telegram";
    public AnalysisConfig Analysis { get; set; } = new();
    public RecordingConfig Recording { get; set; } = new();
    public CompressionConfig Compression { get; set; } = new();
    public BatteryConfig Battery { get; set; } = new();
    public SignalConfig Signal { get; set; } = new();
    public TelegramConfig Telegram { get; set; } = new();
}

public class AnalysisConfig
{
    public bool Enabled { get; set; } = false;
    public int WindowSeconds { get; set; } = 10;
    public int MinTriggerCount { get; set; } = 3;
}

public class RecordingConfig
{
    public int MaxFiles { get; set; } = 0;    // 0 = unbegrenzt
    public int MaxAgeDays { get; set; } = 0;  // 0 = unbegrenzt
}

public class CompressionConfig
{
    public bool Enabled { get; set; } = false;
    public string FfmpegPath { get; set; } = "ffmpeg.exe";
    public string Format { get; set; } = "opus";   // opus, mp3, aac
    public int BitrateKbps { get; set; } = 24;
    public bool DeleteWavAfterCompress { get; set; } = true;
}

public class BatteryConfig
{
    public bool Enabled { get; set; } = false;
    public int ThresholdPercent { get; set; } = 20;
    public int CheckIntervalSeconds { get; set; } = 60;
    public string Message { get; set; } = "Batterie niedrig";
}

public class SignalConfig
{
    public string CliPath { get; set; } = "signal-cli.bat";
    public string Sender { get; set; } = "";
    public string Recipient { get; set; } = "";
}

public class TelegramConfig
{
    public string BotToken { get; set; } = "";
    public string ChatId { get; set; } = "";
}
