namespace BBFon;

public class AppConfig
{
    public float Threshold { get; set; } = 0.3f;
    public int CooldownSeconds { get; set; } = 60;
    public string Message { get; set; } = "Lärm erkannt!";
    public string Provider { get; set; } = "Telegram";
    public StartupConfig Startup { get; set; } = new();
    public AnalysisConfig Analysis { get; set; } = new();
    public RecordingConfig Recording { get; set; } = new();
    public CompressionConfig Compression { get; set; } = new();
    public CameraConfig Camera { get; set; } = new();
    public BatteryConfig Battery { get; set; } = new();
    public SignalConfig Signal { get; set; } = new();
    public TelegramConfig Telegram { get; set; } = new();
}

public class StartupConfig
{
    public bool Enabled { get; set; } = false;
    public string Message { get; set; } = "ich wache";
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
    public bool SendAttachments { get; set; } = false;
}

public class CompressionConfig
{
    public bool Enabled { get; set; } = false;
    public string FfmpegPath { get; set; } = "ffmpeg.exe";
    public string Format { get; set; } = "opus";   // opus, mp3, aac
    public int BitrateKbps { get; set; } = 24;
    public bool KeepWavAudio { get; set; } = false;
}

public class CameraConfig
{
    public bool Enabled { get; set; } = false;
    public string FfmpegPath { get; set; } = "ffmpeg.exe";
    public string DeviceName { get; set; } = "";   // leer = erstes Gerät automatisch erkennen
    public int DurationSeconds { get; set; } = 10;
    public string Format { get; set; } = "mp4";    // mp4, avi, mkv, gif
    public bool MuxWithAudio { get; set; } = false;       // WAV-Aufnahme in Video einbetten
    public bool KeepMuxAudio { get; set; } = false;  // WAV nach erfolgreichem Muxing löschen
    public int ScaleWidth { get; set; } = 320;             // Videobreite in Pixel (0 = keine Skalierung)
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
