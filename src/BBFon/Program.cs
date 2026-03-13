using BBFon;
using BBFon.Services;
using Microsoft.Extensions.Configuration;


if (args.Contains("--version") || args.Contains("-v"))
{
    var v = System.Diagnostics.FileVersionInfo.GetVersionInfo(Environment.ProcessPath!);
    Console.WriteLine($"BBFon {v.FileVersion}");
    return;
}

if (args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("""
        BBFon – Babyfon / Geräusch-Monitor

        Verwendung:
          bbfon [Optionen]

        Optionen:
          --provider <Signal|Telegram>   Benachrichtigungs-Provider setzen
          --link [Telefonnummer]         Signal-Gerät verknüpfen / Telegram-Token setzen
          --test                         Testnachricht senden und beenden
          --calibrate                    Hintergrundlärm messen, Threshold vorschlagen
          --list-video                   Verfügbare Kamera-Geräte auflisten
          --list-audio                   Verfügbare Audio-Eingabegeräte auflisten
          --debug, -d                    Debug-Modus (kein echtes Senden)
          --version, -v                  Programmversion anzeigen
          --help, -h                     Diese Hilfe anzeigen

        Beispiele:
          bbfon --provider Signal --link +4912345678
          bbfon --provider Telegram --link <BOT_TOKEN>
          bbfon --test
          bbfon --debug
        """);
    return;
}

bool debugMode         = args.Contains("--debug")        || args.Contains("-d");
bool linkMode          = args.Contains("--link");
var  linkArgIdx        = Array.IndexOf(args, "--link");
var  linkToken         = linkArgIdx >= 0 && linkArgIdx + 1 < args.Length && !args[linkArgIdx + 1].StartsWith('-')
                             ? args[linkArgIdx + 1] : null;
bool testMode          = args.Contains("--test");
bool calibrateMode     = args.Contains("--calibrate");
bool listCamerasMode   = args.Contains("--list-video");
bool listAudioMode     = args.Contains("--list-audio");
var  providerArgIdx    = Array.IndexOf(args, "--provider");
var  providerArg       = providerArgIdx >= 0 && providerArgIdx + 1 < args.Length
                             ? args[providerArgIdx + 1] : null;

if (providerArg != null)
{
    if (!providerArg.Equals("Signal", StringComparison.OrdinalIgnoreCase) &&
        !providerArg.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
    {
        ConsoleLog.Error($"[BBFon] Unbekannter Provider \"{providerArg}\". Erlaubt: Signal, Telegram");
        return;
    }
    SetProviderInAppSettings(providerArg);
    ConsoleLog.Success($"[BBFon] Provider auf \"{providerArg}\" gesetzt.");
    if (!linkMode && !testMode) return;
}

var configRoot = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var appConfig = configRoot.Get<AppConfig>()
    ?? throw new InvalidOperationException("appsettings.json konnte nicht geladen werden.");

if (appConfig.Provider.Equals("Signal", StringComparison.OrdinalIgnoreCase))
    CheckJavaVersion();

// --link: Verlinkung durchführen und beenden
if (linkMode)
{
    if (appConfig.Provider.Equals("Signal", StringComparison.OrdinalIgnoreCase))
    {
        await new LinkService(appConfig.Signal).RunAsync(linkToken);
        return;
    }

    if (appConfig.Provider.Equals("Telegram", StringComparison.OrdinalIgnoreCase))
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

static void CheckJavaVersion()
{
    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName               = "java",
            Arguments              = "-version",
            UseShellExecute        = false,
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
            CreateNoWindow         = true
        };
        using var p = System.Diagnostics.Process.Start(psi);
        if (p == null) { WarnJavaNotFound(); return; }

        // java -version schreibt auf stderr
        var output = p.StandardError.ReadToEnd();
        p.WaitForExit();

        // "java version \"1.8.0\"" oder "openjdk version \"21.0.1\""
        var match = System.Text.RegularExpressions.Regex.Match(output, @"version ""(\d+)(?:\.(\d+))?");
        if (!match.Success) { WarnJavaNotFound(); return; }

        int major = int.Parse(match.Groups[1].Value);
        // Java 8 und älter: major == 1, minor ist die echte Version
        if (major == 1 && match.Groups[2].Success)
            major = int.Parse(match.Groups[2].Value);

        if (major < 25)
        {
            ConsoleLog.Warning($"[BBFon] Java {major} erkannt – signal-cli benötigt mindestens Java 25 (empfohlen: Java 25).");
            ConsoleLog.Warning("[BBFon] Java 25 installieren: winget install EclipseAdoptium.Temurin.25.JDK");
        }
    }
    catch
    {
        WarnJavaNotFound();
    }
}

static void SetProviderInAppSettings(string provider)
{
    var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
    var json = File.ReadAllText(path);
    var root = System.Text.Json.Nodes.JsonNode.Parse(json)?.AsObject()
        ?? throw new InvalidOperationException("appsettings.json konnte nicht geparst werden.");

    root["Provider"] = provider;

    var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
    File.WriteAllText(path, root.ToJsonString(opts));
}

static void WarnJavaNotFound()
{
    ConsoleLog.Warning("[BBFon] Java nicht gefunden – signal-cli wird nicht funktionieren.");
    ConsoleLog.Warning("[BBFon] Java 21 installieren: https://adoptium.net/");
}

static void PrintSettings(AppConfig cfg)
{
    ConsoleLog.Info("[BBFon] --- Einstellungen ---");
    ConsoleLog.Info($"[BBFon]   Provider:       {cfg.Provider}");
    var audioDevice = string.IsNullOrWhiteSpace(cfg.AudioDevice) ? "Standard" : $"\"{cfg.AudioDevice}\"";
    ConsoleLog.Info($"[BBFon]   Audio-Eingang:  {audioDevice}");
    ConsoleLog.Info($"[BBFon]   Startnachricht: {(cfg.Startup.Enabled ? $"\"{cfg.Startup.Message}\"" : "inaktiv")}");
    ConsoleLog.Info($"[BBFon]   Triggers ({cfg.Triggers.Count}):");
    for (int i = 0; i < cfg.Triggers.Count; i++)
    {
        var t = cfg.Triggers[i];
        var analyse = t.Analysis.Enabled ? $"Analyse {t.Analysis.MinTriggerCount}x/{t.Analysis.WindowSeconds}s" : "direkt";
        var rec = t.IsRecording ? " | Aufnahme" : "";
        ConsoleLog.Info($"[BBFon]     T{i + 1}: {t.Threshold:F3} | Cooldown {t.CooldownSeconds}s | {analyse}{rec} → \"{t.Message}\"");
    }
    bool anyRec = cfg.Triggers.Any(t => t.IsRecording);
    var recParts = new List<string>();
    if (cfg.Recording.MaxFiles > 0 && anyRec)   recParts.Add($"max. {cfg.Recording.MaxFiles} Dateien");
    if (cfg.Recording.MaxAgeDays > 0 && anyRec) recParts.Add($"max. {cfg.Recording.MaxAgeDays} Tage");
    if (cfg.Recording.SendAttachments && anyRec) recParts.Add("Anhänge senden");
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
