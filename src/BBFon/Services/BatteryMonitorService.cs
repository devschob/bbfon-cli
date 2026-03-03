using System.Runtime.InteropServices;

namespace BBFon.Services;

public sealed class BatteryMonitorService
{
    private readonly BatteryConfig _config;
    private readonly INotificationService _notification;
    private readonly bool _debugMode;
    private bool _wasAboveThreshold;

    public BatteryMonitorService(BatteryConfig config, INotificationService notification, bool debugMode)
    {
        _config = config;
        _notification = notification;
        _debugMode = debugMode;

        // Initialen Zustand ermitteln – kein Fehlalarm beim Start
        float initial = ReadBatteryPercent();
        _wasAboveThreshold = initial < 0 || initial >= _config.ThresholdPercent;

        if (initial < 0)
            ConsoleLog.Info("[BBFon] Batterie: kein Akku erkannt (Desktop?). Überwachung läuft trotzdem.");
        else
            ConsoleLog.Info($"[BBFon] Batterie: aktuell {initial:F0}% | Schwellwert: {_config.ThresholdPercent}% | Prüfintervall: {_config.CheckIntervalSeconds}s");
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_config.CheckIntervalSeconds), ct);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            float percent = ReadBatteryPercent();
            if (percent < 0) continue;

            bool isBelow = percent < _config.ThresholdPercent;

            if (_debugMode)
                ConsoleLog.Debug($"\n[DEBUG] Batterie-Check: {percent:F0}% (Schwellwert: {_config.ThresholdPercent}%, {(isBelow ? "UNTER" : "über")} Schwellwert, fallende Flanke: {(isBelow && _wasAboveThreshold ? "ja" : "nein")})");

            if (isBelow && _wasAboveThreshold)
            {
                _wasAboveThreshold = false;
                ConsoleLog.Alarm($"\n[BBFon] Batterie unter {_config.ThresholdPercent}%! Aktuell: {percent:F0}%. Sende Nachricht...");
                await SendAsync(percent);
            }
            else if (!isBelow)
            {
                _wasAboveThreshold = true;
            }
        }
    }

    private async Task SendAsync(float percent)
    {
        try
        {
            await _notification.SendAsync($"{_config.Message} ({percent:F0}%)");
            ConsoleLog.Success("[BBFon] Batterie-Warnung gesendet.");
        }
        catch (Exception ex)
        {
            ConsoleLog.Error($"[BBFon] Fehler beim Senden der Batterie-Warnung: {ex.Message}");
        }
    }

    private static float ReadBatteryPercent()
    {
        if (!GetSystemPowerStatus(out var status))
            return -1f;

        // 255 = unbekannt / kein Akku
        return status.BatteryLifePercent == 255 ? -1f : status.BatteryLifePercent;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}
