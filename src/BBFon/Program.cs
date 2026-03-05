using BBFon;
using BBFon.Services;
using Microsoft.Extensions.Configuration;


bool debugMode         = args.Contains("--debug")        || args.Contains("-d");
bool linkMode          = args.Contains("--link");
var  linkArgIdx        = Array.IndexOf(args, "--link");
var  linkToken         = linkArgIdx >= 0 && linkArgIdx + 1 < args.Length && !args[linkArgIdx + 1].StartsWith('-')
                             ? args[linkArgIdx + 1] : null;
bool testMode          = args.Contains("--test");
bool calibrateMode     = args.Contains("--calibrate");
bool listCamerasMode   = args.Contains("--list-video");
bool listAudioMode     = args.Contains("--list-audio");

var configRoot = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var appConfig = configRoot.Get<AppConfig>()
    ?? throw new InvalidOperationException("appsettings.json konnte nicht geladen werden.");

// --link: Verlinkung durchführen und beenden
if (linkMode)
{
    if (appConfig.Provider.Equals("Telegram", StringComparison.OrdinalIgnoreCase) || linkToken != null)
    {
        var token = linkToken ?? appConfig.Telegram.BotToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            ConsoleLog.Error("[BBFon] Kein Bot-Token angegeben.");
            ConsoleLog.Error("[BBFon] Verwendung: bbfon --link <BOT_TOKEN>  oder  BotToken in appsettings.json setzen.");
            return;
        }
        await new TelegramLinkService().RunAsync(token);
        return;
    }

    if (appConfig.Provider.Equals("Signal", StringComparison.OrdinalIgnoreCase))
    {
        await new LinkService(appConfig.Signal).RunAsync();
        return;
    }

    ConsoleLog.Error($"[BBFon] --link ist nur für Provider \"Signal\" oder \"Telegram\" verfügbar (aktuell: \"{appConfig.Provider}\").");
    return;
}

// --calibrate: Hintergrundlärm messen und Threshold vorschlagen
if (calibrateMode)
{
    await new CalibrateService().RunAsync();
    return;
}

// --list-video: Verfügbare Kamera-Geräte auflisten
if (listCamerasMode)
{
    var camService = new CameraRecorderService(appConfig.Camera, appConfig.Recording, appConfig.FfmpegPath);
    var devices = await camService.ListDevicesAsync();
    if (devices.Count == 0)
    {
        ConsoleLog.Warning("[BBFon] Keine DirectShow-Videogeräte gefunden. Ist ffmpeg installiert?");
    }
    else
    {
        ConsoleLog.Info("[BBFon] Verfügbare Kamera-Geräte:");
        foreach (var d in devices)
            ConsoleLog.Info($"  - \"{d}\"");
        ConsoleLog.Info("[BBFon] Trage den gewünschten Namen als Camera.DeviceName in appsettings.json ein.");
    }
    return;
}

// --list-audio: Verfügbare Audio-Eingabegeräte auflisten
if (listAudioMode)
{
    var devices = AudioMonitorService.ListDevices();
    if (devices.Count == 0)
    {
        ConsoleLog.Warning("[BBFon] Keine Audio-Eingabegeräte gefunden.");
    }
    else
    {
        ConsoleLog.Info("[BBFon] Verfügbare Audio-Eingabegeräte:");
        for (int i = 0; i < devices.Count; i++)
            ConsoleLog.Info($"  [{i}] \"{devices[i]}\"");
        ConsoleLog.Info("[BBFon] Trage den gewünschten Namen als AudioDevice in appsettings.json ein.");
    }
    return;
}

// Config-Validierung
var errors = ConfigValidator.Validate(appConfig);
if (errors.Count > 0)
{
    ConsoleLog.Error("[BBFon] Konfigurationsfehler – bitte appsettings.json prüfen:");
    foreach (var err in errors)
        ConsoleLog.Error($"  ! {err}");
    return;
}

ConsoleLog.Info($"[BBFon] Starte... | Provider: {appConfig.Provider}{(debugMode ? " | DEBUG-Modus" : "")}");
PrintSettings(appConfig);

INotificationService baseNotification = debugMode
    ? new DebugNotificationService()
    : appConfig.Provider.ToLowerInvariant() switch
    {
        "signal"   => (INotificationService)new SignalNotificationService(appConfig.Signal),
        "telegram" => new TelegramNotificationService(appConfig.Telegram),
        _          => throw new InvalidOperationException($"Unbekannter Provider \"{appConfig.Provider}\".")
    };

// Im Normalbetrieb mit Retry + Netzwerk-Wartelogik wrappen
INotificationService notification = debugMode
    ? baseNotification
    : new RetryNotificationService(baseNotification);

// Startnachricht senden
if (appConfig.Startup.Enabled && !debugMode)
{
    try
    {
        await notification.SendAsync(appConfig.Startup.Message);
        ConsoleLog.Success($"[BBFon] Startnachricht gesendet: \"{appConfig.Startup.Message}\"");
    }
    catch (Exception ex)
    {
        ConsoleLog.Warning($"[BBFon] Startnachricht fehlgeschlagen: {ex.Message}");
    }
}

// --test: Testnachricht senden und beenden
if (testMode)
{
    ConsoleLog.Info("[BBFon] Sende Testnachricht...");
    try
    {
        await notification.SendAsync("BBFon Test – Konfiguration funktioniert!");
        ConsoleLog.Success("[BBFon] Testnachricht erfolgreich gesendet.");
    }
    catch (Exception ex)
    {
        ConsoleLog.Error($"[BBFon] Fehler: {ex.Message}");
    }
    return;
}

// Hot reload: bei Dateiänderung Config neu einlesen und Settings anzeigen
RegisterReloadCallback(configRoot, appConfig);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Beenden mit Q oder Escape
_ = Task.Run(() =>
{
    try
    {
        while (!cts.Token.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
                    cts.Cancel();
            }
            Thread.Sleep(50);
        }
    }
    catch { /* Stdin nicht verfügbar (z.B. Dienst-Modus) */ }
});

// Schlafmodus verhindern – System muss für Babyfon wach bleiben
using var sleepPrevention = new SleepPreventionService();

if (appConfig.Battery.Enabled)
{
    var batteryMonitor = new BatteryMonitorService(appConfig.Battery, notification, debugMode);
    _ = Task.Run(() => batteryMonitor.RunAsync(cts.Token));
}

CameraRecorderService? camera = (appConfig.Camera.Enabled)
    ? new CameraRecorderService(appConfig.Camera, appConfig.Recording, appConfig.FfmpegPath)
    : null;

using var monitor = new AudioMonitorService(appConfig, notification, debugMode, camera);
monitor.Start(cts.Token);

ConsoleLog.Info("\n[BBFon] Beendet.");

// --- Lokale Funktionen ---

static void PrintSettings(AppConfig cfg)
{
    ConsoleLog.Info("[BBFon] --- Einstellungen ---");
    ConsoleLog.Info($"[BBFon]   Provider:       {cfg.Provider}");
    ConsoleLog.Info($"[BBFon]   Schwellwert:    {cfg.Threshold:F3}");
    ConsoleLog.Info($"[BBFon]   Cooldown:       {cfg.CooldownSeconds}s");
    ConsoleLog.Info($"[BBFon]   Nachricht:      {cfg.Message}");
    var audioDevice = string.IsNullOrWhiteSpace(cfg.AudioDevice) ? "Standard" : $"\"{cfg.AudioDevice}\"";
    ConsoleLog.Info($"[BBFon]   Audio-Eingang:  {audioDevice}");
    ConsoleLog.Info($"[BBFon]   Startnachricht: {(cfg.Startup.Enabled ? $"\"{cfg.Startup.Message}\"" : "inaktiv")}");
    ConsoleLog.Info($"[BBFon]   Analyse:        {(cfg.Analysis.Enabled ? $"aktiv ({cfg.Analysis.MinTriggerCount}x in {cfg.Analysis.WindowSeconds}s)" : "inaktiv")}");
    var recParts = new List<string>();
    if (cfg.Recording.MaxFiles > 0 && cfg.Recording.Enabled)  recParts.Add($"max. {cfg.Recording.MaxFiles} Dateien");
    if (cfg.Recording.MaxAgeDays > 0 && cfg.Recording.Enabled) recParts.Add($"max. {cfg.Recording.MaxAgeDays} Tage");
    if (cfg.Recording.SendAttachments && cfg.Recording.Enabled) recParts.Add("Anhänge senden");
    ConsoleLog.Info($"[BBFon]   Aufnahme:       {(recParts.Count > 0 ? string.Join(", ", recParts) : "keine (reine Lautstärken-Analyse)")}");
    ConsoleLog.Info($"[BBFon]   Komprimierung:  {(cfg.Compression.Enabled ? $"aktiv ({cfg.Compression.Format.ToUpperInvariant()}, {cfg.Compression.BitrateKbps}kbps, WAV behalten: {cfg.Compression.KeepWavAudio})" : "inaktiv")}");
    var camDevice = string.IsNullOrWhiteSpace(cfg.Camera.DeviceName) ? "auto" : $"\"{cfg.Camera.DeviceName}\"";
    var camScale  = cfg.Camera.ScaleWidth > 0 ? $", {cfg.Camera.ScaleWidth}px" : "";
    var camMux    = cfg.Camera.MuxWithAudio ? $", mit Audio{(cfg.Camera.KeepMuxAudio ? " (WAV behalten)" : "")}" : "";
    ConsoleLog.Info($"[BBFon]   Kamera:         {(cfg.Camera.Enabled ? $"aktiv ({cfg.Recording.DurationSeconds}s, {cfg.Camera.Format.ToUpperInvariant()}{camScale}{camMux}, Gerät: {camDevice})" : "inaktiv")}");
    ConsoleLog.Info($"[BBFon]   Batterie:       {(cfg.Battery.Enabled ? $"aktiv (< {cfg.Battery.ThresholdPercent}%, alle {cfg.Battery.CheckIntervalSeconds}s)" : "inaktiv")}");
    ConsoleLog.Info("[BBFon] -------------------------");
}

static void RegisterReloadCallback(IConfigurationRoot root, AppConfig appConfig)
{
    root.GetReloadToken().RegisterChangeCallback(_ =>
    {
        root.Bind(appConfig);
        ConsoleLog.Warning("\n[BBFon] Konfiguration neu geladen.");
        PrintSettings(appConfig);
        RegisterReloadCallback(root, appConfig);
    }, null);
}
