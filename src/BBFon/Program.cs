using BBFon;
using BBFon.Services;
using Microsoft.Extensions.Configuration;

bool recordingEnabled = args.Contains("--record")    || args.Contains("-r");
bool debugMode        = args.Contains("--debug")     || args.Contains("-d");
bool linkMode         = args.Contains("--link");
bool testMode         = args.Contains("--test");
bool calibrateMode    = args.Contains("--calibrate");

var configRoot = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var appConfig = configRoot.Get<AppConfig>()
    ?? throw new InvalidOperationException("appsettings.json konnte nicht geladen werden.");

// --link: Signal-Verlinkung durchführen und beenden
if (linkMode)
{
    if (!appConfig.Provider.Equals("Signal", StringComparison.OrdinalIgnoreCase))
    {
        ConsoleLog.Error("[BBFon] --link ist nur verfügbar wenn Provider = \"Signal\" in appsettings.json.");
        return;
    }
    await new LinkService(appConfig.Signal).RunAsync();
    return;
}

// --calibrate: Hintergrundlärm messen und Threshold vorschlagen
if (calibrateMode)
{
    await new CalibrateService().RunAsync();
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

using var monitor = new AudioMonitorService(appConfig, notification, recordingEnabled, debugMode);
monitor.Start(cts.Token);

ConsoleLog.Info("\n[BBFon] Beendet.");

// --- Lokale Funktionen ---

static void PrintSettings(AppConfig cfg)
{
    ConsoleLog.Info("[BBFon] --- Einstellungen ---");
    ConsoleLog.Info($"[BBFon]   Provider:       {cfg.Provider}");
    ConsoleLog.Info($"[BBFon]   Schwellwert:    {cfg.Threshold:F2}");
    ConsoleLog.Info($"[BBFon]   Cooldown:       {cfg.CooldownSeconds}s");
    ConsoleLog.Info($"[BBFon]   Nachricht:      {cfg.Message}");
    ConsoleLog.Info($"[BBFon]   Analyse:        {(cfg.Analysis.Enabled ? $"aktiv ({cfg.Analysis.MinTriggerCount}x in {cfg.Analysis.WindowSeconds}s)" : "inaktiv")}");
    ConsoleLog.Info($"[BBFon]   Aufnahme:       {(cfg.Recording.MaxFiles > 0 || cfg.Recording.MaxAgeDays > 0 ? $"Bereinigung: max. {cfg.Recording.MaxFiles} Dateien / {cfg.Recording.MaxAgeDays} Tage" : "keine Bereinigung")}");
    ConsoleLog.Info($"[BBFon]   Komprimierung:  {(cfg.Compression.Enabled ? $"aktiv ({cfg.Compression.Format.ToUpperInvariant()}, {cfg.Compression.BitrateKbps}kbps, WAV löschen: {cfg.Compression.DeleteWavAfterCompress})" : "inaktiv")}");
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
